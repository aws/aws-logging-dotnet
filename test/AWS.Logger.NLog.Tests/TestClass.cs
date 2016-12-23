using NLog;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using NLog.Config;
using AWS.Logger.TestUtils;

namespace AWS.Logger.NLog.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class NLogTestClass: IClassFixture<TestFixture>
    {
        TestFixture testFixture;
        public NLogTestClass(TestFixture testFixture)
        {
            this.testFixture = testFixture;
        }
        #region Test Cases  
        [Fact]
        public void Nlog()
        {
            try
            {
                LogManager.Configuration = new XmlLoggingConfiguration("Regular.config");
            }
            catch (FileNotFoundException)
            {
                LogManager.Configuration = new XmlLoggingConfiguration("./test/AWS.Logger.NLog.Tests/Regular.config");
            }
            global::NLog.Logger logger = LogManager.GetLogger("loggerRegular");
            for (int i = 0; i < 10; i++)
            {
                logger.Debug(string.Format("Test logging message {0} NLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }

            string region = "us-west-2";
            string logGroupName = "AWSNLogGroup";

            Thread.Sleep(10000);
            testFixture.client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));

            DescribeLogStreamsResponse describeLogstreamsResponse = testFixture.client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;

            GetLogEventsResponse getLogEventsResponse = testFixture.client.GetLogEventsAsync(new GetLogEventsRequest
            {
                LogGroupName = logGroupName,
                LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
            }).Result;

            Assert.Equal(10, getLogEventsResponse.Events.Count());

            testFixture.logGroupNameList.Add(logGroupName);
            testFixture.regionList.Add(region);
        }

        [Fact]
        public void MultiThreadTest()
        {
            global::NLog.Logger logger;
            try
            {
                LogManager.Configuration = new XmlLoggingConfiguration("Regular.config");
            }
            catch (FileNotFoundException)
            {
                LogManager.Configuration = new XmlLoggingConfiguration("./test/AWS.Logger.NLog.Tests/Regular.config");
            }
            logger = LogManager.GetLogger("loggerMultiThread");

            var tasks = new List<Task>();
            var streamNames = new List<string>();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => NLogThread(count, logger)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray());

            Thread.Sleep(10000);
            string region = "us-west-2";
            string logGroupName = "AWSNLogGroupMultiThreadTest";


            testFixture.client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));


            DescribeLogStreamsResponse describeLogstreamsResponse = testFixture.client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;


            int testCount = 0;
            if (describeLogstreamsResponse.LogStreams.Count > 0)
            {
                foreach (var logStream in describeLogstreamsResponse.LogStreams)
                {
                    GetLogEventsResponse getLogEventsResponse = testFixture.client.GetLogEventsAsync(new GetLogEventsRequest
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


            Assert.Equal(totcount, testCount);

            testFixture.logGroupNameList.Add(logGroupName);
            testFixture.regionList.Add(region);
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            global::NLog.Logger logger;
            try
            {
                LogManager.Configuration = new XmlLoggingConfiguration("Regular.config");
            }
            catch (FileNotFoundException)
            {
                LogManager.Configuration = new XmlLoggingConfiguration("./test/AWS.Logger.NLog.Tests/Regular.config");
            }
            logger = LogManager.GetLogger("loggerMultiThreadBufferFull");
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => NLogThread(count, logger)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray());

            string region = "us-west-2";
            string logGroupName = "AWSNLogGroupMultiThreadBufferFullTest";

            Thread.Sleep(10000);
            testFixture.client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));

            DescribeLogStreamsResponse describeLogstreamsResponse = testFixture.client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;


            List<string> logStreamNames = new List<string>();
            logStreamNames.Add(describeLogstreamsResponse.LogStreams[0].LogStreamName);
            FilterLogEventsResponse filterLogEventsResponse = testFixture.client.FilterLogEventsAsync(new FilterLogEventsRequest
            {
                FilterPattern = "maximum",
                LogGroupName = logGroupName,
                LogStreamNames = logStreamNames
            }).Result;

            Assert.NotEmpty(filterLogEventsResponse.Events);

            testFixture.logGroupNameList.Add(logGroupName);
            testFixture.regionList.Add(region);
        }

        private void NLogThread(int count, global::NLog.Logger logger)
        {
            for (int i = 0; i < count; i++)
            {
                logger.Debug(string.Format("Test logging message {0} NLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
        }

        //public void Dispose()
        //{
        //    testFixture.client = new AmazonCloudWatchLogsClient(
        //        Amazon.RegionEndpoint.GetBySystemName(region));

        //    var response = testFixture.client.DeleteLogGroupAsync(new DeleteLogGroupRequest
        //    {
        //        LogGroupName = logGroupName
        //    });
        //}
    }
    #endregion
}
