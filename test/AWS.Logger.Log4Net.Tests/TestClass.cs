using System.Collections.Generic;
using System.Linq;
using Xunit;
using log4net;
using log4net.Config;
using System.Threading;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using log4net.Repository.Hierarchy;
using log4net.Layout;
using log4net.Core;
using System.Threading.Tasks;
using System;
using log4net.Repository;
using AWS.Logger.TestUtils;

namespace AWS.Logger.Log4Net.Tests
{
    public class Log4NetTestClass : IClassFixture<TestFixture>
    {
        TestFixture testFixture;

        public Log4NetTestClass(TestFixture testFixture)
        {
            this.testFixture = testFixture;
        }

        #region Test Cases                                                        
        ILog logger;
        [Fact]
        public void Log4Net()
        {
            XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
            logger = LogManager.GetLogger("Log4Net");
            for (int i = 0; i < 10; i++)
            {
                logger.Debug(string.Format("Test logging message {0} Log4Net", i));
            }

            //Added Sleep to give sufficient time for the log stream to get posted on CloudWatch
            Thread.Sleep(10000);
            string region = "us-west-2";
            string logGroupName = "AWSLog4NetGroupLog4Net";

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
            XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
            logger = LogManager.GetLogger("MultiThreadTest");

            var log = LogManager.GetCurrentLoggers();
            var tasks = new List<Task>();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => Log4NetThread(count)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);

            //Added Sleep to give sufficient time for the log stream to get posted on CloudWatch
            Thread.Sleep(5000);
            string region = "us-west-2";
            string logGroupName = "AWSLog4NetGroupLog4NetMultiThreadTest";

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

            testFixture.logGroupNameList.Add(logGroupName);
            testFixture.regionList.Add(region);
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
            logger = LogManager.GetLogger("MultiThreadBufferFullTest");

            var tasks = new List<Task>();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => Log4NetThread(count)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);

            Thread.Sleep(10000);
            string region = "us-west-2";
            string logGroupName = "AWSLog4NetGroupMultiThreadBufferFullTest";

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

        void Log4NetThread(int count)
        {
            for (int i = 0; i < count; i++)
            {
                logger.Debug(string.Format("Test logging message {0} Log4Net, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
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

        //    testFixture.client.Dispose();
        //}
        #endregion
    }
}
