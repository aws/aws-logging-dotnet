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

            logger.StartMonitor();
            logger.StartMonitor();
            logger.StartMonitor();

            await client.WaitForCreateLogStreamAsync();

            Assert.Equal(1, client.CreateLogStreamCallCount);

            logger.Close();
        }

        private sealed class TestCloudWatchLogsClient : AmazonCloudWatchLogsClient
        {
            private readonly TaskCompletionSource<bool> _createLogStreamCalled =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            public int DisposeCallCount { get; private set; }
            public int CreateLogStreamCallCount { get; private set; }
            private readonly TaskCompletionSource<bool> _disposed =
    new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);
            public TestCloudWatchLogsClient()
                : base(new AmazonCloudWatchLogsConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.USWest2
                })
            {
            }

            public override Task<CreateLogStreamResponse> CreateLogStreamAsync(
                CreateLogStreamRequest request,
                CancellationToken cancellationToken = default)
            {
                CreateLogStreamCallCount++;
                _createLogStreamCalled.TrySetResult(true);

                return Task.FromResult(new CreateLogStreamResponse
                {
                    HttpStatusCode = System.Net.HttpStatusCode.OK
                });
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    DisposeCallCount++;
                    _disposed.TrySetResult(true);
                }

                base.Dispose(disposing);
            }
            public async Task WaitForDisposeAsync()
            {
                await _disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            public async Task WaitForCreateLogStreamAsync()
            {
                await _createLogStreamCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
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

            var startTasks = new Task[10];

            for (int i = 0; i < startTasks.Length; i++)
            {
                startTasks[i] = Task.Run(() => logger.StartMonitor());
            }

            await Task.WhenAll(startTasks);

            await client.WaitForCreateLogStreamAsync();

            Assert.Equal(1, client.CreateLogStreamCallCount);

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

            logger.StartMonitor();

            await client.WaitForCreateLogStreamAsync();

            logger.Close();

            await client.WaitForDisposeAsync();

            Assert.Equal(1, client.DisposeCallCount);
        }
    }
}