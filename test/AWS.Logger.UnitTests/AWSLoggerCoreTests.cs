using System;
using System.Threading;
using System.Threading.Tasks;

using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;

using AWS.Logger.Core;

using Xunit;

namespace AWS.Logger.UnitTests
{
    public class AWSLoggerCoreTests
    {
        [Fact]
        public async Task StartMonitor_CalledMultipleTimes_StartsOnlyOneMonitor()
        {
            var client = new TestCloudWatchLogsClient();

            var config = new AWSLoggerConfig
            {
                LogGroup = "test-start-monitor",
                DisableLogGroupCreation = true,
                PreconfiguredServiceClient = client
            };

            var logger = new AWSLoggerCore(config, "UnitTest");

            await client.WaitForCreateLogStreamAsync();

            logger.StartMonitor();
            logger.StartMonitor();
            logger.StartMonitor();

            Assert.Equal(1, client.CreateLogStreamCallCount);

            client.ReleaseCreateLogStream();

            logger.Close();
        }

        [Fact]
        public async Task StartMonitor_CalledConcurrently_StartsOnlyOneMonitor()
        {
            var client = new TestCloudWatchLogsClient();

            var config = new AWSLoggerConfig
            {
                LogGroup = "test-start-monitor-concurrent",
                DisableLogGroupCreation = true,
                PreconfiguredServiceClient = client
            };

            var logger = new AWSLoggerCore(config, "UnitTest");

            await client.WaitForCreateLogStreamAsync();

            var startTasks = new Task[10];

            for (int i = 0; i < startTasks.Length; i++)
            {
                startTasks[i] = Task.Run(() => logger.StartMonitor());
            }

            await Task.WhenAll(startTasks);

            Assert.Equal(1, client.CreateLogStreamCallCount);

            client.ReleaseCreateLogStream();

            logger.Close();
        }

        [Fact]
        public async Task Close_CancelsActiveMonitor()
        {
            var client = new TestCloudWatchLogsClient();

            var config = new AWSLoggerConfig
            {
                LogGroup = "test-start-monitor-close",
                DisableLogGroupCreation = true,
                PreconfiguredServiceClient = client
            };

            var logger = new AWSLoggerCore(config, "UnitTest");

            await client.WaitForCreateLogStreamAsync();

            client.ReleaseCreateLogStream();

            await Task.Delay(100);

            logger.Close();

            await client.WaitForDisposeAsync();

            Assert.Equal(1, client.DisposeCallCount);
        }

        private sealed class TestCloudWatchLogsClient : AmazonCloudWatchLogsClient
        {
            private readonly TaskCompletionSource<bool> _createLogStreamCalled =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> _releaseCreateLogStream =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> _disposed =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            private int _createLogStreamCallCount;
            private int _disposeCallCount;

            public int CreateLogStreamCallCount =>
                Volatile.Read(ref _createLogStreamCallCount);

            public int DisposeCallCount =>
                Volatile.Read(ref _disposeCallCount);

            public TestCloudWatchLogsClient()
                : base(new AmazonCloudWatchLogsConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.USWest2
                })
            {
            }

            public override async Task<CreateLogStreamResponse> CreateLogStreamAsync(
                CreateLogStreamRequest request,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _createLogStreamCallCount);

                _createLogStreamCalled.TrySetResult(true);

                await _releaseCreateLogStream.Task.WaitAsync(cancellationToken);

                return new CreateLogStreamResponse
                {
                    HttpStatusCode = System.Net.HttpStatusCode.OK
                };
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Interlocked.Increment(ref _disposeCallCount);

                    _disposed.TrySetResult(true);
                }

                base.Dispose(disposing);
            }

            public async Task WaitForCreateLogStreamAsync()
            {
                await _createLogStreamCalled.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));
            }

            public void ReleaseCreateLogStream()
            {
                _releaseCreateLogStream.TrySetResult(true);
            }

            public async Task WaitForDisposeAsync()
            {
                await _disposed.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));
            }
        }
    }
}