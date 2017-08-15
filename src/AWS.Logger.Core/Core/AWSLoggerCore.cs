using System;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using System.Collections.Concurrent;
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
        private ConcurrentQueue<InputLogEvent> _pendingMessageQueue = new ConcurrentQueue<InputLogEvent>();
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
            this.Close();
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
            _config.ShutDown();
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
            _cancelStartSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                await Monitor(_cancelStartSource.Token).ConfigureAwait(false);
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
                    _currentStreamName = await CreateLogStream(token).ConfigureAwait(false);
                }

                while (!token.IsCancellationRequested)
                {
                    if (!_pendingMessageQueue.IsEmpty)
                    {
                        var pendingMessages = Interlocked.Exchange(ref _pendingMessageQueue, new ConcurrentQueue<InputLogEvent>());

                        // If the max messages are 10k and there is 25k in the queue, it will create 10k + 10k + 5k chunks
                        var chunks = pendingMessages
                            .ToList()
                            .OrderBy(x => x.Timestamp)
                            .ChunkByIndex(_config.MaxQueuedMessages);

                        int sentBatches = 0;
                        foreach (var chunk in chunks)
                        {
                            if (sentBatches > 0 && sentBatches % 5 == 0)
                            {
                                // CloudWatch API Throttling: 5 request / second. 
                                // In case of 15 chunks, we need to wait 3 times.

                                await Task.Delay(1000).ConfigureAwait(false);
                            }

                            var logBatch = LogEventBatch.CreateBatch(chunk, SequenceToken, _currentStreamName, _config);
                            
                            if (logBatch.TotalBatchSizeInBytes < _config.BatchSizeInBytes)
                            {
                                await SendMessages(logBatch, token).ConfigureAwait(false);

                                sentBatches++;
                            }
                            else
                            {
                                // If the batch is too big in bytes, we cut it in even smaller chunks
                                var minorChunk = chunk.ChunkByIndex(logBatch.Count / 2);

                                foreach (var innerChunk in minorChunk)
                                {
                                    logBatch = LogEventBatch.CreateBatch(innerChunk, SequenceToken, _currentStreamName, _config);

                                    await SendMessages(logBatch, token).ConfigureAwait(false);

                                    sentBatches++;
                                }
                            }                            
                        }
                    }
                    else
                    {
                        // If the logger is being terminated and all the messages have been sent exit out of loop.
                        // If there are messages, keep aggressively polling the message queue until it gets empty, so the process can die, but maximum it has 5 sec.
                        if (_isTerminated)
                        {
                            if (_pendingMessageQueue.IsEmpty || _terminationStopWatch.ElapsedMilliseconds > 5000)
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
        private async Task SendMessages(LogEventBatch logBatch, CancellationToken token)
        {
            if (!logBatch.ShouldSendRequest)
                return;

            try
            {
                var response = await _client.PutLogEventsAsync(logBatch.Request, token).ConfigureAwait(false);

                lock (_sequenceTokenLock)                
                    SequenceToken = response.NextSequenceToken;                

                MaxTryCount = 5;
            }
            catch (TaskCanceledException)
            {
                throw;
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
                        lock(_sequenceTokenLock)                        
                            SequenceToken = logBatch.Request.SequenceToken = regexResult.Groups[1].Value;

                        await SendMessages(logBatch, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    _currentStreamName = await CreateLogStream(token).ConfigureAwait(false);

                    lock(_sequenceTokenLock)                    
                        SequenceToken = null;                    
                }
            }
            catch (Exception e)
            {
                LogLibraryError(e, _config.LibraryLogFileName);
                throw;
            }
        }

        /// <summary>
        /// Creates a logstream.
        /// </summary>
        /// <returns></returns>
        /// 
        private async Task<string> CreateLogStream(CancellationToken token)
        {
            try
            {
                var logGroupResponse = await _client.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = _config.LogGroup
                }, token).ConfigureAwait(false);

                if (logGroupResponse.LogGroups.FirstOrDefault(x => string.Equals(x.LogGroupName, _config.LogGroup, StringComparison.Ordinal)) == null)
                {
                    await _client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = _config.LogGroup }, token).ConfigureAwait(false);
                }

                var streamName = DateTime.Now.ToString("yyyy/MM/ddTHH.mm.ss") + " - " + _config.LogStreamNameSuffix;

                var streamResponse = await _client.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = _config.LogGroup,
                    LogStreamName = streamName
                }, token).ConfigureAwait(false);

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
            public int TotalBatchSizeInBytes => Request.LogEvents.Sum(x => Encoding.Unicode.GetMaxByteCount(x.Message.Length));
            public bool ShouldSendRequest => Count != 0;

            private LogEventBatch(List<InputLogEvent> logs, string sequenceToken, string logGroupName, string streamName)
            {
                Request.LogEvents = logs;
                Request.LogGroupName = logGroupName;
                Request.LogStreamName = streamName;
                Request.SequenceToken = sequenceToken;
            }     

            internal static LogEventBatch CreateBatch(List<InputLogEvent> logs, string sequenceToken, string currentStreamName, AWSLoggerConfig config)
            {
                return new LogEventBatch(logs, sequenceToken, config.LogGroup, currentStreamName);
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
