using AWS.Logger.TestUtils;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Threading;
using Xunit;

namespace AWS.Logger.SeriLogger.Tests
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
                 WriteTo.AWSSeriLogger(configuration).CreateLogger();
        }
        #region Test Cases  

        [Fact]
        public void SeriLogger()
        {
            CreateLoggerFromConfiguration("AWSSeriLoggerGroup.json");
            SimpleLoggingTest("AWSSeriLoggerGroup");
        }

        [Fact]
        public void MultiThreadTest()
        {
            CreateLoggerFromConfiguration("AWSSeriLoggerGroupMultiThreadTest.json");
            MultiThreadTest("AWSSeriLoggerGroupMultiThreadTest");
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            CreateLoggerFromConfiguration("AWSSeriLoggerGroupMultiThreadBufferFullTest.json");
            MultiThreadBufferFullTest("AWSSeriLoggerGroupMultiThreadBufferFullTest");
        }

        /// <summary>
        /// This method posts debug messages onto CloudWatchLogs.
        /// </summary>
        /// <param name="count">The number of messages that would be posted onto CloudWatchLogs</param>
        public override void LogMessages(int count)
        {
            for (int i = 0; i < count - 1; i++)
            {
                Log.Debug(string.Format("Test logging message {0} SeriLogger, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            Log.Debug(LASTMESSAGE);
        }
#endregion
    }
}
