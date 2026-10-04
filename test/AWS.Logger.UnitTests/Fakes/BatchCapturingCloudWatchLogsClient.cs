using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;

namespace AWS.Logger.UnitTests.Fakes
{
    /// <summary>
    /// An in-memory <see cref="IAmazonCloudWatchLogs"/> that captures each PutLogEvents batch
    /// separately so batch-size accounting can be asserted, without touching the network or
    /// requiring AWS credentials.
    ///
    /// <para>
    /// We subclass <see cref="AmazonCloudWatchLogsClient"/> (rather than implementing the very
    /// large <see cref="IAmazonCloudWatchLogs"/> interface) and override only the operations
    /// <c>AWSLoggerCore</c> actually invokes. The base client is constructed with static
    /// credentials and an explicit endpoint so no region resolution, credential lookup, or HTTP
    /// call happens at construction time.
    /// </para>
    /// </summary>
    public class BatchCapturingCloudWatchLogsClient : AmazonCloudWatchLogsClient
    {
        private readonly object _sync = new object();
        private readonly List<List<InputLogEvent>> _batches = new List<List<InputLogEvent>>();

        public BatchCapturingCloudWatchLogsClient()
            : base(
                new BasicAWSCredentials("fake-access-key", "fake-secret-key"),
                new AmazonCloudWatchLogsConfig
                {
                    // Explicit endpoint => no region/credential resolution or network at construction.
                    ServiceURL = "http://localhost:59998",
                    MaxErrorRetry = 0
                })
        {
        }

        /// <summary>Every captured batch, in the order PutLogEvents was called.</summary>
        public IReadOnlyList<IReadOnlyList<InputLogEvent>> Batches
        {
            get
            {
                lock (_sync)
                {
                    return _batches.Select(b => (IReadOnlyList<InputLogEvent>)b.ToList()).ToList();
                }
            }
        }

        /// <summary>Number of PutLogEvents calls (i.e. number of batches sent).</summary>
        public int PutCallCount
        {
            get
            {
                lock (_sync)
                {
                    return _batches.Count;
                }
            }
        }

        /// <summary>Total events received across every batch.</summary>
        public int TotalEventCount
        {
            get
            {
                lock (_sync)
                {
                    return _batches.Sum(b => b.Count);
                }
            }
        }

        public override Task<PutLogEventsResponse> PutLogEventsAsync(PutLogEventsRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Deep copy the events NOW: AWSLoggerCore calls LogEventBatch.Reset() (which clears this
            // same List<InputLogEvent> instance) immediately after this task completes, so holding
            // the reference would observe a cleared list.
            lock (_sync)
            {
                var copied = request?.LogEvents == null
                    ? new List<InputLogEvent>()
                    : request.LogEvents
                        .Select(e => new InputLogEvent { Message = e.Message, Timestamp = e.Timestamp })
                        .ToList();
                _batches.Add(copied);
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
