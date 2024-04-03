using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
        public AmazonCloudWatchLogsClient Client;

        public BaseTestClass(TestFixture testFixture)
        {
            _testFixture = testFixture;
            Client = new AmazonCloudWatchLogsClient(Amazon.RegionEndpoint.USWest2);
        }

        protected bool NotifyLoggingCompleted(string logGroupName, string filterPattern)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (timer.Elapsed < TimeSpan.FromSeconds(THREAD_WAITTIME))
            {
                Thread.Sleep(500);
                if (FilterPatternExists(logGroupName, filterPattern))
                {
                    break;
                }
            }
            return FilterPatternExists(logGroupName, filterPattern);
        }

        protected bool FilterPatternExists(string logGroupName, string filterPattern)
        {
            DescribeLogStreamsResponse describeLogstreamsResponse;
            try
            {
                describeLogstreamsResponse = Client.
                    DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                    {
                        Descending = true,
                        LogGroupName = logGroupName,
                        OrderBy = "LastEventTime"
                    }).Result;
            }
            catch (Exception) {
                return false;
            }
            
            if (describeLogstreamsResponse.LogStreams.Count > 0)
            {
                List<string> logStreamNames = new List<string>();
                logStreamNames.Add(describeLogstreamsResponse.LogStreams[0].LogStreamName);
                FilterLogEventsResponse filterLogEventsResponse = Client.
                    FilterLogEventsAsync(new FilterLogEventsRequest
                    {
                        FilterPattern = filterPattern,
                        LogGroupName = logGroupName,
                        LogStreamNames = logStreamNames
                    }).Result;

                return filterLogEventsResponse.Events.Count > 0;
            }
            else
            {
                return false;
            }
        }

        protected abstract void LogMessages(int count);

        protected void SimpleLoggingTest(string logGroupName)
        {
            LogMessages(SIMPLELOGTEST_COUNT);
            GetLogEventsResponse getLogEventsResponse = new GetLogEventsResponse();
            if (NotifyLoggingCompleted(logGroupName, "LASTMESSAGE"))
            {
                DescribeLogStreamsResponse describeLogstreamsResponse =
                                                        Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                                                        {
                                                            Descending = true,
                                                            LogGroupName = logGroupName,
                                                            OrderBy = "LastEventTime"
                                                        }).Result;

                
                getLogEventsResponse = Client.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
                }).Result;

                var customStreamSuffix = describeLogstreamsResponse.LogStreams[0].LogStreamName.Split('-').Last().Trim();
                Assert.Equal(CUSTOMSTREAMSUFFIX, customStreamSuffix);
                var customStreamPrefix = describeLogstreamsResponse.LogStreams[0].LogStreamName.Split('-').First().Trim();
                Assert.Equal(CUSTOMSTREAMPREFIX, customStreamPrefix);
            }
            Assert.Equal(SIMPLELOGTEST_COUNT, getLogEventsResponse.Events.Count());


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
        protected void MultiThreadTestGroup(string logGroupName, string expectedLogStreamName = "")
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
                tasks.Add(Task.Factory.StartNew(() => LogMessages(count)));
                totalCount = totalCount + count;
            }


            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(THREAD_WAITTIME));
            int testCount = -1;
            if (NotifyLoggingCompleted(logGroupName, "LASTMESSAGE"))
            {
                DescribeLogStreamsResponse describeLogstreamsResponse =
                Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                {
                    Descending = true,
                    LogGroupName = logGroupName,
                    OrderBy = "LastEventTime"
                }).Result;

                if (describeLogstreamsResponse.LogStreams.Count > 0)
                {
                    // If the caller provided an expected log stream name (when testing the explicit setting),
                    // assert that all producers should share the same stream with the configured name.
                    if (!string.IsNullOrEmpty(expectedLogStreamName))
                    {
                        Assert.Single(describeLogstreamsResponse.LogStreams);
                        Assert.Equal(expectedLogStreamName, describeLogstreamsResponse.LogStreams[0].LogStreamName);
                    }
                    testCount = 0;
                    foreach (var logStream in describeLogstreamsResponse.LogStreams)
                    {
                        GetLogEventsResponse getLogEventsResponse =
                            Client.GetLogEventsAsync(new GetLogEventsRequest
                            {
                                LogGroupName = logGroupName,
                                LogStreamName = logStream.LogStreamName
                            }).Result;

                        if (getLogEventsResponse != null)
                        {
                            testCount += getLogEventsResponse.Events.Count();
                        }
                    }
                }
            }

            Assert.Equal(totalCount, testCount);
        }

        protected void MultiThreadBufferFullTestGroup(string logGroupName)
        {
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            var count = MULTITHREADTEST_COUNT;
            var totalCount = 0;
            for (int i = 0; i < THREAD_COUNT; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => LogMessages(count)));
                totalCount = totalCount + count;
            }
            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(THREAD_WAITTIME));
            Assert.True(NotifyLoggingCompleted(logGroupName, "maximum"));

            _testFixture.LogGroupNameList.Add(logGroupName);
        }


    }
}
