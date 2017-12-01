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
using System.Reflection;
using log4net.Repository;
using AWS.Logger.TestUtils;

namespace AWS.Logger.Log4Net.Tests
{
    public class Log4NetTestClass : BaseTestClass
    {
        public ILog Logger;

        public void GetLog4NetLogger(string fileName, string logName)
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
            MultiThreadTest("AWSLog4NetGroupLog4NetMultiThreadTest");
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            GetLog4NetLogger("MultiThreadBufferFullTest.config", "MultiThreadBufferFullTest");
            MultiThreadBufferFullTest("AWSLog4NetGroupMultiThreadBufferFullTest");
        }

        public override void LogMessages(int count)
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
