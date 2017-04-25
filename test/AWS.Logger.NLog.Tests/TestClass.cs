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
    public class NLogTestFixture : TestFixture
    {
        public NLogTestFixture()
        {
            CreateLoggerFromConfiguration();
        }
        private void CreateLoggerFromConfiguration()
        {
            try
            {
                LogManager.Configuration = new XmlLoggingConfiguration("Regular.config");
            }
            catch (FileNotFoundException)
            {
                LogManager.Configuration = new XmlLoggingConfiguration("./test/AWS.Logger.NLog.Tests/Regular.config");
            }
        }
        

    }
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class NLogTestClass: IClassFixture<NLogTestFixture>
    {
        NLogTestFixture _testFixture;
        public NLogTestClass(NLogTestFixture testFixture)
        {
            _testFixture = testFixture;
        }
        #region Test Cases  
        [Fact]
        public void Nlog()
        {
            global::NLog.Logger logger = LogManager.GetLogger("loggerRegular");
            for (int i = 0; i < 9; i++)
            {
                logger.Debug(string.Format("Test logging message {0} NLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            logger.Debug("LASTMESSAGE");
            string logGroupName = "AWSNLogGroup";

            if (_testFixture.NotifyLoggingCompleted(logGroupName, "LASTMESSAGE"))
            {
                DescribeLogStreamsResponse describeLogstreamsResponse = _testFixture.Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                {
                    Descending = true,
                    LogGroupName = logGroupName,
                    OrderBy = "LastEventTime"
                }).Result;

                GetLogEventsResponse getLogEventsResponse = _testFixture.Client.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
                }).Result;

                Assert.Equal(10, getLogEventsResponse.Events.Count());
            }
            else
            {
                Assert.True(false);
            }

            _testFixture.LogGroupNameList.Add(logGroupName);
        }

        [Fact]
        public void MultiThreadTest()
        {
            global::NLog.Logger logger;
            
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
            
            string logGroupName = "AWSNLogGroupMultiThreadTest";
            if (_testFixture.NotifyLoggingCompleted(logGroupName, "LASTMESSAGE"))
            {
                DescribeLogStreamsResponse describeLogstreamsResponse = _testFixture.Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
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
                        GetLogEventsResponse getLogEventsResponse = _testFixture.Client.GetLogEventsAsync(new GetLogEventsRequest
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
            }
            else
            {
                Assert.True(false);
            }
            

            _testFixture.LogGroupNameList.Add(logGroupName);
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            global::NLog.Logger logger;

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
            string logGroupName = "AWSNLogGroupMultiThreadBufferFullTest";
            if (_testFixture.NotifyLoggingCompleted(logGroupName, "maximum"))
            {
                Assert.True(_testFixture.IsFilterPatternExists(logGroupName, "maximum"));
            }
            else
            {
                Assert.True(false);
            }
            _testFixture.LogGroupNameList.Add(logGroupName);
        }

        private void NLogThread(int count, global::NLog.Logger logger)
        {
            for (int i = 0; i < count-1; i++)
            {
                logger.Debug(string.Format("Test logging message {0} NLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            logger.Debug("LASTMESSAGE");
        }
    }
    #endregion
}
