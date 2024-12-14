using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AWS.Logger.Log4net;
using AWS.Logger.TestUtils;
using log4net;
using log4net.Config;
using Xunit;

namespace AWS.Logger.Log4Net.Tests
{
    public class Log4NetTestClass : BaseTestClass
    {
        public ILog Logger;

        private void GetLog4NetLogger(string fileName, string logName, string logGroupName)
        {
            // Create logger
            var repositoryAssembly = typeof(Log4NetTestClass).GetTypeInfo().Assembly;
            var loggerRepository = LogManager.GetRepository(repositoryAssembly);
            var fileInfo = new FileInfo(fileName);
            var fileContent = File.ReadAllText(fileInfo.FullName);
            using (Stream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent.Replace("{LOG_GROUP_NAME}", logGroupName))))
            {
                XmlConfigurator.Configure(loggerRepository, memoryStream);
                Logger = LogManager.GetLogger(repositoryAssembly, logName);
            }
        }

        public Log4NetTestClass(TestFixture testFixture) : base(testFixture)
        {
        }

        #region Test Cases
        [Fact]
        public async Task Log4Net()
        {
            var logGroupName = $"AWSLog4NetGroupLog4Net{Guid.NewGuid().ToString().Split('-').Last()}";
            GetLog4NetLogger("log4net.config","Log4Net", logGroupName);
            await SimpleLoggingTest(logGroupName);
        }

        [Fact]
        public async Task MultiThreadTest()
        {
            var logGroupName = $"AWSLog4NetGroupLog4NetMultiThreadTest{Guid.NewGuid().ToString().Split('-').Last()}";
            GetLog4NetLogger("MultiThreadTest.config", "MultiThreadTest", logGroupName);
            await MultiThreadTestGroup(logGroupName);
        }

        [Fact]
        public async Task MultiThreadBufferFullTest()
        {
            var logGroupName = $"AWSLog4NetGroupMultiThreadBufferFullTest{Guid.NewGuid().ToString().Split('-').Last()}";
            GetLog4NetLogger("MultiThreadBufferFullTest.config", "MultiThreadBufferFullTest", logGroupName);
            await MultiThreadBufferFullTestGroup(logGroupName);
        }

        /// <summary>
        /// Verifies that multiple producers can log to the same log stream
        /// when an override log stream name is provided
        /// </summary>
        [Fact]
        public async Task CustomLogStreamNameTest()
        {
            var logGroupName = $"AWSLog4NetGroupMultiThreadBufferFullTest{Guid.NewGuid().ToString().Split('-').Last()}";
            GetLog4NetLogger("OverrideLogStreamName.config", "OverrideLogStreamName", logGroupName);
            await MultiThreadTestGroup(logGroupName);
        }

        protected override void LogMessages(int count)
        {
            for (int i = 0; i < count-1; i++)
            {
                Logger.Debug(string.Format("Test logging message {0} Log4Net, Thread Id:{1}", i, Environment.CurrentManagedThreadId));
            }
            Logger.Debug(LASTMESSAGE);
        }
        #endregion
    }
}
