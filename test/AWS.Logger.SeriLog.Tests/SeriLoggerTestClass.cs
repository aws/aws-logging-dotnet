using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudWatchLogs.Model;
using AWS.Logger.SeriLog;
using AWS.Logger.TestUtils;
using Microsoft.Extensions.Configuration;
using Serilog;
using Xunit;

namespace AWS.Logger.SeriLog.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. 
    // In the Build tab select "Produce outputs on build".
    public class SeriLoggerTestClass : BaseTestClass
    {
        public SeriLoggerTestClass(TestFixture testFixture) : base(testFixture)
        {
        }

        private void CreateLoggerFromConfiguration(string configurationFile, string logGroupName)
        {
            var fileInfo = new FileInfo(configurationFile);
            var fileContent = File.ReadAllText(fileInfo.FullName);
            using (Stream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent.Replace("{LOG_GROUP_NAME}", logGroupName))))
            {
                var configuration = new ConfigurationBuilder()
                .AddJsonStream(memoryStream)
                .Build();

                Log.Logger = new LoggerConfiguration().
                    ReadFrom.Configuration(configuration).
                     WriteTo.AWSSeriLog(configuration).CreateLogger();
            }
        }
        #region Test Cases  

        [Fact]
        public async Task SeriLogger()
        {
            var logGroupName = $"AWSSeriLogGroup{Guid.NewGuid().ToString().Split('-').Last()}";
            CreateLoggerFromConfiguration("AWSSeriLogGroup.json", logGroupName);
            await SimpleLoggingTest(logGroupName);
        }

        [Fact]
        public async Task MultiThreadTest()
        {
            var logGroupName = $"AWSSeriLogGroup{Guid.NewGuid().ToString().Split('-').Last()}";
            CreateLoggerFromConfiguration("AWSSeriLogGroupMultiThreadTest.json", logGroupName);
            await MultiThreadTestGroup(logGroupName);
        }

        [Fact]
        public async Task MultiThreadBufferFullTest()
        {
            var logGroupName = $"AWSSeriLogGroup{Guid.NewGuid().ToString().Split('-').Last()}";
            CreateLoggerFromConfiguration("AWSSeriLogGroupMultiThreadBufferFullTest.json", logGroupName);
            await MultiThreadBufferFullTestGroup(logGroupName);
        }

        /// <summary>
        /// Verifies that multiple producers can log to the same log stream
        /// when an override log stream name is provided
        /// </summary>
        [Fact]
        public async Task CustomLogStreamNameTest()
        {
            var logGroupName = $"AWSSeriLogGroup{Guid.NewGuid().ToString().Split('-').Last()}";
            CreateLoggerFromConfiguration("AWSSeriLogGroupOverrideLogStreamName.json", logGroupName);
            await MultiThreadTestGroup(logGroupName, "CustomLogStream");
        }

        [Fact]
        public async Task RestrictedToMinimumLevelTest()
        {
            var logGroupName = $"AWSSeriLogGroupRestrictedtoMinimumLevel{Guid.NewGuid().ToString().Split('-').Last()}";

            var fileInfo = new FileInfo("AWSSeriLogGroupRestrictedToMinimumLevel.json");
            var fileContent = File.ReadAllText(fileInfo.FullName);
            using (Stream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent.Replace("{LOG_GROUP_NAME}", logGroupName))))
            {
                // Create logger
                var configuration = new ConfigurationBuilder()
                .AddJsonStream(memoryStream)
                .Build();

                Log.Logger = new LoggerConfiguration().
                    ReadFrom.Configuration(configuration).CreateLogger();
            }

            await ExecuteRestrictedToMinimumLevelTest(logGroupName);
        }

        private async Task ExecuteRestrictedToMinimumLevelTest(string logGroupName)
        {
            // Log 4 Debug messages
            for (int i = 0; i < 3; i++)
            {
                Log.Debug(string.Format("Test logging message {0} SeriLog, Thread Id:{1}", i, Environment.CurrentManagedThreadId));
            }
            // Log 5 Error messages
            for (int i = 0; i < 5; i++)
            {
                Log.Error(string.Format("Test logging message {0} SeriLog, Thread Id:{1}", i, Environment.CurrentManagedThreadId));
            }
            Log.Error(LASTMESSAGE);

            GetLogEventsResponse getLogEventsResponse = new GetLogEventsResponse();
            if (await NotifyLoggingCompleted(logGroupName, "LASTMESSAGE"))
            {
                DescribeLogStreamsResponse describeLogstreamsResponse = await _testFixture.Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                {
                    Descending = true,
                    LogGroupName = logGroupName,
                    OrderBy = "LastEventTime"
                });

                getLogEventsResponse = await _testFixture.Client.GetLogEventsAsync(new GetLogEventsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
                });
            }
            Assert.Equal(6, getLogEventsResponse.Events.Count);
        }

        /// <summary>
        /// This method posts debug messages onto CloudWatchLogs.
        /// </summary>
        /// <param name="count">The number of messages that would be posted onto CloudWatchLogs</param>
        protected override void LogMessages(int count)
        {
            for (int i = 0; i < count - 1; i++)
            {
                Log.Debug(string.Format("Test logging message {0} SeriLog, Thread Id:{1}", i, Environment.CurrentManagedThreadId));
            }
            Log.Debug(LASTMESSAGE);
        }
#endregion
    }
}
