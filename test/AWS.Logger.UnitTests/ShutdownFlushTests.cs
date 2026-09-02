using System;
using System.Linq;
using Amazon.CloudWatchLogs;
using AWS.Logger.Core;
using AWS.Logger.UnitTests.Fakes;
using Xunit;

namespace AWS.Logger.UnitTests
{
    /// <summary>
    /// Regression tests for https://github.com/aws/aws-logging-dotnet/issues/372
    ///
    /// AWSLoggerCore.Close() calls Flush() then cancels the background monitor. On cancellation the monitor
    /// took the OperationCanceledException path and returned, abandoning anything still queued or sitting in a
    /// not-yet-sent batch. The result was that log events produced near shutdown were silently dropped.
    /// </summary>
    public class ShutdownFlushTests
    {
        private static AWSLoggerConfig CreateConfig(IAmazonCloudWatchLogs client)
        {
            return new AWSLoggerConfig("issue-372-test-group")
            {
                PreconfiguredServiceClient = client,
                // The group "exists" via the fake; skip describe/create entirely.
                DisableLogGroupCreation = true,
                // Fixed stream name so no stream-name generation is involved.
                LogStreamName = "issue-372-test-stream",
                // Make the ONLY thing that can trigger a send be an explicit flush:
                //  - huge push interval => the time-based push never fires during the test
                //  - large queue capacity => the count-based push never fires for our small burst
                BatchPushInterval = TimeSpan.FromMinutes(30),
                MaxQueuedMessages = 100000,
                // Bound the test so a regression in the drain logic fails fast instead of hanging.
                FlushTimeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Reproduces the tail-loss race: a message that is produced *during* the shutdown flush (modeling a
        /// concurrent logging call while the process is winding down) must still be delivered by Close().
        /// Before the fix, this message is abandoned when the monitor is cancelled and the test fails with
        /// 5 delivered instead of 6.
        /// </summary>
        [Fact]
        public void Close_DeliversMessageProducedDuringShutdownFlush()
        {
            var fake = new InMemoryCloudWatchLogsClient();
            var core = new AWSLoggerCore(CreateConfig(fake), "unit");

            // When the flush drives the first PutLogEvents, simulate one more log line arriving concurrently
            // (the exact shutdown race from #372).
            fake.OnFirstPut = () => core.AddMessage("late-tail-event");

            const int burst = 5;
            for (var i = 0; i < burst; i++)
            {
                core.AddMessage("event-" + i);
            }

            core.Close();

            var received = fake.ReceivedMessages;
            Assert.Contains("late-tail-event", received);
            for (var i = 0; i < burst; i++)
            {
                Assert.Contains("event-" + i, received);
            }
            Assert.Equal(burst + 1, received.Count);
        }

        /// <summary>
        /// A plain burst with no concurrent producer must be fully delivered by Close(). This guards the
        /// straightforward "flush everything that was enqueued before Close" contract.
        /// </summary>
        [Fact]
        public void Close_DeliversEntireBurst()
        {
            var fake = new InMemoryCloudWatchLogsClient();
            var core = new AWSLoggerCore(CreateConfig(fake), "unit");

            const int burst = 50;
            for (var i = 0; i < burst; i++)
            {
                core.AddMessage("event-" + i);
            }

            core.Close();

            var received = fake.ReceivedMessages;
            Assert.Equal(burst, received.Count);
            Assert.Equal(
                Enumerable.Range(0, burst).Select(i => "event-" + i).OrderBy(x => x),
                received.OrderBy(x => x));
        }
    }
}
