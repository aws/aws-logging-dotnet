using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AWS.Logger.Core
{
    /// <summary>
    /// Sends LogEvent messages to CloudWatch Logs
    /// </summary>
    public class AWSLoggerCore : IAWSLoggerCore
    {
        const int MAX_MESSAGE_SIZE_IN_BYTES = 256000;

        #region Private Members
        const string EMPTY_MESSAGE = "\t";
        private ConcurrentQueue<InputLogEvent> _pendingMessageQueue = new ConcurrentQueue<InputLogEvent>();
        private string _currentStreamName = null;
        private LogEventBatch _repo = new LogEventBatch();
        private CancellationTokenSource _cancelStartSource;
        private SemaphoreSlim _flushTriggerEvent;
        private ManualResetEventSlim _flushCompletedEvent;
        private AWSLoggerConfig _config;
        private IAmazonCloudWatchLogs _client;
        private DateTime _maxBufferTimeStamp = new DateTime();
        private string _logType;

        private static readonly string _assemblyVersion = typeof(AWSLoggerCore).GetTypeInfo().Assembly.GetName().Version?.ToString() ?? string.Empty;
        private static readonly string _baseUserAgentString = $"lib/aws-logger-core#{_assemblyVersion}";

        /// <summary>
        /// Minimum interval in minutes between two error messages on in-memory buffer overflow.
        /// </summary>
        const double MAX_BUFFER_TIMEDIFF = 5;
        #endregion

        /// <summary>
        /// Alert details from CloudWatch Log Engine
        /// </summary>
        public sealed class LogLibraryEventArgs : EventArgs
        {
            internal LogLibraryEventArgs(Exception ex)
            {
                Exception = ex;
            }

            /// <summary>
            /// Exception Details returned
            /// </summary>
            public Exception Exception { get; }

            /// <summary>
            /// Service EndPoint Url involved
            /// </summary>
            public string ServiceUrl { get; internal set; }
        }

        /// <summary>
        /// Event Notification on alerts from the CloudWatch Log Engine
        /// </summary>
        public event EventHandler<LogLibraryEventArgs> LogLibraryAlert;

        /// <summary>
        /// Construct an instance of AWSLoggerCore
        /// </summary>
        /// <param name="config">Configuration options for logging messages to AWS</param>
        /// <param name="logType">Logging Provider Name to include in UserAgentHeader</param>
        public AWSLoggerCore(AWSLoggerConfig config, string logType)
        {
            _config = config;
            _logType = logType;

            var awsConfig = new AmazonCloudWatchLogsConfig();
            if (!string.IsNullOrWhiteSpace(_config.ServiceUrl))
            {
                var serviceUrl = _config.ServiceUrl.Trim();
                awsConfig.ServiceURL = serviceUrl;
                if (serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    awsConfig.UseHttp = true;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_config.Region))
                {
                    awsConfig.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_config.Region);
                }
            }

            var credentials = DetermineCredentials(config);
            _client = new AmazonCloudWatchLogsClient(credentials, awsConfig);


            ((AmazonCloudWatchLogsClient)this._client).BeforeRequestEvent += ServiceClientBeforeRequestEvent;
            ((AmazonCloudWatchLogsClient)this._client).ExceptionEvent += ServiceClienExceptionEvent;

            StartMonitor();
            RegisterShutdownHook();
        }

        private void RegisterShutdownHook()
        {
            AppDomain.CurrentDomain.DomainUnload += ProcessExit;
            AppDomain.CurrentDomain.ProcessExit += ProcessExit;
        }

        private void ProcessExit(object sender, EventArgs e)
        {
            Close();
        }

        private static AWSCredentials DetermineCredentials(AWSLoggerConfig config)
        {
            if (config.Credentials != null)
            {
                return config.Credentials;
            }
            if (!string.IsNullOrEmpty(config.Profile))
            {
                var credentials = LookupCredentialsFromProfileStore(config);
                if (credentials != null)
                    return credentials;
            }
            return FallbackCredentialsFactory.GetCredentials();
        }

        private static AWSCredentials LookupCredentialsFromProfileStore(AWSLoggerConfig config)
        {
            var credentialProfileStore = string.IsNullOrEmpty(config.ProfilesLocation)
                ? new CredentialProfileStoreChain()
                : new CredentialProfileStoreChain(config.ProfilesLocation);
            if (credentialProfileStore.TryGetAWSCredentials(config.Profile, out var credentials))
                return credentials;
            else
                return null;
        }

        /// <inheritdoc />
        public void Close()
        {
            try
            {
                Flush();
                _cancelStartSource.Cancel();
            }
            catch (Exception ex)
            {
                LogLibraryServiceError(ex);
            }
            finally
            {
                LogLibraryAlert = null;
            }
        }

        /// <inheritdoc />
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
                    System.Threading.Monitor.TryEnter(_flushTriggerEvent, ref lockTaken);
                    if (lockTaken)
                    {
                        _flushCompletedEvent.Reset();
                        if (_flushTriggerEvent.CurrentCount == 0)
                        {
                            _flushTriggerEvent.Release();   // Signal Monitor-Task to start premature flush
                        }
                        else
                        {
                            // Means that the Background Task is busy, and not yet claimed the previous release (Maybe busy with credentials)
                            var serviceUrl = GetServiceUrl();
                            LogLibraryServiceError(new TimeoutException($"Flush Pending - ServiceURL={serviceUrl}, StreamName={_currentStreamName}, PendingMessages={_pendingMessageQueue.Count}, CurrentBatch={_repo.CurrentBatchMessageCount}"), serviceUrl);
                        }
                    }

                    // Waiting for Monitor-Task to complete flush
                    if (!_flushCompletedEvent.Wait(_config.FlushTimeout, _cancelStartSource.Token))
                    {
                        var serviceUrl = GetServiceUrl();
                        LogLibraryServiceError(new TimeoutException($"Flush Timeout - ServiceURL={serviceUrl}, StreamName={_currentStreamName}, PendingMessages={_pendingMessageQueue.Count}, CurrentBatch={_repo.CurrentBatchMessageCount}"), serviceUrl);
                    }
                }
                finally
                {
                    if (lockTaken)
                        System.Threading.Monitor.Exit(_flushTriggerEvent);
                }
            }
        }

        private string GetServiceUrl()
        {
            try
            {
                _client.Config.Validate();
                return _client.Config.DetermineServiceURL() ?? "Undetermined ServiceURL";
            }
            catch (Exception ex)
            {
                LogLibraryServiceError(ex, string.Empty);
                return "Unknown ServiceURL";
            }
        }

        private void AddSingleMessage(string message)
        {
            if (_pendingMessageQueue.Count > _config.MaxQueuedMessages)
            {
                if (_maxBufferTimeStamp.AddMinutes(MAX_BUFFER_TIMEDIFF) < DateTime.UtcNow)
                {
                    message = "The AWS Logger in-memory buffer has reached maximum capacity";
                    if (_maxBufferTimeStamp == DateTime.MinValue)
                    {
                        LogLibraryServiceError(new System.InvalidOperationException(message));
                    }
                    _maxBufferTimeStamp = DateTime.UtcNow;
                    _pendingMessageQueue.Enqueue(new InputLogEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        Message = message,
                    });
                }
            }
            else
            {
                _pendingMessageQueue.Enqueue(new InputLogEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Message = message,
                });
            }
        }

        /// <summary>
        /// A Concurrent Queue is used to store the messages from 
        /// the logger
        /// </summary>
        /// <param name="rawMessage">Message to log.</param>
        public void AddMessage(string rawMessage)
        {
            if (string.IsNullOrEmpty(rawMessage))
            {
                rawMessage = EMPTY_MESSAGE;
            }

            // Only do the extra work of breaking up the message if the max unicode bytes exceeds the possible size. This is not
            // an exact measurement since the string is UTF8 but it gives us a chance to skip the extra computation for 
            // typically small messages.
            if (Encoding.Unicode.GetMaxByteCount(rawMessage.Length) < MAX_MESSAGE_SIZE_IN_BYTES)
            {
                AddSingleMessage(rawMessage);
            }
            else
            {
                var messageParts = BreakupMessage(rawMessage);
                foreach (var message in messageParts)
                {
                    AddSingleMessage(message);
                }
            }
        }

        /// <summary>
        /// Finalizer to ensure shutdown when forgetting to dispose
        /// </summary>
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
        public void StartMonitor()
        {
            _flushTriggerEvent = new SemaphoreSlim(0, 1);
            _flushCompletedEvent = new ManualResetEventSlim(false);
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

            while (_currentStreamName == null && !token.IsCancellationRequested)
            {
                try
                {
                    _currentStreamName = await LogEventTransmissionSetup(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    if (!_pendingMessageQueue.IsEmpty)
                        LogLibraryServiceError(ex);
                    if (token.IsCancellationRequested)
                    {
                        _client.Dispose();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // We don't want to kill the main monitor loop. We will simply log the error, then continue.
                    // If it is an OperationCancelledException, die
                    LogLibraryServiceError(ex);
                    await Task.Delay(Math.Max(100, DateTime.UtcNow.Second * 10), token);
                }
            }

            while (!token.IsCancellationRequested)
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
                    }

                    if (executeFlush)
                        _flushCompletedEvent.Set();

                    executeFlush = await _flushTriggerEvent.WaitAsync(TimeSpan.FromMilliseconds(_config.MonitorSleepTime.TotalMilliseconds), token);
                }
                catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
                {
                    // Workaround to handle timeouts of .net httpclient 
                    // https://github.com/dotnet/corefx/issues/20296
                    LogLibraryServiceError(ex);
                }
                catch (OperationCanceledException ex)
                {
                    if (!token.IsCancellationRequested || !_repo.IsEmpty || !_pendingMessageQueue.IsEmpty)
                        LogLibraryServiceError(ex);
                    _client.Dispose();
                    return;
                }
                catch (Exception ex)
                {
                    // We don't want to kill the main monitor loop. We will simply log the error, then continue.
                    // If it is an OperationCancelledException, die
                    LogLibraryServiceError(ex);
                }
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
                _repo.Reset();
            }
            catch (ResourceNotFoundException ex)
            {
                // The specified log stream does not exist. Refresh or create new stream.
                LogLibraryServiceError(ex);

                _currentStreamName = await LogEventTransmissionSetup(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates and Allocates resources for message trasnmission
        /// </summary>
        /// <returns></returns>
        private async Task<string> LogEventTransmissionSetup(CancellationToken token)
        {
            string serviceURL = GetServiceUrl();

            if (!_config.DisableLogGroupCreation)
            {
                var logGroupResponse = await _client.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = _config.LogGroup
                }, token).ConfigureAwait(false);
                if (!IsSuccessStatusCode(logGroupResponse))
                {
                    LogLibraryServiceError(new System.Net.WebException($"Lookup LogGroup {_config.LogGroup} returned status: {logGroupResponse.HttpStatusCode}"), serviceURL);
                }

                if (logGroupResponse.LogGroups.FirstOrDefault(x => string.Equals(x.LogGroupName, _config.LogGroup, StringComparison.Ordinal)) == null)
                {
                    var createGroupResponse = await _client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = _config.LogGroup }, token).ConfigureAwait(false);
                    if (!IsSuccessStatusCode(createGroupResponse))
                    {
                        LogLibraryServiceError(new System.Net.WebException($"Create LogGroup {_config.LogGroup} returned status: {createGroupResponse.HttpStatusCode}"), serviceURL);
                    }
                    else if (_config.NewLogGroupRetentionInDays.HasValue && _config.NewLogGroupRetentionInDays.Value > 0)
                    {
                        // If CreateLogGroup returns a success status code then this process is responsible for applying the retention policy.
                        // This prevents a case of multiple instances each trying to set the retention policy. 
                        PutRetentionPolicy(_config.NewLogGroupRetentionInDays.Value,_config.LogGroup, serviceURL, token);
                    }
                }
            }

            var currentStreamName = GenerateStreamName(_config);

            var streamResponse = await _client.CreateLogStreamAsync(new CreateLogStreamRequest
            {
                LogGroupName = _config.LogGroup,
                LogStreamName = currentStreamName
            }, token).ConfigureAwait(false);
            if (!IsSuccessStatusCode(streamResponse))
            {
                LogLibraryServiceError(new System.Net.WebException($"Create LogStream {currentStreamName} for LogGroup {_config.LogGroup} returned status: {streamResponse.HttpStatusCode}"), serviceURL);
            }

            _repo = new LogEventBatch(_config.LogGroup, currentStreamName, Convert.ToInt32(_config.BatchPushInterval.TotalSeconds), _config.BatchSizeInBytes);

            return currentStreamName;
        }

        /// <summary>
        ///     Puts a retention policy on a log group.
        /// </summary>
        private void PutRetentionPolicy(int logGroupRetentionInDays, string logGroup, string serviceURL, CancellationToken token)
        {
            _ = Task.Run(async () =>
                {
                    try
                    {
                        var putPolicyResponse = await _client.PutRetentionPolicyAsync(new PutRetentionPolicyRequest(logGroup, logGroupRetentionInDays), token).ConfigureAwait(false);
                        if (!IsSuccessStatusCode(putPolicyResponse))
                        {
                            LogLibraryServiceError(new System.Net.WebException($"Put retention policy {logGroupRetentionInDays} for LogGroup {logGroup} returned status: {putPolicyResponse.HttpStatusCode}"), serviceURL);
                        }
                    }
                    catch (Exception e)
                    {
                        LogLibraryServiceError(new System.Net.WebException($"Unexpected error putting retention policy {logGroupRetentionInDays} for LogGroup {logGroup}", e), serviceURL); 
                    }
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates a log stream name based either on the explicit one specified in the config, or the generated one 
        /// using the prefix, suffix, and date
        /// </summary>
        /// <returns>Log stream name</returns>
        public static string GenerateStreamName(IAWSLoggerConfig config)
        {
            if (!string.IsNullOrEmpty(config.LogStreamName))
            {
                return config.LogStreamName;
            }

            var streamName = new StringBuilder();

            var prefix = config.LogStreamNamePrefix;
            if (!string.IsNullOrEmpty(prefix))
            {
                streamName.Append(prefix);
                streamName.Append(" - ");
            }

            streamName.Append(DateTime.Now.ToString("yyyy/MM/ddTHH.mm.ss"));

            var suffix = config.LogStreamNameSuffix;
            if (!string.IsNullOrEmpty(suffix))
            {
                streamName.Append(" - ");
                streamName.Append(suffix);
            }


            return streamName.ToString();
        }

        private static bool IsSuccessStatusCode(AmazonWebServiceResponse serviceResponse)
        {
            return (int)serviceResponse.HttpStatusCode >= 200 && (int)serviceResponse.HttpStatusCode <= 299;
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
                Reset();
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

            public void Reset()
            {
                _request.LogEvents.Clear();
                _totalMessageSize = 0;
                _nextPushTime = DateTime.UtcNow.Add(TimeIntervalBetweenPushes);
            }
        }

        const string UserAgentHeader = "User-Agent";
        void ServiceClientBeforeRequestEvent(object sender, RequestEventArgs e)
        {
            var userAgentString = $"{_baseUserAgentString} ft/{_logType}";
            var args = e as Amazon.Runtime.WebServiceRequestEventArgs;
            if (args == null || !args.Headers.ContainsKey(UserAgentHeader) || args.Headers[UserAgentHeader].Contains(userAgentString))
                return;

            args.Headers[UserAgentHeader] = args.Headers[UserAgentHeader] + " " + userAgentString;
        }

        void ServiceClienExceptionEvent(object sender, ExceptionEventArgs e)
        {
            var eventArgs = e as WebServiceExceptionEventArgs;
            if (eventArgs?.Exception != null)
                LogLibraryServiceError(eventArgs?.Exception, eventArgs.Endpoint?.ToString());
            else
                LogLibraryServiceError(new System.Net.WebException(e.GetType().ToString()));
        }

        private void LogLibraryServiceError(Exception ex, string serviceUrl = null)
        {
            LogLibraryAlert?.Invoke(this, new LogLibraryEventArgs(ex) { ServiceUrl = serviceUrl ?? GetServiceUrl() });
            if (!string.IsNullOrEmpty(_config.LibraryLogFileName) && _config.LibraryLogErrors)
            {
                LogLibraryError(ex, _config.LibraryLogFileName);
            }
        }

        /// <summary>
        /// Write Exception details to the file specified with the filename
        /// </summary>
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
