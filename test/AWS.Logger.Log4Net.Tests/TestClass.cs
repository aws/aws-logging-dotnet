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

namespace AWS.Logger.Log4Net.Tests
{
    public class TestClass
    {
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

            AmazonCloudWatchLogsClient client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));

            DescribeLogStreamsResponse describeLogstreamsResponse = client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;


            GetLogEventsResponse getLogEventsResponse = client.GetLogEventsAsync(new GetLogEventsRequest
            {
                LogGroupName = logGroupName,
                LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
            }).Result;
            Assert.Equal(10, getLogEventsResponse.Events.Count());

        }
    }
    public class Log4NetMultiThreadTest
    { 
        [Fact]
        public void MultiThreadTest()
        {
            ILog logger;
            XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
            logger = LogManager.GetLogger("MultiThreadTest");

            var log = LogManager.GetCurrentLoggers();
            var tasks = new List<Task>();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => Log4NetThread(count,logger)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);

            //Added Sleep to give sufficient time for the log stream to get posted on CloudWatch
            Thread.Sleep(5000);
            string region = "us-west-2";
            string logGroupName = "AWSLog4NetGroupLog4NetMultiThreadTest";

            AmazonCloudWatchLogsClient client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));

            DescribeLogStreamsResponse describeLogstreamsResponse = client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;


            GetLogEventsResponse getLogEventsResponse = client.GetLogEventsAsync(new GetLogEventsRequest
            {
                LogGroupName = logGroupName,
                LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
            }).Result;
            Assert.Equal(totcount, getLogEventsResponse.Events.Count());
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            ILog logger;
            XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
            logger = LogManager.GetLogger("MultiThreadBufferFullTest");

            var tasks = new List<Task>();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => Log4NetThread(count,logger)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);

            Thread.Sleep(10000);
            string region = "us-west-2";
            string logGroupName = "AWSLog4NetGroupMultiThreadBufferFullTest";

            AmazonCloudWatchLogsClient client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));

            DescribeLogStreamsResponse describeLogstreamsResponse = client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;

            List<string> logStreamNames = new List<string>();
            logStreamNames.Add(describeLogstreamsResponse.LogStreams[0].LogStreamName);
            FilterLogEventsResponse filterLogEventsResponse = client.FilterLogEventsAsync(new FilterLogEventsRequest
            {
                FilterPattern = "maximum",
                LogGroupName = logGroupName,
                LogStreamNames = logStreamNames
            }).Result;

            Assert.NotEmpty(filterLogEventsResponse.Events);
        }

        void Log4NetThread(int count,ILog logger)
        {
            for (int i = 0; i < count; i++)
            {
                logger.Debug(string.Format("Test logging message {0} Log4Net, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
        }
    }
        #endregion
}
