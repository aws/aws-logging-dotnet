using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using AWS.Logger.AspNetCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using AWS.Logger.TestUtils;

namespace AWS.Logger.AspNetCore.Tests
{
    public class ILoggerTestSetup : BaseTestClass
    {
        #region Properties
        public ILogger Logger;
        public AWSLoggerConfigSection ConfigSection;
        public IServiceCollection ServiceCollection;
        public IServiceProvider Provider;
        public ILoggerTestSetup(TestFixture testFixture) : base(testFixture)
        {
            ServiceCollection = new ServiceCollection();
        }
        #endregion

        /// <summary>
        /// Setup class that marks down the _configSection, upon which the logger object would be created
        /// </summary>
        /// <param name="configFileName">The configuration file that contains the user's config data as a Json file.</param>
        /// <param name="configSectionInfoBlockName">The Json object name that contains the AWS Logging configuration information
        /// . The Default value is "AWS.Logging".</param>
        /// <param name="sourceFilePath">The source file path specifies the path for the configuration file.</param>
        public void LoggingSetup(string configFileName, string configSectionInfoBlockName,
            [System.Runtime.CompilerServices.CallerFilePath]string sourceFilePath = "")
        {
            var configurationBuilder = new ConfigurationBuilder()
                                       .SetBasePath(Path.GetDirectoryName(sourceFilePath))
                                       .AddJsonFile(configFileName);

            if (configSectionInfoBlockName != null)
            {
                ConfigSection = configurationBuilder
                    .Build()
                    .GetAWSLoggingConfigSection(configSectionInfoBlockName);
            }

            else
            {
                ConfigSection = configurationBuilder
                      .Build()
                      .GetAWSLoggingConfigSection();
            }
            LoggerSetup();
        }

        /// <summary>
        /// This method returns an ILogger object.
        /// </summary>
        /// <returns></returns>
        public void LoggerSetup()
        {
            var loggingFactoryService = this.ServiceCollection.FirstOrDefault(x => x.ServiceType is ILoggerFactory);
            this.Provider = this.ServiceCollection.AddLogging()
                                    .BuildServiceProvider();

            if (loggingFactoryService == null)
            {
                var loggingFactory = this.Provider.GetService<ILoggerFactory>();
                loggingFactory.AddAWSProvider(ConfigSection);
                Logger = loggingFactory.CreateLogger<ILoggerTestClass>();
            }
        }
    }
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. 
    // In the Build tab select "Produce outputs on build".
    public class ILoggerTestClass : ILoggerTestSetup
    {
        public ILoggerTestClass(TestFixture testFixture) : base(testFixture)
        {
        }


        #region Tests
        /// <summary>
        /// Basic test case that reads the configuration from "appsettings.json", creates a log object and logs
        /// 10 debug messages to CloudWatchLogs. The results are then verified.
        /// </summary>
        [Fact]
        public void ILogger()
        {
            LoggingSetup("appsettings.json",null);
            SimpleLogging(ConfigSection.Config.LogGroup);
        }


        /// <summary>
        /// Basic test case that creates multiple threads and each thread mocks log messages
        /// onto the FakeCoreLogger. The results are then verified.
        /// </summary>
        [Fact]
        public void MultiThreadTestMock()
        {
            var categoryName = "testlogging";
            var coreLogger = new FakeCoreLogger();
            Logger = new AWSLogger(
                categoryName,
                coreLogger, null);
            var tasks = new List<Task>();
            var logMessageCount = 200;
            var actualCount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => Logging(logMessageCount)));
                actualCount = actualCount + logMessageCount;
            }
            Task.WaitAll(tasks.ToArray());

            Assert.Equal(actualCount, coreLogger.ReceivedMessages.Count);
        }

        /// <summary>
        /// Basic test case that reads the configuration from "appsettings.json", 
        /// creates a log object and spools multiple
        /// threads that log 200 debug messages each to CloudWatchLogs. The results are then verified.
        /// </summary>
        [Fact]
        public void MultiThreadTest()
        {
            LoggingSetup("multiThreadTest.json",null);
            MultiThreadTest(ConfigSection.Config.LogGroup);
        }

        /// <summary>
        /// Basic test case that reads the configuration from "multiThreadBufferFullTest.json", 
        /// creates a log object and spools multiple
        /// threads that log 200 debug messages each to CloudWatchLogs with a reduced buffer 
        /// size of just 10 messages
        /// inorder to force a buffer full scenario. The results are then verified.
        /// </summary>
        [Fact]
        public void MultiThreadBufferFullTest()
        {
            LoggingSetup("multiThreadBufferFullTest.json",null);
            MultiThreadBufferFullTest(ConfigSection.Config.LogGroup);
        }

        /// <summary>
        /// This method posts debug messages onto CloudWatchLogs.
        /// </summary>
        /// <param name="count">The number of messages that would be posted onto CloudWatchLogs</param>
        public override void Logging(int count)
        {
            for (int i = 0; i < count-1; i++)
            {
                Logger.LogDebug(string.Format("Test logging message {0} Ilogger, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            Logger.LogDebug("LASTMESSAGE");
        }
        #endregion
    }
}