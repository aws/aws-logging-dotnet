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

namespace AWS.Logger.NLogger.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class NLogTestClass: BaseTestClass
    {
        public NLog.Logger Logger;

        private void CreateLoggerFromConfiguration(string configFileName)
        {
            LogManager.Configuration = new XmlLoggingConfiguration(configFileName);
        }
        public NLogTestClass(TestFixture testFixture) : base(testFixture)
        {
        }

        #region Test Cases  
        [Fact]
        public void Nlog()
        {
            CreateLoggerFromConfiguration("Regular.config");
            Logger = LogManager.GetLogger("loggerRegular");
            SimpleLoggingTest("AWSNLogGroup");
        }

        [Fact]
        public void MultiThreadTest()
        {
            CreateLoggerFromConfiguration("AWSNLogGroupMultiThreadTest.config");
            Logger = LogManager.GetLogger("loggerMultiThread");
            MultiThreadTest("AWSNLogGroupMultiThreadTest");
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            CreateLoggerFromConfiguration("AWSNLogGroupMultiThreadBufferFullTest.config");
            Logger = LogManager.GetLogger("loggerMultiThreadBufferFull");
            MultiThreadBufferFullTest("AWSNLogGroupMultiThreadBufferFullTest");
        }

        [Fact]
        public void MessageHasToBeBrokenUp()
        {
            string logGroupName = "AWSNLogGroupEventSizeExceededTest";

            CreateLoggerFromConfiguration("AWSNLogGroupEventSizeExceededTest.config");
            Logger = LogManager.GetLogger("loggerRegularEventSizeExceeded");

            // This will get broken up into 3 CloudWatch Log messages
            Logger.Debug(new string('a', 600000)); 
            Logger.Debug(LASTMESSAGE);

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

                // Wait for the large messages to propagate
                Thread.Sleep(5000);
                getLogEventsResponse = Client.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
                }).Result;
            }
            _testFixture.LogGroupNameList.Add(logGroupName);
            Assert.Equal(4, getLogEventsResponse.Events.Count());
        }

        public override void LogMessages(int count)
        {
            for (int i = 0; i < count-1; i++)
            {
                Logger.Debug(string.Format("Test logging message {0} NLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            Logger.Debug(LASTMESSAGE);
        }
    }
    #endregion
}
