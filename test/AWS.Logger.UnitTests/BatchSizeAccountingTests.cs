using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Amazon.CloudWatchLogs.Model;

using AWS.Logger.Core;
using AWS.Logger.UnitTests.Fakes;

using Xunit;

namespace AWS.Logger.UnitTests
{
    /// <summary>
    /// Tests for the PutLogEvents batch-size accounting in <see cref="AWSLoggerCore"/>.
    ///
    /// Regression coverage for https://github.com/aws/aws-logging-dotnet/issues/372
    /// ("aggravating factor #1"): batch size used to be estimated with
    /// <c>Encoding.Unicode.GetMaxByteCount(len)</c> == (len+1)*2, roughly double the real UTF-8
    /// size of ASCII/JSON payloads, so batches filled at ~half of real capacity and a burst was
    /// split into ~2x as many PutLogEvents calls as necessary. The fix sizes batches by real
    /// UTF-8 bytes plus the 26-byte-per-event service overhead, while enforcing the hard service
    /// limits (1,048,576 bytes and 10,000 events per batch).
    /// </summary>
    public class BatchSizeAccountingTests
    {
        // CloudWatch Logs PutLogEvents hard limits.
        private const int MaxBatchSizeInBytes = 1_048_576;
        private const int EventSizeOverheadInBytes = 26;
        private const int MaxEventsInBatch = 10_000;

        private static int RealBatchSizeInBytes(IReadOnlyList<InputLogEvent> batch)
        {
            return batch.Sum(e => Encoding.UTF8.GetByteCount(e.Message) + EventSizeOverheadInBytes);
        }

        /// <summary>
        /// Simulates the number of batches the monitor loop's "send the current batch before
        /// adding the next event that would overflow it" logic produces for a fixed per-event
        /// cost. Mirrors <c>IsSizeConstraintViolated</c> (send when the prospective total exceeds
        /// the cap, or when the event count reaches the per-batch cap).
        /// </summary>
        private static int SimulateBatchCount(int eventCount, int perEventCost, int cap, int maxEvents)
        {
            var batches = 1;
            var runningTotal = 0;
            var eventsInBatch = 0;

            for (var i = 0; i < eventCount; i++)
            {
                if (eventsInBatch > 0 && (eventsInBatch >= maxEvents || runningTotal + perEventCost > cap))
                {
                    batches++;
                    runningTotal = 0;
                    eventsInBatch = 0;
                }

                runningTotal += perEventCost;
                eventsInBatch++;
            }

            return batches;
        }

        private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                    return;

                await Task.Delay(50);
            }
        }

        [Fact]
        public async Task AsciiBurst_NoBatchExceedsRealLimit_AndFewerBatchesThanUnicodeOverestimate()
        {
            const int messageLength = 1000;
            const int messageCount = 5000;

            var fake = new BatchCapturingCloudWatchLogsClient();
            var core = new AWSLoggerCore(new AWSLoggerConfig
            {
                LogGroup = "/aws/logging-tests/batch-size-ascii",
                PreconfiguredServiceClient = fake,
                BatchSizeInBytes = MaxBatchSizeInBytes,
                // Large enough that neither the in-memory drop guard nor the count-based
                // ShouldSendRequest trigger fires; batching is driven purely by byte size.
                MaxQueuedMessages = 2_000_000,
                // Effectively disable time-based pushes so the burst splits only on size.
                BatchPushInterval = TimeSpan.FromSeconds(1000),
            }, "aws-logger-unittests#0.0.0.0");

            var message = new string('a', messageLength);
            for (var i = 0; i < messageCount; i++)
            {
                core.AddMessage(message);
            }

            core.Flush();
            await WaitForAsync(() => fake.TotalEventCount == messageCount, TimeSpan.FromSeconds(30));

            var batches = fake.Batches;

            // No events were dropped.
            Assert.Equal(messageCount, fake.TotalEventCount);
            Assert.NotEmpty(batches);

            // (a) Every batch stays within the real service limits.
            foreach (var batch in batches)
            {
                Assert.True(batch.Count <= MaxEventsInBatch,
                    $"Batch had {batch.Count} events, exceeding the {MaxEventsInBatch} cap.");

                var realSize = RealBatchSizeInBytes(batch);
                Assert.True(realSize <= MaxBatchSizeInBytes,
                    $"Batch real size {realSize} exceeded the {MaxBatchSizeInBytes} byte limit.");
            }

            // (b) Fewer batches than the old Unicode-overestimate accounting would have produced.
            var newPerEventCost = messageLength + EventSizeOverheadInBytes;                 // real UTF-8 + overhead
            var oldPerEventCost = (messageLength + 1) * 2;                                   // Encoding.Unicode.GetMaxByteCount
            var expectedNewBatches = SimulateBatchCount(messageCount, newPerEventCost, MaxBatchSizeInBytes, MaxEventsInBatch);
            var oldBatches = SimulateBatchCount(messageCount, oldPerEventCost, MaxBatchSizeInBytes, MaxEventsInBatch);

            Assert.Equal(expectedNewBatches, batches.Count);
            Assert.True(batches.Count < oldBatches,
                $"Expected fewer batches than the old behavior: got {batches.Count}, old would be {oldBatches}.");
        }

        [Fact]
        public async Task Burst_EnforcesTenThousandEventsPerBatchCap()
        {
            const int messageCount = 12_000;

            var fake = new BatchCapturingCloudWatchLogsClient();
            var core = new AWSLoggerCore(new AWSLoggerConfig
            {
                LogGroup = "/aws/logging-tests/batch-size-count-cap",
                PreconfiguredServiceClient = fake,
                // Max byte size so small messages never trigger the byte-size limit; only the
                // 10,000-event count cap can split these batches.
                BatchSizeInBytes = MaxBatchSizeInBytes,
                MaxQueuedMessages = 2_000_000,
                BatchPushInterval = TimeSpan.FromSeconds(1000),
            }, "aws-logger-unittests#0.0.0.0");

            for (var i = 0; i < messageCount; i++)
            {
                core.AddMessage("x");
            }

            core.Flush();
            await WaitForAsync(() => fake.TotalEventCount == messageCount, TimeSpan.FromSeconds(30));

            var batches = fake.Batches;

            Assert.Equal(messageCount, fake.TotalEventCount);

            foreach (var batch in batches)
            {
                Assert.True(batch.Count <= MaxEventsInBatch,
                    $"Batch had {batch.Count} events, exceeding the {MaxEventsInBatch} cap.");
            }

            // 12,000 tiny events must span more than one batch, and the cap must actually bite.
            Assert.True(batches.Count >= 2, $"Expected at least 2 batches, got {batches.Count}.");
            Assert.Contains(batches, b => b.Count == MaxEventsInBatch);
        }
    }
}
