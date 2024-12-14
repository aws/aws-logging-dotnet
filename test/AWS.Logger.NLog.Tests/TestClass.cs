using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Amazon.CloudWatchLogs.Model;
using AWS.Logger.TestUtils;
using NLog;
using NLog.Config;
using Xunit;

namespace AWS.Logger.NLogger.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class NLogTestClass: BaseTestClass
    {
        public NLog.Logger Logger;

        private void CreateLoggerFromConfiguration(string configFileName, string logGroupName)
        {
            var fileInfo = new FileInfo(configFileName);
            var fileContent = File.ReadAllText(fileInfo.FullName);
            using (Stream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent.Replace("{LOG_GROUP_NAME}", logGroupName))))
            using (XmlReader reader = XmlReader.Create(memoryStream))
            {
                LogManager.Configuration = new XmlLoggingConfiguration(reader, configFileName);
            }
        }

        public NLogTestClass(TestFixture testFixture) : base(testFixture)
        {
        }

        #region Test Cases  
        [Fact]
        public async Task Nlog()
        {
            var logGroupName = $"AWSNLogGroup{Guid.NewGuid().ToString().Split('-').Last()}";
            CreateLoggerFromConfiguration("Regular.config", logGroupName);
            Logger = LogManager.GetLogger("loggerRegular");
            await SimpleLoggingTest(logGroupName);
        }

        [Fact]
        public async Task MultiThreadTest()
        {
            var logGroupName = $"AWSNLogGroupMultiThreadTest{Guid.NewGuid().ToString().Split('-').Last()}";
            CreateLoggerFromConfiguration("AWSNLogGroupMultiThreadTest.config", logGroupName);
            Logger = LogManager.GetLogger("loggerMultiThread");
            await MultiThreadTestGroup(logGroupName);
        }

        [Fact]
        public async Task MultiThreadBufferFullTest()
        {
            var logGroupName = $"AWSNLogGroupMultiThreadBufferFullTest{Guid.NewGuid().ToString().Split('-').Last()}";
            CreateLoggerFromConfiguration("AWSNLogGroupMultiThreadBufferFullTest.config", logGroupName);
            Logger = LogManager.GetLogger("loggerMultiThreadBufferFull");
            await MultiThreadBufferFullTestGroup(logGroupName);
        }

        /// <summary>
        /// Verifies that multiple producers can log to the same log stream
        /// when an override log stream name is provided
        /// </summary>
        [Fact]
        public async Task CustomLogStreamNameTest()
        {
            var logGroupName = $"AWSNLogOverrideLogStreamName{Guid.NewGuid().ToString().Split('-').Last()}";
            CreateLoggerFromConfiguration("AWSNLogOverrideLogStreamName.config", logGroupName);
            Logger = LogManager.GetLogger("overrideLogStreamName");
            await MultiThreadTestGroup(logGroupName, "CustomStreamName");
        }

        [Fact]
        public async Task MessageHasToBeBrokenUp()
        {
            var logGroupName = $"AWSNLogGroupEventSizeExceededTest{Guid.NewGuid().ToString().Split('-').Last()}";

            CreateLoggerFromConfiguration("AWSNLogGroupEventSizeExceededTest.config", logGroupName);
            Logger = LogManager.GetLogger("loggerRegularEventSizeExceeded");

            // This will get broken up into 3 CloudWatch Log messages
            Logger.Debug(new string('a', 600000)); 
            Logger.Debug(LASTMESSAGE);

            GetLogEventsResponse getLogEventsResponse = new GetLogEventsResponse();
            if (await NotifyLoggingCompleted(logGroupName, "LASTMESSAGE"))
            {
                DescribeLogStreamsResponse describeLogstreamsResponse =
                await _testFixture.Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                {
                    Descending = true,
                    LogGroupName = logGroupName,
                    OrderBy = "LastEventTime"
                });

                // Wait for the large messages to propagate
                Thread.Sleep(5000);
                getLogEventsResponse = await _testFixture.Client.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
                });
            }
            _testFixture.LogGroupNameList.Add(logGroupName);
            Assert.Equal(4, getLogEventsResponse.Events.Count);
        }

        protected override void LogMessages(int count)
        {
            for (int i = 0; i < count-1; i++)
            {
                Logger.Debug(string.Format("Test logging message {0} NLog, Thread Id:{1}", i, Environment.CurrentManagedThreadId));
            }
            Logger.Debug(LASTMESSAGE);
        }
    }
    #endregion
}
