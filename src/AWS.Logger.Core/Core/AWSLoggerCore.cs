using System;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
#if CORECLR
using System.Runtime.Loader;
#endif
using System.Reflection;

namespace AWS.Logger.Core
{
    public class AWSLoggerCore : IAWSLoggerCore
    {
        #region Private Members
        private Object _sequenceTokenLock = new Object();
        private static string[] TransientErrorCodes = { "ThrottlingException" };
        private const string EMPTY_MESSAGE = "\t";
        private Queue<InputLogEvent> _pendingMessageQueue = new Queue<InputLogEvent>(200000);
        private string SequenceToken = null;
        private string _currentStreamName = null;
        private CancellationTokenSource _cancelStartSource;
        private AWSLoggerConfig _config;
        private IAmazonCloudWatchLogs _client;
        private bool _isTerminated = false;
        private Stopwatch _terminationStopWatch = new Stopwatch();
        private string _logType;
        private static int MaxTryCount = 5;
        private const double MAX_BUFFER_TIMEDIFF = 5;
        private const string UserAgentHeader = "User-Agent";
        private readonly static Regex Invalid_sequence_token_regex = new Regex(
            @"The given sequenceToken is invalid. The next expected sequenceToken is: (\d+)");
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
            Close();
        }

#elif NET452

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
            if (!string.IsNullOrEmpty(config.Profile) && StoredProfileAWSCredentials.IsProfileKnown(config.Profile, config.ProfilesLocation))
            {
                return new StoredProfileAWSCredentials(config.Profile, config.ProfilesLocation);
            }

            return FallbackCredentialsFactory.GetCredentials();
        }

        public void Close()
        {
            _isTerminated = true;
            _terminationStopWatch.Start();
            _cancelStartSource.Cancel();
            Task.Run(async () =>
            {
                await Monitor(CancellationToken.None).ConfigureAwait(false);
            }).Wait();
        }


        /// <summary>
        /// A Concurrent Queue is used to store the messages from 
        /// the logger
        /// </summary>
        /// <param name="message"></param>
        public void AddMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = EMPTY_MESSAGE;
            }

            _pendingMessageQueue.Enqueue(new InputLogEvent
            {
                Timestamp = DateTime.Now,
                Message = message,
            });
        }

        ~AWSLoggerCore() => _cancelStartSource?.Dispose();

        /// <summary>
        /// Kicks off the Poller Thread to keep tabs on the PutLogEvent request and the
        /// Concurrent Queue
        /// </summary>
        /// <param name="PatrolSleepTime"></param>
        public void StartMonitor()
        {
            _cancelStartSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                byte errorCount = 0;
                while (true)
                {
                    if (_isTerminated) break;

                    try
                    {
                        await Monitor(_cancelStartSource.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        errorCount++;
                        if (errorCount > 5)
                            throw;
                    }
                }
            });
        }

        /// <summary>
        /// Patrolling thread. keeps tab on the PutLogEvent request and the
        /// Concurrent Queue
        /// </summary>
        private async Task Monitor(CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                if (_currentStreamName == null)
                {
                    _currentStreamName = await CreateLogStreamAsync(token).ConfigureAwait(false);
                }

                while (!token.IsCancellationRequested)
                {
                    if (_pendingMessageQueue.Count > 0)
                    {
                        var pendingMessages = Interlocked.Exchange(ref _pendingMessageQueue, new Queue<InputLogEvent>(200000));

                        var batches = LogEventBatch.CreateBatches(pendingMessages, _currentStreamName, _config);

                        foreach (var batch in batches)
                        {
                            await SendBatchAsync(batch, token).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        // If the logger is being terminated and all the messages have been sent, then exit out of loop.
                        // If there are messages left, then keep aggressively polling the message queue until it gets empty, so the process can die, but maximum it has 5 sec.
                        if (_isTerminated)
                        {
                            if (_pendingMessageQueue.Count == 0 || _terminationStopWatch.ElapsedMilliseconds > 5000)
                                break;
                            else
                                continue;
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(_config.MonitorSleepTime.TotalMilliseconds)).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException oc)
            {
                LogLibraryError(oc, _config.LibraryLogFileName);
                throw;
            }
            catch (AmazonServiceException amazonEx)
            {
                LogLibraryError(amazonEx, _config.LibraryLogFileName);

                if (!TransientErrorCodes.Any(x => amazonEx.ErrorCode.Equals(x)))
                    throw;
            }
            catch (Exception ex)
            {
                LogLibraryError(ex, _config.LibraryLogFileName);
            }

        }

        /// <summary>
        /// Method to transmit the PutLogEvent Request
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task SendBatchAsync(LogEventBatch logBatch, CancellationToken token)
        {
            if (!logBatch.ShouldSendRequest)
                return;

            try
            {
                lock (_sequenceTokenLock)
                    logBatch.Request.SequenceToken = SequenceToken;

                var response = await _client.PutLogEventsAsync(logBatch.Request, token).ConfigureAwait(false);

                lock (_sequenceTokenLock)
                    SequenceToken = response.NextSequenceToken;

                MaxTryCount = 5;
            }
            catch (InvalidSequenceTokenException ex)
            {
                //In case the NextSequenceToken is invalid for the last sent message, a new stream would be 
                //created for the said application.
                LogLibraryError(ex, _config.LibraryLogFileName);
                if (MaxTryCount > 0)
                {
                    MaxTryCount--;
                    var regexResult = Invalid_sequence_token_regex.Match(ex.Message);
                    if (regexResult.Success)
                    {
                        lock (_sequenceTokenLock)
                            SequenceToken = logBatch.Request.SequenceToken = regexResult.Groups[1].Value;

                        await SendBatchAsync(logBatch, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    _currentStreamName = await CreateLogStreamAsync(token).ConfigureAwait(false);

                    lock (_sequenceTokenLock)
                        SequenceToken = null;
                }
            }
            catch(AmazonUnmarshallingException)
            {
                // Giving it one more chance here. 
                // TODO: Create RetryManager class and retry transient errors
                lock (_sequenceTokenLock)
                    logBatch.Request.SequenceToken = SequenceToken;

                var response = await _client.PutLogEventsAsync(logBatch.Request, token).ConfigureAwait(false);

                lock (_sequenceTokenLock)
                    SequenceToken = response.NextSequenceToken;
            }
            catch (Exception e)
            {
                LogLibraryError(e, _config.LibraryLogFileName);
                throw;
            }
        }

        /// <summary>
        /// Creates a logstream and returns its name.
        /// </summary>
        /// <returns></returns>
        /// 
        private async Task<string> CreateLogStreamAsync(CancellationToken token)
        {
            try
            {
                var logGroupResponse = await _client.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = _config.LogGroup
                }, token).ConfigureAwait(false);

                if (!logGroupResponse.LogGroups.Any(x => string.Equals(x.LogGroupName, _config.LogGroup, StringComparison.Ordinal)))
                {
                    try
                    {
                        await _client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = _config.LogGroup }, token).ConfigureAwait(false);
                    }
                    catch (ResourceAlreadyExistsException)
                    {
                        // Suppressed due to possible race conditions
                    }
                }

                var streamName = DateTime.Now.ToString("yyyy/MM/ddTHH.mm.ss") + " - " + _config.LogStreamNameSuffix;

                try
                {
                    await _client.CreateLogStreamAsync(new CreateLogStreamRequest
                    {
                        LogGroupName = _config.LogGroup,
                        LogStreamName = streamName
                    }, token).ConfigureAwait(false);
                }
                catch (ResourceAlreadyExistsException)
                {
                    // Suppressed due to possible race conditions
                }


                return streamName;
            }
            catch (Exception e)
            {
                LogLibraryError(e, _config.LibraryLogFileName);
                throw;
            }

        }

        /// <summary>
        /// Class to handle PutLogEvent request and associated parameters.
        /// </summary>
        private class LogEventBatch
        {
            public PutLogEventsRequest Request = new PutLogEventsRequest();
            public int Count => Request.LogEvents.Count;
            public bool IsEmpty => Count == 0;
            public bool ShouldSendRequest => Count != 0;

            private LogEventBatch(List<InputLogEvent> logs, string logGroupName, string streamName)
            {
                Request.LogEvents = logs;
                Request.LogGroupName = logGroupName;
                Request.LogStreamName = streamName;
            }

            internal static IEnumerable<LogEventBatch> CreateBatches(Queue<InputLogEvent> allLogEvents, string currentStreamName, AWSLoggerConfig config)
            {
                while (allLogEvents.Count > 0)
                {
                    var currentLogEvents = new List<InputLogEvent>();
                    long currentBatchSizeInBytes = 0;

                    while (allLogEvents.Count > 0)
                    {
                        var logEvent = allLogEvents.Dequeue();

                        if (logEvent == null) continue;

                        var logEventSize = Encoding.Unicode.GetMaxByteCount(logEvent.Message.Length);

                        if (currentBatchSizeInBytes + logEventSize <= config.BatchSizeInBytes
                          && currentLogEvents.Count < config.MaxQueuedMessages)
                        {
                            currentBatchSizeInBytes += logEventSize;
                            currentLogEvents.Add(logEvent);

                            if (allLogEvents.Count == 0)
                            {
                                yield return new LogEventBatch
                                (
                                   currentLogEvents.OrderBy(x => x.Timestamp).ToList(),
                                   config.LogGroup,
                                   currentStreamName
                                );

                                break;
                            }
                        }
                        else
                        {
                            // A constraint is violated, this logEvent will be sent in the next batch
                            allLogEvents.Enqueue(logEvent);

                            yield return new LogEventBatch
                            (
                               currentLogEvents.OrderBy(x => x.Timestamp).ToList(),
                               config.LogGroup,
                               currentStreamName
                            );

                            currentLogEvents.Clear();
                            currentBatchSizeInBytes = 0;
                        }
                    }
                }
            }
        }

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
