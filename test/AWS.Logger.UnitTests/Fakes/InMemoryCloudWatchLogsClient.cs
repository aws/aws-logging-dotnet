using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;

namespace AWS.Logger.UnitTests.Fakes
{
    /// <summary>
    /// An in-memory <see cref="IAmazonCloudWatchLogs"/> used by the shutdown/flush tests. It captures every
    /// message handed to <see cref="PutLogEventsAsync(PutLogEventsRequest, CancellationToken)"/> without touching
    /// the network, so tests are deterministic and require no AWS credentials.
    ///
    /// <para>
    /// We subclass <see cref="AmazonCloudWatchLogsClient"/> (rather than implementing the very large
    /// <see cref="IAmazonCloudWatchLogs"/> interface) and override only the four operations that
    /// <c>AWSLoggerCore</c> actually invokes. The base client is constructed with static credentials and an
    /// explicit <see cref="AmazonCloudWatchLogsConfig.ServiceURL"/> so that no region resolution, credential
    /// lookup, or HTTP call ever happens at construction time.
    /// </para>
    /// </summary>
    public class InMemoryCloudWatchLogsClient : AmazonCloudWatchLogsClient
    {
        private readonly object _sync = new object();
        private readonly List<string> _receivedMessages = new List<string>();
        private int _putCallCount;
        private int _onFirstPutFired;

        public InMemoryCloudWatchLogsClient()
            : base(
                new BasicAWSCredentials("fake-access-key", "fake-secret-key"),
                new AmazonCloudWatchLogsConfig
                {
                    // Explicit endpoint => no region/credential resolution or network at construction.
                    ServiceURL = "http://localhost:59999",
                    MaxErrorRetry = 0
                })
        {
        }

        /// <summary>
        /// Optional hook invoked exactly once, synchronously, during the first
        /// <see cref="PutLogEventsAsync(PutLogEventsRequest, CancellationToken)"/> call (before it returns).
        /// Used to model a producer that logs one more line concurrently with a shutdown flush (issue #372).
        /// </summary>
        public Action OnFirstPut { get; set; }

        /// <summary>Number of times PutLogEvents was invoked.</summary>
        public int PutCallCount => Volatile.Read(ref _putCallCount);

        /// <summary>All messages received across every PutLogEvents batch, in arrival order.</summary>
        public IReadOnlyList<string> ReceivedMessages
        {
            get
            {
                lock (_sync)
                {
                    return new List<string>(_receivedMessages);
                }
            }
        }

        public int ReceivedMessageCount
        {
            get
            {
                lock (_sync)
                {
                    return _receivedMessages.Count;
                }
            }
        }

        public override Task<PutLogEventsResponse> PutLogEventsAsync(PutLogEventsRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Copy the messages NOW: AWSLoggerCore calls LogEventBatch.Reset() (which clears this same
            // List<InputLogEvent> instance) immediately after this task completes, so holding the reference
            // would observe a cleared list.
            lock (_sync)
            {
                if (request?.LogEvents != null)
                {
                    foreach (var ev in request.LogEvents)
                    {
                        _receivedMessages.Add(ev.Message);
                    }
                }
            }

            Interlocked.Increment(ref _putCallCount);

            if (OnFirstPut != null && Interlocked.Exchange(ref _onFirstPutFired, 1) == 0)
            {
                OnFirstPut();
            }

            return Task.FromResult(new PutLogEventsResponse { HttpStatusCode = HttpStatusCode.OK });
        }

        public override Task<CreateLogStreamResponse> CreateLogStreamAsync(CreateLogStreamRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CreateLogStreamResponse { HttpStatusCode = HttpStatusCode.OK });
        }

        public override Task<DescribeLogGroupsResponse> DescribeLogGroupsAsync(DescribeLogGroupsRequest request, CancellationToken cancellationToken = default)
        {
            // Report the requested group as already existing so the core never needs to create it.
            return Task.FromResult(new DescribeLogGroupsResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                LogGroups = new List<LogGroup>
                {
                    new LogGroup { LogGroupName = request?.LogGroupNamePrefix }
                }
            });
        }

        public override Task<CreateLogGroupResponse> CreateLogGroupAsync(CreateLogGroupRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CreateLogGroupResponse { HttpStatusCode = HttpStatusCode.OK });
        }
    }
}
