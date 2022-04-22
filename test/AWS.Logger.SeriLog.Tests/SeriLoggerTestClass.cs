using System;
using System.Threading;
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

        private void CreateLoggerFromConfiguration(string configurationFile)
        {
            var configuration = new ConfigurationBuilder()
            .AddJsonFile(configurationFile)
            .Build();
             
            Log.Logger = new LoggerConfiguration().
                ReadFrom.Configuration(configuration).
                 WriteTo.AWSSeriLog( configuration).CreateLogger();
        }
        #region Test Cases  

        [Fact]
        public void SeriLogger()
        {
            CreateLoggerFromConfiguration("AWSSeriLogGroup.json");
            SimpleLoggingTest("AWSSeriLogGroup");
        }

        [Fact]
        public void MultiThreadTest()
        {
            CreateLoggerFromConfiguration("AWSSeriLogGroupMultiThreadTest.json");
            MultiThreadTestGroup("AWSSeriLogGroupMultiThreadTest");
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            CreateLoggerFromConfiguration("AWSSeriLogGroupMultiThreadBufferFullTest.json");
            MultiThreadBufferFullTestGroup("AWSSeriLogGroupMultiThreadBufferFullTest");
        }

        [Fact]
        public void RestrictedToMinimumLevelTest()
        {
            string logGroupName = "AWSSeriLogGroupRestrictedtoMinimumLevel";
            // Create logger
            var configuration = new ConfigurationBuilder()
            .AddJsonFile("AWSSeriLogGroupRestrictedToMinimumLevel.json")
            .Build();

            Log.Logger = new LoggerConfiguration().
                ReadFrom.Configuration(configuration).CreateLogger();

            ExecuteRestrictedToMinimumLevelTest(logGroupName);
        }

        private void ExecuteRestrictedToMinimumLevelTest(string logGroupName)
        {
            // Log 4 Debug messages
            for (int i = 0; i < 3; i++)
            {
                Log.Debug(string.Format("Test logging message {0} SeriLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            // Log 5 Error messages
            for (int i = 0; i < 5; i++)
            {
                Log.Error(string.Format("Test logging message {0} SeriLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            Log.Error(LASTMESSAGE);

            GetLogEventsResponse getLogEventsResponse = new GetLogEventsResponse();
            if (NotifyLoggingCompleted("AWSSeriLogGroupRestrictedtoMinimumLevel", "LASTMESSAGE"))
            {
                DescribeLogStreamsResponse describeLogstreamsResponse = Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
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
                Log.Debug(string.Format("Test logging message {0} SeriLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            Log.Debug(LASTMESSAGE);
        }
#endregion
    }
}
