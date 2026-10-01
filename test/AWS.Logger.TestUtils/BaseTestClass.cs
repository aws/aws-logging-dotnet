using Amazon.CloudWatchLogs.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AWS.Logger.TestUtils
{
    public abstract class BaseTestClass : IClassFixture<TestFixture>
    {
        public const int SIMPLELOGTEST_COUNT = 10;
        public const int MULTITHREADTEST_COUNT = 200;
        public const int THREAD_WAITTIME = 25;
        public const int THREAD_COUNT = 2;
        public const string LASTMESSAGE = "LASTMESSAGE";
        public const string CUSTOMSTREAMSUFFIX = "Custom";
        public const string CUSTOMSTREAMPREFIX = "CustomPrefix";
        public TestFixture _testFixture;

        public BaseTestClass(TestFixture testFixture)
        {
            _testFixture = testFixture;
        }

        protected async Task<bool> NotifyLoggingCompleted(string logGroupName, string filterPattern)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (timer.Elapsed < TimeSpan.FromSeconds(THREAD_WAITTIME))
            {
                await Task.Delay(500);
                if (await FilterPatternExists(logGroupName, filterPattern))
                {
                    break;
                }
            }
            return await FilterPatternExists(logGroupName, filterPattern);
        }

        protected async Task<bool> FilterPatternExists(string logGroupName, string filterPattern)
        {
            DescribeLogStreamsResponse describeLogstreamsResponse;
            try
            {
                describeLogstreamsResponse = await _testFixture.Client.
                    DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                    {
                        Descending = true,
                        LogGroupName = logGroupName,
                        OrderBy = "LastEventTime"
                    });
            }
            catch (Exception) {
                return false;
            }
            
            if (describeLogstreamsResponse.LogStreams.Count > 0)
            {
                FilterLogEventsResponse filterLogEventsResponse = await _testFixture.Client.
                    FilterLogEventsAsync(new FilterLogEventsRequest
                    {
                        FilterPattern = filterPattern,
                        LogGroupName = logGroupName,
                        LogStreamNames = new List<string> { describeLogstreamsResponse.LogStreams[0].LogStreamName }
                    });

                return filterLogEventsResponse.Events.Count > 0;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Counts the events in a log stream, polling until at least <paramref name="expectedCount"/> events are
        /// visible or <see cref="THREAD_WAITTIME"/> elapses. GetLogEvents is eventually consistent and is indexed
        /// separately from FilterLogEvents, so seeing LASTMESSAGE via a filter does not mean every event is readable yet.
        /// </summary>
        protected async Task<int> GetLogEventCountWithRetries(string logGroupName, string logStreamName, int expectedCount)
        {
            var timer = Stopwatch.StartNew();
            while (true)
            {
                var count = 0;
                string nextToken = null;
                while (true)
                {
                    var getLogEventsResponse = await _testFixture.Client.GetLogEventsAsync(new GetLogEventsRequest
                    {
                        LogGroupName = logGroupName,
                        LogStreamName = logStreamName,
                        StartFromHead = true,
                        NextToken = nextToken
                    });
                    count += getLogEventsResponse.Events?.Count ?? 0;

                    // GetLogEvents signals the end of the stream by returning the same token that was passed in.
                    if (getLogEventsResponse.NextForwardToken == null || getLogEventsResponse.NextForwardToken == nextToken)
                    {
                        break;
                    }
                    nextToken = getLogEventsResponse.NextForwardToken;
                }

                if (count >= expectedCount || timer.Elapsed >= TimeSpan.FromSeconds(THREAD_WAITTIME))
                {
                    return count;
                }
                await Task.Delay(1000);
            }
        }

        protected abstract void LogMessages(int count);

        protected async Task SimpleLoggingTest(string logGroupName)
        {
            LogMessages(SIMPLELOGTEST_COUNT);
            var eventCount = 0;
            if (await NotifyLoggingCompleted(logGroupName, "LASTMESSAGE"))
            {
                var describeLogstreamsResponse = await _testFixture.Client.DescribeLogStreamsAsync(
                    new DescribeLogStreamsRequest
                    {
                        Descending = true,
                        LogGroupName = logGroupName,
                        OrderBy = "LastEventTime"
                    });
                var logStream = describeLogstreamsResponse.LogStreams.First();
                eventCount = await GetLogEventCountWithRetries(logGroupName, logStream.LogStreamName, SIMPLELOGTEST_COUNT);

                var customStreamSuffix = logStream.LogStreamName.Split('-').Last().Trim();
                Assert.Equal(CUSTOMSTREAMSUFFIX, customStreamSuffix);
                var customStreamPrefix = logStream.LogStreamName.Split('-').First().Trim();
                Assert.Equal(CUSTOMSTREAMPREFIX, customStreamPrefix);
            }
            Assert.Equal(SIMPLELOGTEST_COUNT, eventCount);


            _testFixture.LogGroupNameList.Add(logGroupName);
        }
        
        /// <summary>
        /// Publishes logs from multiple threads to the specified log group, and asserts
        /// that the expected number of events are present
        /// </summary>
        /// <param name="logGroupName">Log group to publish events to</param>
        /// <param name="expectedLogStreamName">
        /// Optional name of the stream within the group to assert against. 
        /// May be used when overriding the stream name as opposed to using the 
        /// generated name based on the prefix and suffix.
        /// </param>
        protected async Task MultiThreadTestGroup(string logGroupName, string expectedLogStreamName = "")
        {
            // This allows the fixture to delete the group at the end,
            // whether or not the test passes
            _testFixture.LogGroupNameList.Add(logGroupName);

            var tasks = new List<Task>();
            var streamNames = new List<string>();
            var count = MULTITHREADTEST_COUNT;
            var totalCount = 0;
            for (int i = 0; i < THREAD_COUNT; i++)
            {
                tasks.Add(Task.Run(() => LogMessages(count)));
                totalCount = totalCount + count;
            }

            await Task.WhenAll(tasks.ToArray());
            int testCount = -1;
            if (await NotifyLoggingCompleted(logGroupName, "LASTMESSAGE"))
            {
                DescribeLogStreamsResponse describeLogstreamsResponse =
                await _testFixture.Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                {
                    Descending = true,
                    LogGroupName = logGroupName,
                    OrderBy = "LastEventTime"
                });

                if (describeLogstreamsResponse.LogStreams.Count > 0)
                {
                    // If the caller provided an expected log stream name (when testing the explicit setting),
                    // assert that all producers should share the same stream with the configured name.
                    if (!string.IsNullOrEmpty(expectedLogStreamName))
                    {
                        Assert.Single(describeLogstreamsResponse.LogStreams);
                        Assert.Equal(expectedLogStreamName, describeLogstreamsResponse.LogStreams[0].LogStreamName);
                    }
                    testCount = await GetLogEventCountWithRetries(logGroupName, describeLogstreamsResponse.LogStreams[0].LogStreamName, totalCount);
                }
            }

            Assert.Equal(totalCount, testCount);
        }

        protected async Task MultiThreadBufferFullTestGroup(string logGroupName)
        {
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            var count = MULTITHREADTEST_COUNT;
            var totalCount = 0;
            for (int i = 0; i < THREAD_COUNT; i++)
            {
                tasks.Add(Task.Run(() => LogMessages(count)));
                totalCount = totalCount + count;
            }
            await Task.WhenAll(tasks.ToArray());
            Assert.True(await NotifyLoggingCompleted(logGroupName, "maximum"));

            _testFixture.LogGroupNameList.Add(logGroupName);
        }


    }
}
