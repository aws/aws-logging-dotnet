using System;
using System.Reflection;
using System.Threading;
using AWS.Logger.TestUtils;
using log4net;
using log4net.Config;
using Xunit;

namespace AWS.Logger.Log4Net.Tests
{
    public class Log4NetTestClass : BaseTestClass
    {
        public ILog Logger;

        private void GetLog4NetLogger(string fileName, string logName)
        {
            // Create logger
            var repositoryAssembly = typeof(Log4NetTestClass).GetTypeInfo().Assembly;
            var loggerRepository = LogManager.GetRepository(repositoryAssembly);
            XmlConfigurator.Configure(loggerRepository, new System.IO.FileInfo(fileName));
            Logger = LogManager.GetLogger(repositoryAssembly, logName);
        }

        public Log4NetTestClass(TestFixture testFixture) : base(testFixture)
        {
        }

        #region Test Cases
        [Fact]
        public void Log4Net()
        {
            GetLog4NetLogger("log4net.config","Log4Net");
            SimpleLoggingTest("AWSLog4NetGroupLog4Net");
        }

        [Fact]
        public void MultiThreadTest()
        {
            GetLog4NetLogger("MultiThreadTest.config", "MultiThreadTest");
            MultiThreadTestGroup("AWSLog4NetGroupLog4NetMultiThreadTest");
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            GetLog4NetLogger("MultiThreadBufferFullTest.config", "MultiThreadBufferFullTest");
            MultiThreadBufferFullTestGroup("AWSLog4NetGroupMultiThreadBufferFullTest");
        }

        /// <summary>
        /// Verifies that multiple producers can log to the same log stream
        /// when an override log stream name is provided
        /// </summary>
        [Fact]
        public void CustomLogStreamNameTest()
        {
            GetLog4NetLogger("OverrideLogStreamName.config", "OverrideLogStreamName");
            MultiThreadTestGroup("AWSLog4NetGroupOverrideLogStreamName");
        }

        protected override void LogMessages(int count)
        {
            for (int i = 0; i < count-1; i++)
            {
                Logger.Debug(string.Format("Test logging message {0} Log4Net, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            Logger.Debug(LASTMESSAGE);
        }
        #endregion
    }
}
