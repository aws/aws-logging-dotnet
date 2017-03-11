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
    public class ILoggerTestFixture : TestFixture
    {
        #region Properties
        public ILogger Logger;
        public AWSLoggerConfigSection ConfigSection;
        public IServiceCollection ServiceCollection;
        public IServiceProvider Provider;
        #endregion

        public ILoggerTestFixture()
        {
            ServiceCollection = new ServiceCollection();
        }
        /// <summary>
        /// Setup class that marks down the _configSection, upon which the logger object would be created
        /// </summary>
        /// <param name="configFileName">The configuration file that contains the user's config data as a Json file.</param>
        /// <param name="configSectionInfoBlockName">The Json object name that contains the AWS Logging configuration information
        /// . The Default value is "AWS.Logging".</param>
        /// <param name="sourceFilePath">The source file path specifies the path for the configuration file.</param>
        public void LoggerConfigSectionSetup(string configFileName, string configSectionInfoBlockName,
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

        public bool IsLoggingDone(string filterpattern)
        {
            try
            {
                var logGroupName = ConfigSection.Config.LogGroup;
                DescribeLogStreamsResponse describeLogstreamsResponse = Client.
                    DescribeLogStreamsAsync(new DescribeLogStreamsRequest
                    {
                        Descending = true,
                        LogGroupName = logGroupName,
                        OrderBy = "LastEventTime"
                    }).Result;
                if (describeLogstreamsResponse.LogStreams.Count > 0)
                {
                    List<string> logStreamNames = new List<string>();
                    logStreamNames.Add(describeLogstreamsResponse.LogStreams[0].LogStreamName);
                    FilterLogEventsResponse filterLogEventsResponse = Client.
                        FilterLogEventsAsync(new FilterLogEventsRequest
                        {
                            FilterPattern = filterpattern,
                            LogGroupName = logGroupName,
                            LogStreamNames = logStreamNames
                        }).Result;

                    return filterLogEventsResponse.Events.Count == 0;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                return true;
            }
            
        }
    }
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. 
    // In the Build tab select "Produce outputs on build".
    public class ILoggerTestClass : IClassFixture<ILoggerTestFixture>
    {
        ILoggerTestFixture _testFixture;
        public ILoggerTestClass(ILoggerTestFixture testFixture)
        {
            _testFixture = testFixture;
        }

        #region Tests
        /// <summary>
        /// Basic test case that reads the configuration from "appsettings.json", creates a log object and logs
        /// 10 debug messages to CloudWatchLogs. The results are then verified.
        /// </summary>
        [Fact]
        public void ILogger()
        {
            _testFixture.LoggerConfigSectionSetup("appsettings.json",null);
            for (int i = 0; i < 9; i++)
            {
                _testFixture.Logger.LogDebug(string.Format("Test logging message {0} Ilogger", i));
            }

            _testFixture.Logger.LogDebug("LASTMESSAGE");
            //Sleep is introduced to give suffiecient time for the logstream to get posted on CloudWatchLogs
            while (_testFixture.IsLoggingDone("LASTMESSAGE")) { }
            string logGroupName = _testFixture.ConfigSection.Config.LogGroup;

            DescribeLogStreamsResponse describeLogstreamsResponse = _testFixture.
                                                        Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;


            GetLogEventsResponse getLogEventsResponse = _testFixture.
                                                        Client.GetLogEventsAsync(new GetLogEventsRequest
            {
                LogGroupName = logGroupName,
                LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
            }).Result;
            Assert.Equal(10, getLogEventsResponse.Events.Count());
            _testFixture.LogGroupNameList.Add(logGroupName);
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
            _testFixture.Logger = new AWSLogger(
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
        /// Basic test case that reads the configuration from "appsettings.json", 
        /// creates a log object and spools multiple
        /// threads that log 200 debug messages each to CloudWatchLogs. The results are then verified.
        /// </summary>
        [Fact]
        public void MultiThreadTest()
        {
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            _testFixture.LoggerConfigSectionSetup("multiThreadTest.json",null);
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => ILoggerThread(count)));
                totcount = totcount + count;
            }

            
            Task.WaitAll(tasks.ToArray(), 10000);
            while (_testFixture.IsLoggingDone("LASTMESSAGE")) { }

            string logGroupName = _testFixture.ConfigSection.Config.LogGroup;

            DescribeLogStreamsResponse describeLogstreamsResponse = _testFixture.
                Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
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
                    GetLogEventsResponse getLogEventsResponse = _testFixture.
                        Client.GetLogEventsAsync(new GetLogEventsRequest
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

            _testFixture.LogGroupNameList.Add(logGroupName);
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
            var tasks = new List<Task>();
            var streamNames = new List<string>();
            _testFixture.LoggerConfigSectionSetup("multiThreadBufferFullTest.json",null);
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => ILoggerThread(count)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);
            while (_testFixture.IsLoggingDone("maximum")) { }

            Assert.True(!(_testFixture.IsLoggingDone("maximum")));
            _testFixture.LogGroupNameList.Add(_testFixture.ConfigSection.Config.LogGroup);
        }

        /// <summary>
        /// This method posts debug messages onto CloudWatchLogs.
        /// </summary>
        /// <param name="count">The number of messages that would be posted onto CloudWatchLogs</param>
        public void ILoggerThread(int count)
        {
            for (int i = 0; i < count-1; i++)
            {
                _testFixture.Logger.LogDebug(string.Format("Test logging message {0} Ilogger, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            _testFixture.Logger.LogDebug("LASTMESSAGE");
        }
        #endregion
    }
}