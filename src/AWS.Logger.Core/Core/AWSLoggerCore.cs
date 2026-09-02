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
using Amazon.Runtime.Credentials;

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
        private DateTime _maxBufferTimeStamp = new DateTime();
        private string _logType;

        /// <summary>
        /// Set once <see cref="Close"/> begins. While set, the background monitor keeps draining the pending
        /// queue and the current batch until both are empty (issue #372) instead of doing a single flush pass.
        /// </summary>
        private volatile bool _shutdownInitiated;

        /// <summary>
        /// A short-lived, bounded-deadline token source used for the final drain during shutdown. It is
        /// deliberately separate from <see cref="_cancelStartSource"/> so that cancelling the monitor does not
        /// abort the final PutLogEvents calls. Its deadline (<see cref="AWSLoggerConfig.FlushTimeout"/>) means
        /// the drain can never block forever, even under a continuous producer.
        /// </summary>
        private CancellationTokenSource _shutdownGraceSource;

        /// <summary>Guards against <see cref="Close"/> running more than once (ProcessExit + explicit Dispose).</summary>
        private int _closeCalled;

        /// <summary>
        /// Internal CloudWatch Logs client
        /// </summary>
        /// <remarks>
        /// We defer the initialization of the client until it is first accessed. This avoids a deadlock for log4net:
        ///   1. The thread creating the logger (which contains the CWL client) gets an internal lock in log4net, then tries to 
        ///      access SDK configuration via the static FallbackInternalConfigurationFactory.
        ///   2. The timer thread the SDK uses to load EC2 IMDS credentials requests SDK configuration via 
        ///      FallbackInternalConfigurationFactory, which attempts to create additional loggers for logging the configuration loading.
        /// There's an implicit lock around FallbackInternalConfigurationFactory's static constructor, so these two threads deadlock.
        /// 
        /// By delaying initializing the internal client, we delay starting thread 2 until thread 1 has finished, that way we're 
        /// not creating additional log4net loggers in FallbackInternalConfigurationFactory while another thread is holding the log4net lock.
        /// </remarks>
        private Lazy<IAmazonCloudWatchLogs> _client;

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

            if (config.PreconfiguredServiceClient == null)
            {
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

                if (!string.IsNullOrEmpty(_config.AuthenticationRegion))
                {
                    awsConfig.AuthenticationRegion = _config.AuthenticationRegion;
                }

                _client = new Lazy<IAmazonCloudWatchLogs>(() =>
                {
                    var credentials = DetermineCredentials(config);
                    var client = new AmazonCloudWatchLogsClient(credentials, awsConfig);

                    client.BeforeRequestEvent += ServiceClientBeforeRequestEvent;
                    client.ExceptionEvent += ServiceClientExceptionEvent;

                    return client;
                });
            }
            else
            {
                var preconfiguredClient = config.PreconfiguredServiceClient;
                if (preconfiguredClient is AmazonCloudWatchLogsClient preconfiguredClientImpl)
                {
                    preconfiguredClientImpl.BeforeRequestEvent += ServiceClientBeforeRequestEvent;
                    preconfiguredClientImpl.ExceptionEvent += ServiceClientExceptionEvent;
                }


                _client = new Lazy<IAmazonCloudWatchLogs>(() =>
                {
                    return preconfiguredClient;
                });
            }

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
            return DefaultAWSCredentialsIdentityResolver.GetCredentials();
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
        /// <remarks>
        /// On shutdown the pending queue and the current in-memory batch are fully drained and sent BEFORE the
        /// background monitor is cancelled, so events produced right up to shutdown are not silently dropped
        /// (issue #372). The drain is bounded by <see cref="AWSLoggerConfig.FlushTimeout"/> so Close() can never
        /// block indefinitely.
        /// <para>
        /// Note: this only protects an orderly shutdown. An abrupt process kill, or exceeding the ~2 second
        /// budget the .NET runtime allows <see cref="AppDomain.ProcessExit"/> handlers, can still lose messages
        /// that are only held in this in-memory buffer. Durable delivery of the very last events would require
        /// out-of-process buffering, which is outside the scope of this library.
        /// </para>
        /// </remarks>
        public void Close()
        {
            // Close can be invoked from DomainUnload, ProcessExit and an explicit Dispose; only run once.
            if (Interlocked.Exchange(ref _closeCalled, 1) != 0)
                return;

            try
            {
                // Order matters: publish the grace source before the volatile flag so the monitor sees a
                // non-null source once it observes _shutdownInitiated.
                _shutdownGraceSource = new CancellationTokenSource(_config.FlushTimeout);
                _shutdownInitiated = true;

                // Drain-and-send everything still buffered. During shutdown Flush() blocks until BOTH the
                // pending queue and the current batch are empty (bounded by FlushTimeout).
                Flush();
            }
            catch (Exception ex)
            {
                LogLibraryServiceError(ex);
            }
            finally
            {
                // Only now stop the monitor - the drain above has already sent the buffered events.
                _cancelStartSource.Cancel();

                // Flush() has returned, so the monitor is done using the grace source; dispose it here to
                // release its backing timer promptly instead of waiting for finalization. Interlocked.Exchange
                // makes disposal single-shot and safe against the finalizer.
                var graceSource = Interlocked.Exchange(ref _shutdownGraceSource, null);
                graceSource?.Dispose();

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
        
        private string _cachedServiceUrl;
        private string GetServiceUrl()
        {
            try
            {
                _client.Value.Config.Validate();
                if (_cachedServiceUrl == null)
                {
                    _cachedServiceUrl = _client.Value.DetermineServiceOperationEndpoint(new DescribeLogGroupsRequest
                    {
                        LogGroupNamePrefix = _config.LogGroup
                    }).URL ?? "Undetermined ServiceURL";
                }

                return _cachedServiceUrl;
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
            // Once the monitor has been cancelled at the end of shutdown there is nothing left to drain the
            // queue, so reject new messages rather than growing memory with events that can never be sent.
            // Messages produced earlier - including during the bounded shutdown drain - are still accepted.
            // The token source can also be disposed during teardown; treat that as "closed" and no-op safely.
            try
            {
                if (_cancelStartSource.IsCancellationRequested)
                {
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }

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
            if (_shutdownGraceSource != null)
            {
                _shutdownGraceSource.Dispose();
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
                        _client.Value.Dispose();
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
                        // During shutdown, send with the grace token so the monitor's own cancellation cannot
                        // abort the final delivery. Steady-state sends keep using the monitor token unchanged.
                        var graceSource = _shutdownGraceSource;
                        var sendToken = (_shutdownInitiated && graceSource != null)
                            ? graceSource.Token
                            : token;
                        await SendMessages(sendToken).ConfigureAwait(false);
                    }

                    if (executeFlush)
                    {
                        // Issue #372: when the flush is part of shutdown, a single drain pass is not enough -
                        // an event can be produced (or a batch can remain) after the pass above. Keep draining
                        // the pending queue and the current batch until BOTH are empty before signalling the
                        // flush as complete, so Flush()/Close() only return once everything buffered has been
                        // sent. Sends use the dedicated shutdown-grace token (not the monitor's own token) so
                        // that cancelling the monitor cannot abort the final PutLogEvents, and the grace token's
                        // bounded deadline (FlushTimeout) guarantees this loop always terminates.
                        if (_shutdownInitiated)
                        {
                            var graceSource = _shutdownGraceSource;
                            var graceToken = graceSource?.Token ?? token;
                            while (!graceToken.IsCancellationRequested &&
                                   (!_pendingMessageQueue.IsEmpty || !_repo.IsEmpty))
                            {
                                while (_pendingMessageQueue.TryDequeue(out var inputLogEvent))
                                {
                                    if (_repo.CurrentBatchMessageCount > 0 && _repo.IsSizeConstraintViolated(inputLogEvent.Message))
                                    {
                                        await SendMessages(graceToken).ConfigureAwait(false);
                                    }

                                    _repo.AddMessage(inputLogEvent);
                                }

                                if (!_repo.IsEmpty)
                                {
                                    await SendMessages(graceToken).ConfigureAwait(false);
                                }
                            }

                            // If the grace deadline (FlushTimeout) elapsed while events were still buffered, the
                            // loop exits without having sent everything. Surface it instead of silently reporting
                            // completion, so operators can tell a full drain apart from a timed-out one.
                            if (!_pendingMessageQueue.IsEmpty || !_repo.IsEmpty)
                            {
                                var serviceUrl = GetServiceUrl();
                                LogLibraryServiceError(new TimeoutException($"Shutdown flush timed out before all messages were sent - ServiceURL={serviceUrl}, StreamName={_currentStreamName}, PendingMessages={_pendingMessageQueue.Count}, CurrentBatch={_repo.CurrentBatchMessageCount}"), serviceUrl);
                            }
                        }

                        _flushCompletedEvent.Set();
                    }

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
                    _client.Value.Dispose();
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
                _repo._request.LogEvents.Sort((ev1, ev2) => 
                    ev1.Timestamp.GetValueOrDefault().CompareTo(ev2.Timestamp.GetValueOrDefault()));
                var response = await _client.Value.PutLogEventsAsync(_repo._request, token).ConfigureAwait(false);
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
                var logGroupResponse = await _client.Value.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = _config.LogGroup
                }, token).ConfigureAwait(false);
                if (!IsSuccessStatusCode(logGroupResponse))
                {
                    LogLibraryServiceError(new System.Net.WebException($"Lookup LogGroup {_config.LogGroup} returned status: {logGroupResponse.HttpStatusCode}"), serviceURL);
                }

                if (logGroupResponse.LogGroups?.FirstOrDefault(x => string.Equals(x.LogGroupName, _config.LogGroup, StringComparison.Ordinal)) == null)
                {
                    var createGroupResponse = await _client.Value.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = _config.LogGroup }, token).ConfigureAwait(false);
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

            try
            {
                var streamResponse = await _client.Value.CreateLogStreamAsync(new CreateLogStreamRequest
                {
                    LogGroupName = _config.LogGroup,
                    LogStreamName = currentStreamName
                }, token).ConfigureAwait(false);
                if (!IsSuccessStatusCode(streamResponse))
                {
                    LogLibraryServiceError(new System.Net.WebException($"Create LogStream {currentStreamName} for LogGroup {_config.LogGroup} returned status: {streamResponse.HttpStatusCode}"), serviceURL);
                }
            }
            catch (ResourceAlreadyExistsException) when (!string.IsNullOrEmpty(_config.LogStreamName))
            {
            }
            catch (Exception ex)
            {
                LogLibraryServiceError(new Exception($"Create LogStream {currentStreamName} for LogGroup {_config.LogGroup} returned error: {ex.Message}"), serviceURL);
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
                        var putPolicyResponse = await _client.Value.PutRetentionPolicyAsync(new PutRetentionPolicyRequest(logGroup, logGroupRetentionInDays), token).ConfigureAwait(false);
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
            public PutLogEventsRequest _request = new PutLogEventsRequest { LogEvents = new List<InputLogEvent>() };
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
            if (args != null && args.Request is Amazon.Runtime.Internal.IAmazonWebServiceRequest internalRequest && !internalRequest.UserAgentDetails.GetCustomUserAgentComponents().Contains(userAgentString))
            {
                internalRequest.UserAgentDetails.AddUserAgentComponent(userAgentString);
        }
        }

        void ServiceClientExceptionEvent(object sender, ExceptionEventArgs e)
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
        public static void LogLibraryError(Exception originalException, string LibraryLogFileName)
        {
            try
            {
                using (StreamWriter w = File.AppendText(LibraryLogFileName))
                {
                    w.WriteLine("Log Entry : ");
                    w.WriteLine("{0}", DateTime.Now.ToString());
                    w.WriteLine("  :");
                    w.WriteLine("  :{0}", originalException.ToString());
                    w.WriteLine("-------------------------------");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception caught when writing error log to file" + e.ToString());
                Console.WriteLine("Original Exception attempted to be written to the log file: " + originalException.ToString());
            }
        }
    }
}
