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
        public const int MULTITHREADTEST_COUNT = 20;
        public const int THREAD_WAITTIME = 10;
        public const int THREAD_COUNT = 5;        
        public const string LASTMESSAGE = "LASTMESSAGE";
        public const string CUSTOMSTREAMSUFFIX = "Custom";
        public TestFixture _testFixture;
        public AmazonCloudWatchLogsClient Client;

        public BaseTestClass(TestFixture testFixture)
        {
            _testFixture = testFixture;
            Client = new AmazonCloudWatchLogsClient(Amazon.RegionEndpoint.USWest2);
        }

        public List<FilteredLogEvent> FilterLogStream(string logGroupName, string message)
        {
            var result = Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;

            var streamName = result.LogStreams[0].LogStreamName;

            var logFilterResponse = Client.FilterLogEventsAsync(new FilterLogEventsRequest
            {
                LogGroupName = logGroupName,
                LogStreamNames = new List<string> { streamName },
                FilterPattern = message
            }).Result;

            return logFilterResponse.Events;
        }

        public bool NotifyLoggingCompleted(string logGroupName, string filterPattern)
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
        public bool FilterPatternExists(string logGroupName, string filterPattern)
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

        public abstract void LogMessages(int count);

        public void SimpleLoggingTest(string logGroupName)
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
            }
            Assert.Equal(SIMPLELOGTEST_COUNT, getLogEventsResponse.Events.Count());


            _testFixture.LogGroupNameList.Add(logGroupName);
        }

        public void MultiThreadTest(string logGroupName)
        {
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            var count = MULTITHREADTEST_COUNT;

            for (int i = 0; i < THREAD_COUNT; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => LogMessages(count)));
            }

            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(THREAD_WAITTIME));

            Thread.Sleep(3000);

            var lastMessageEvents = FilterLogStream(logGroupName, LASTMESSAGE);

            Assert.Equal(THREAD_COUNT, lastMessageEvents.Count);

            _testFixture.LogGroupNameList.Add(logGroupName);
        }

        public void MultiThreadBufferFullTest(string logGroupName, int waitMilliSec)
        {
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            var count = MULTITHREADTEST_COUNT;
            for (int i = 0; i < THREAD_COUNT; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => LogMessages(count)));
            }

            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(THREAD_WAITTIME));

            Thread.Sleep(waitMilliSec);

            var lastMessageEvents = FilterLogStream(logGroupName, LASTMESSAGE);

            Assert.Equal(THREAD_COUNT, lastMessageEvents.Count);

            _testFixture.LogGroupNameList.Add(logGroupName);
        }
    }
}
