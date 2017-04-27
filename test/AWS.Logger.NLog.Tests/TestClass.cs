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
    public class NLogTestSetup : BaseTestClass
    {
        public NLog.Logger Logger;
        public NLogTestSetup(TestFixture testFixture) : base(testFixture)
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
    public class NLogTestClass: NLogTestSetup
    {
        public NLogTestClass(TestFixture testFixture) : base(testFixture)
        {
        }

        #region Test Cases  
        [Fact]
        public void Nlog()
        {
            Logger = LogManager.GetLogger("loggerRegular");
            SimpleLogging("AWSNLogGroup");
        }

        [Fact]
        public void MultiThreadTest()
        {
            Logger = LogManager.GetLogger("loggerMultiThread");
            MultiThreadTest("AWSNLogGroupMultiThreadTest");
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            Logger = LogManager.GetLogger("loggerMultiThreadBufferFull");
            MultiThreadBufferFullTest("AWSNLogGroupMultiThreadBufferFullTest");
        }

        public override void Logging(int count)
        {
            for (int i = 0; i < count-1; i++)
            {
                Logger.Debug(string.Format("Test logging message {0} NLog, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            Logger.Debug("LASTMESSAGE");
        }
    }
    #endregion
}
