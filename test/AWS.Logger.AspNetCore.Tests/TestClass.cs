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
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class ILoggerTestClass : IClassFixture<TestFixture>
    {
        #region Properties
        ILogger logger;
        internal AWSLoggerConfigSection _configSection = null;
        IServiceCollection _serviceCollection = new ServiceCollection();
        IServiceProvider _provider;
        TestFixture testFixture;
        #endregion
        public ILoggerTestClass(TestFixture testFixture)
        {
            this.testFixture = testFixture;
        }


        /// <summary>
        /// Setup class that marks down the _configSection, upon which the logger object would be created
        /// </summary>
        /// <param name="configFileName">The configuration file that contains the user's config data as a Json file.</param>
        /// <param name="configSectionInfoBlockName">The Json object name that contains the AWS Logging configuration information
        /// . The Default value is "AWS.Logging".</param>
        /// <param name="sourceFilePath">The source file path specifies the path for the configuration file.</param>
        public void LoggerConfigSectionSetup(string configFileName,string configSectionInfoBlockName,
            [System.Runtime.CompilerServices.CallerFilePath]string sourceFilePath = "")
        {
            var configurationBuilder = new ConfigurationBuilder()
                                       .SetBasePath(Path.GetDirectoryName(sourceFilePath))
                                       .AddJsonFile(configFileName);

            if (configSectionInfoBlockName != null)
            {
                _configSection = configurationBuilder
                    .Build()
                    .GetAWSLoggingConfigSection(configSectionInfoBlockName);
            }

            else
            {
                _configSection = configurationBuilder
                      .Build()
                      .GetAWSLoggingConfigSection();
            }

        }

        /// <summary>
        /// This method returns an ILogger object.
        /// </summary>
        /// <returns></returns>
        public Microsoft.Extensions.Logging.ILogger LoggerSetup()
        {
            var loggingFactoryService = this._serviceCollection.FirstOrDefault(x => x.ServiceType is ILoggerFactory);
            if (loggingFactoryService == null)
            {
                this._serviceCollection.AddLogging();
            }
            this._provider = this._serviceCollection.BuildServiceProvider();

            if (loggingFactoryService == null)
            {
                var loggingFactory = this._provider.GetService<ILoggerFactory>();
                loggingFactory.AddAWSProvider(_configSection);
                logger = loggingFactory.CreateLogger<ILoggerTestClass>();
            }
            return logger;
        }
        #region Tests
        /// <summary>
        /// Basic test case that reads the configuration from "appsettings.json", creates a log object and logs
        /// 10 debug messages to CloudWatchLogs. The results are then verified.
        /// </summary>
        [Fact]
        public void ILogger()
        {
            LoggerConfigSectionSetup("appsettings.json",null);
            logger = LoggerSetup();
            for (int i = 0; i < 10; i++)
            {
                logger.LogDebug(string.Format("Test logging message {0} Ilogger", i));
            }

            //Sleep is introduced to give suffiecient time for the logstream to get posted on CloudWatchLogs
            Thread.Sleep(5000);
            string region = _configSection.Config.Region;
            string logGroupName = _configSection.Config.LogGroup;

            testFixture.client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));

            DescribeLogStreamsResponse describeLogstreamsResponse = testFixture.client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;


            GetLogEventsResponse getLogEventsResponse = testFixture.client.GetLogEventsAsync(new GetLogEventsRequest
            {
                LogGroupName = logGroupName,
                LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
            }).Result;
            Assert.Equal(10, getLogEventsResponse.Events.Count());
            testFixture.logGroupNameList.Add(logGroupName);
            testFixture.regionList.Add(region);
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
            logger = new AWSLogger(
                categoryName,
                coreLogger, null);
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            var count = 200;
            int i = 0;
            var totcount = 0;
            for (i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => ILoggerThread(count)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray());

            Assert.Equal(totcount, coreLogger.ReceivedMessages.Count);
        }

        /// <summary>
        /// Basic test case that reads the configuration from "appsettings.json", creates a log object and spools multiple
        /// threads that log 200 debug messages each to CloudWatchLogs. The results are then verified.
        /// </summary>
        [Fact]
        public void MultiThreadTest()
        {
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            ILoggerSetup("multiThreadTest.json");
            logger = LoggerSetup();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => ILoggerThread(count)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);

            //Sleep is introduced to give sufficient time for the logstream to get posted on CloudWatchLogs
            Thread.Sleep(10000);
            string region = _configSection.Config.Region;
            string logGroupName = _configSection.Config.LogGroup;

            testFixture.client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));

            DescribeLogStreamsResponse describeLogstreamsResponse = testFixture.client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;

            int testCount = 0;
            if (describeLogstreamsResponse.LogStreams.Count > 0)
            {
                foreach(var logStream in describeLogstreamsResponse.LogStreams)
                {
                    GetLogEventsResponse getLogEventsResponse = testFixture.client.GetLogEventsAsync(new GetLogEventsRequest
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

            testFixture.logGroupNameList.Add(logGroupName);
            testFixture.regionList.Add(region);
        }

        public void ILoggerSetup(string configFileName)
        {
            LoggerConfigSectionSetup(configFileName, null);
        }

        /// <summary>
        /// Basic test case that reads the configuration from "multiThreadBufferFullTest.json", creates a log object and spools multiple
        /// threads that log 200 debug messages each to CloudWatchLogs with a reduced buffer size of just 10 messages
        /// inorder to force a buffer full scenario. The results are then verified.
        /// </summary>
        [Fact]
        public void MultiThreadBufferFullTest()
        {
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            ILoggerSetup("multiThreadBufferFullTest.json");
            logger = LoggerSetup();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => ILoggerThread(count)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);

            //Sleep is introduced to give suffiecient time for the logstream to get posted on CloudWatchLogs
            Thread.Sleep(5000);
            string region = _configSection.Config.Region;
            string logGroupName = _configSection.Config.LogGroup;

            testFixture.client = new AmazonCloudWatchLogsClient(
                Amazon.RegionEndpoint.GetBySystemName(region));

            DescribeLogStreamsResponse describeLogstreamsResponse = testFixture.client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;

            List<string> logStreamNames = new List<string>();
            logStreamNames.Add(describeLogstreamsResponse.LogStreams[0].LogStreamName);
            FilterLogEventsResponse filterLogEventsResponse = testFixture.client.FilterLogEventsAsync(new FilterLogEventsRequest
            {
                FilterPattern = "maximum",
                LogGroupName = logGroupName,
                LogStreamNames = logStreamNames
            }).Result;

            Assert.NotEmpty(filterLogEventsResponse.Events);
            testFixture.logGroupNameList.Add(logGroupName);
            testFixture.regionList.Add(region);
        }

        /// <summary>
        /// This method posts debug messages onto CloudWatchLogs.
        /// </summary>
        /// <param name="count">The number of messages that would be posted onto CloudWatchLogs</param>
        public void ILoggerThread(int count)
        {
            for (int i = 0; i < count; i++)
            {
                logger.LogDebug(string.Format("Test logging message {0} Ilogger, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
        }
        #endregion
    }
}