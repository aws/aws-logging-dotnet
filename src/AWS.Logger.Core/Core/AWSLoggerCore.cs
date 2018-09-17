using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
#if CORECLR
using System.Runtime.Loader;
#endif
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace AWS.Logger.Core
{
    public class AWSLoggerCore : IAWSLoggerCore
    {
        const int MAX_MESSAGE_SIZE_IN_BYTES = 256000;

        #region Private Members
        const string EMPTY_MESSAGE = "\t";
        private ConcurrentQueue<InputLogEvent> _pendingMessageQueue = new ConcurrentQueue<InputLogEvent>();
        private string _currentStreamName = null;
        private LogEventBatch _repo = new LogEventBatch();
        private CancellationTokenSource _cancelStartSource;
        private SemaphoreSlim _flushEventCounter;
        private AWSLoggerConfig _config;
        private IAmazonCloudWatchLogs _client;
        private DateTime _maxBufferTimeStamp = new DateTime();
        private string _logType;
        private static int requestCount = 5;
        const double MAX_BUFFER_TIMEDIFF = 5;
        private readonly static Regex invalid_sequence_token_regex = new
            Regex(@"The given sequenceToken is invalid. The next expected sequenceToken is: (\d+)");

        #endregion
        public AWSLoggerCore(AWSLoggerConfig config, string logType)
        {
            _config = config;
            _logType = logType;

            var credentials = DetermineCredentials(config);

            if (_config.Region != null)
            {
                _client = new AmazonCloudWatchLogsClient(credentials, Amazon.RegionEndpoint.GetBySystemName(_config.Region));
            }
            else
            {
                _client = new AmazonCloudWatchLogsClient(credentials);
            }

            ((AmazonCloudWatchLogsClient)this._client).BeforeRequestEvent += ServiceClientBeforeRequestEvent;

            StartMonitor();
            RegisterShutdownHook();
        }


#if CORECLR
        private void RegisterShutdownHook()
        {
            var currentAssembly = typeof(AWSLoggerCore).GetTypeInfo().Assembly;
            AssemblyLoadContext.GetLoadContext(currentAssembly).Unloading += this.OnAssemblyLoadContextUnloading;
        }

        internal void OnAssemblyLoadContextUnloading(AssemblyLoadContext obj)
        {
            this.Close();
        }
#else
        private void RegisterShutdownHook()
        {
            AppDomain.CurrentDomain.DomainUnload += ProcessExit;
            AppDomain.CurrentDomain.ProcessExit += ProcessExit;
        }

        private void ProcessExit(object sender, EventArgs e)
        {
            Close();
        }
#endif

        private static AWSCredentials DetermineCredentials(AWSLoggerConfig config)
        {
            if (config.Credentials != null)
            {
                return config.Credentials;
            }
            if (!string.IsNullOrEmpty(config.Profile) && new CredentialProfileStoreChain(config.ProfilesLocation)
                .TryGetAWSCredentials(config.Profile, out var credentials))
            {
                return credentials;
            }
            return FallbackCredentialsFactory.GetCredentials();
        }

        public void Close()
        {
            Flush();
            _cancelStartSource.Cancel();
        }

        public void Flush()
        {
            if (_cancelStartSource.IsCancellationRequested)
                return;

            if (!_pendingMessageQueue.IsEmpty || !_repo.IsEmpty)
            {
                bool lockTaken = false;
                try
                {
                    // Ensure only one thread executes the flush operation
                    System.Threading.Monitor.TryEnter(_flushEventCounter, ref lockTaken);
                    if (lockTaken)
                    {
                        _flushEventCounter.Release();   // Signal Monitor-Task to start premature flush

                        bool flushStarted = false;
                        for (int i = 0; i < 150; ++i)
                        {
                            Task.Delay(100).Wait();     // Waiting for Monitor-Task to complete flush
                            if (_flushEventCounter.CurrentCount == 0)
                            {
                                if (!flushStarted)
                                {
                                    flushStarted = true;// Monitor-Task have now started premature-flush
                                    _flushEventCounter.Release();
                                }
                                else
                                {
                                    break;              // Monitor-Task have now completed premature-flush
                                }
                            }
                        }
                    }
                    else
                    {
                        // Someone else is waiting for flush, lets join
                        System.Threading.Monitor.TryEnter(_flushEventCounter, TimeSpan.FromSeconds(15), ref lockTaken);
                    }
                }
                finally
                { 
                    if (lockTaken)
                        System.Threading.Monitor.Exit(_flushEventCounter);
                }
            }
        }

        /// <summary>
        /// A Concurrent Queue is used to store the messages from 
        /// the logger
        /// </summary>
        /// <param name="message"></param>
        public void AddMessage(string rawMessage)
        {
            Action<string> processor = (message) =>
            {
                if (_pendingMessageQueue.Count > _config.MaxQueuedMessages)
                {
                    if ((_maxBufferTimeStamp == DateTime.MinValue) || (DateTime.UtcNow > _maxBufferTimeStamp.Add(TimeSpan.FromMinutes(MAX_BUFFER_TIMEDIFF))))
                    {
                        _maxBufferTimeStamp = DateTime.UtcNow;
                        message = "The AWS Logger in-memory buffer has reached maximum capacity";
                        _pendingMessageQueue.Enqueue(new InputLogEvent
                        {
                            Timestamp = DateTime.Now,
                            Message = message,
                        });
                    }
                }
                else
                {
                    _pendingMessageQueue.Enqueue(new InputLogEvent
                    {
                        Timestamp = DateTime.Now,
                        Message = message,
                    });
                }
            };

            if (string.IsNullOrEmpty(rawMessage))
            {
                rawMessage = EMPTY_MESSAGE;
            }

            // Only do the extra work of breaking up the message if the max unicode bytes exceeds the possible size. This is not
            // an exact measurement since the string is UTF8 but it gives us a chance to skip the extra computation for 
            // typically small messages.
            if (Encoding.Unicode.GetMaxByteCount(rawMessage.Length) < MAX_MESSAGE_SIZE_IN_BYTES)
            {
                processor(rawMessage);
            }
            else
            {
                var messageParts = BreakupMessage(rawMessage);
                foreach(var message in messageParts)
                {
                    processor(message);
                }
            }
        }

        ~AWSLoggerCore()
        {
            if (_cancelStartSource != null)
            {
                _cancelStartSource.Dispose();
            }
        }

        /// <summary>
        /// Kicks off the Poller Thread to keep tabs on the PutLogEvent request and the
        /// Concurrent Queue
        /// </summary>
        /// <param name="PatrolSleepTime"></param>
        public void StartMonitor()
        {
            _flushEventCounter = new SemaphoreSlim(0, 1);
            _cancelStartSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                await Monitor(_cancelStartSource.Token);
            });
        }

        /// <summary>
        /// Patrolling thread. keeps tab on the PutLogEvent request and the
        /// Concurrent Queue
        /// </summary>
        private async Task Monitor(CancellationToken token)
        {
            bool executeFlush = false;

            try
            {
                if (_currentStreamName == null)
                {
                    await LogEventTransmissionSetup(token).ConfigureAwait(false);
                }

                while (true)
                {
                    try
                    {
                        while (_pendingMessageQueue.TryDequeue(out var inputLogEvent))
                        {
                            // See if new message will cause the current batch to violote the size constraint.
                            // If so send the current batch now before adding more to the batch of messages to send.
                            if (_repo.CurrentBatchMessageCount > 0 && _repo.IsSizeConstraintViolated(inputLogEvent.Message))
                            {
                                await SendMessages(token).ConfigureAwait(false);
                            }

                            _repo.AddMessage(inputLogEvent);
                        }

                        if (_repo.ShouldSendRequest(_config.MaxQueuedMessages) || (executeFlush && !_repo.IsEmpty))
                        {
                            await SendMessages(token).ConfigureAwait(false);
                            executeFlush = false;
                        }

                        executeFlush = await _flushEventCounter.WaitAsync(Convert.ToInt32(_config.MonitorSleepTime.TotalMilliseconds), token);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        // We don't want to kill the main monitor loop. We will simply log the error, then continue.
                        // If it is an OperationCancelledException, die
                        LogLibraryError(ex, _config.LibraryLogFileName);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //Just exit the method
            }
        }

        /// <summary>
        /// Method to transmit the PutLogEvent Request
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task SendMessages(CancellationToken token)
        {
            try
            {
                //Make sure the log events are in the right order.
                _repo._request.LogEvents.Sort((ev1, ev2) => ev1.Timestamp.CompareTo(ev2.Timestamp));
                var response = await _client.PutLogEventsAsync(_repo._request, token).ConfigureAwait(false);
                _repo.Reset(response.NextSequenceToken);
                requestCount = 5;
            }
            catch (InvalidSequenceTokenException ex)
            {
                //In case the NextSequenceToken is invalid for the last sent message, a new stream would be 
                //created for the said application.
                LogLibraryError(ex, _config.LibraryLogFileName);
                if (requestCount > 0)
                {
                    requestCount--;
                    var regexResult = invalid_sequence_token_regex.Match(ex.Message);
                    if (regexResult.Success)
                    {
                        _repo._request.SequenceToken = regexResult.Groups[1].Value;
                        await SendMessages(token).ConfigureAwait(false);
                    }
                }
                else
                {
                    await LogEventTransmissionSetup(token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Creates and Allocates resources for message trasnmission
        /// </summary>
        /// <returns></returns>
        /// 
        private async Task LogEventTransmissionSetup(CancellationToken token)
        {
            var logGroupResponse = await _client.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
            {
                LogGroupNamePrefix = _config.LogGroup
            }, token).ConfigureAwait(false);

            if (logGroupResponse.LogGroups.FirstOrDefault(x => string.Equals(x.LogGroupName, _config.LogGroup, StringComparison.Ordinal)) == null)
            {
                await _client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = _config.LogGroup }, token);
            }
            _currentStreamName = DateTime.Now.ToString("yyyy/MM/ddTHH.mm.ss") + " - " + _config.LogStreamNameSuffix;

            var streamResponse = await _client.CreateLogStreamAsync(new CreateLogStreamRequest
            {
                LogGroupName = _config.LogGroup,
                LogStreamName = _currentStreamName
            }, token).ConfigureAwait(false);

            _repo = new LogEventBatch(_config.LogGroup, _currentStreamName, Convert.ToInt32(_config.BatchPushInterval.TotalSeconds), _config.BatchSizeInBytes);
        }

        /// <summary>
        /// Break up the message into max parts of 256K.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static IList<string> BreakupMessage(string message)
        {
            var parts = new List<string>();

            var singleCharArray = new char[1];
            var encoding = Encoding.UTF8;
            int byteCount = 0;
            var sb = new StringBuilder(MAX_MESSAGE_SIZE_IN_BYTES);
            foreach (var c in message)
            {
                singleCharArray[0] = c;
                byteCount += encoding.GetByteCount(singleCharArray);
                sb.Append(c);

                // This could go a couple bytes
                if (byteCount > MAX_MESSAGE_SIZE_IN_BYTES)
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                    byteCount = 0;
                }
            }

            if (sb.Length > 0)
            {
                parts.Add(sb.ToString());
            }

            return parts;
        }

        /// <summary>
        /// Class to handle PutLogEvent request and associated parameters. 
        /// Also has the requisite checks to determine when the object is ready for Transmission.
        /// </summary>
        private class LogEventBatch
        {
            public TimeSpan TimeIntervalBetweenPushes { get; private set; }
            public int MaxBatchSize { get; private set; }

            public bool ShouldSendRequest(int maxQueuedEvents)
            {
                if (_request.LogEvents.Count == 0)
                    return false;

                if (_nextPushTime < DateTime.UtcNow)
                    return true;

                if (maxQueuedEvents <= _request.LogEvents.Count)
                    return true;

                return false;
            }

            int _totalMessageSize { get; set; }
            DateTime _nextPushTime;
            public PutLogEventsRequest _request = new PutLogEventsRequest();
            public LogEventBatch(string logGroupName, string streamName, int timeIntervalBetweenPushes, int maxBatchSize)
            {
                _request.LogGroupName = logGroupName;
                _request.LogStreamName = streamName;
                TimeIntervalBetweenPushes = TimeSpan.FromSeconds(timeIntervalBetweenPushes);
                MaxBatchSize = maxBatchSize;
                Reset(null);
            }

            public LogEventBatch()
            {
            }

            public int CurrentBatchMessageCount
            {
                get { return this._request.LogEvents.Count; }
            }

            public bool IsEmpty => _request.LogEvents.Count == 0;

            public bool IsSizeConstraintViolated(string message)
            {
                Encoding unicode = Encoding.Unicode;
                int prospectiveLength = _totalMessageSize + unicode.GetMaxByteCount(message.Length);
                if (MaxBatchSize < prospectiveLength)
                    return true;

                return false;
            }

            public void AddMessage(InputLogEvent ev)
            {
                Encoding unicode = Encoding.Unicode;
                _totalMessageSize += unicode.GetMaxByteCount(ev.Message.Length);
                _request.LogEvents.Add(ev);
            }

            public void Reset(string SeqToken)
            {
                _request.LogEvents.Clear();
                _totalMessageSize = 0;
                _request.SequenceToken = SeqToken;
                _nextPushTime = DateTime.UtcNow.Add(TimeIntervalBetweenPushes);
            }
        }

        const string UserAgentHeader = "User-Agent";
        void ServiceClientBeforeRequestEvent(object sender, RequestEventArgs e)
        {
            Amazon.Runtime.WebServiceRequestEventArgs args = e as Amazon.Runtime.WebServiceRequestEventArgs;
            if (args == null || !args.Headers.ContainsKey(UserAgentHeader))
                return;

            args.Headers[UserAgentHeader] = args.Headers[UserAgentHeader] + " AWSLogger/" + _logType;
        }

        public static void LogLibraryError(Exception ex, string LibraryLogFileName)
        {
            try
            {
                using (StreamWriter w = File.AppendText(LibraryLogFileName))
                {
                    w.WriteLine("Log Entry : ");
                    w.WriteLine("{0}", DateTime.Now.ToString());
                    w.WriteLine("  :");
                    w.WriteLine("  :{0}", ex.ToString());
                    w.WriteLine("-------------------------------");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception caught when writing error log to file" + e.ToString());
            }
        }
    }
}
