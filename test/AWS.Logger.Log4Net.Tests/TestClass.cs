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
using log4net.Repository;
using AWS.Logger.TestUtils;

namespace AWS.Logger.Log4Net.Tests
{
    public class Log4NetTestFixture : TestFixture
    {
        public ILog Logger;
        public void GetLog4NetLogger(string fileName,string logName)
        {
            XmlConfigurator.Configure(new System.IO.FileInfo(fileName));
            Logger = LogManager.GetLogger(logName);
        }
        public bool IsLoggingDone(string logGroupName,string filterpattern)
        {
            try
            {
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
    public class Log4NetTestClass : IClassFixture<Log4NetTestFixture>
    {
        Log4NetTestFixture _testFixture;
        
        public Log4NetTestClass(Log4NetTestFixture testFixture)
        {
            _testFixture = testFixture;
        }

        #region Test Cases                                                        
        [Fact]
        public void Log4Net()
        {
            _testFixture.GetLog4NetLogger("log4net.config","Log4Net");
            for (int i = 0; i < 9; i++)
            {
                _testFixture.Logger.Debug(string.Format("Test logging message {0} Log4Net", i));
            }
            _testFixture.Logger.Debug("LASTMESSAGE");

            string logGroupName = "AWSLog4NetGroupLog4Net";
            while(_testFixture.IsLoggingDone(logGroupName, "LASTMESSAGE")){ }
            
            DescribeLogStreamsResponse describeLogstreamsResponse = _testFixture.Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;


            GetLogEventsResponse getLogEventsResponse = _testFixture.Client.GetLogEventsAsync(new GetLogEventsRequest
            {
                LogGroupName = logGroupName,
                LogStreamName = describeLogstreamsResponse.LogStreams[0].LogStreamName
            }).Result;
            Assert.Equal(10, getLogEventsResponse.Events.Count());

            _testFixture.LogGroupNameList.Add(logGroupName);
        }

        [Fact]
        public void MultiThreadTest()
        {
            _testFixture.GetLog4NetLogger("log4net.config", "MultiThreadTest");
            var log = LogManager.GetCurrentLoggers();
            var tasks = new List<Task>();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => Log4NetThread(count)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);

            string logGroupName = "AWSLog4NetGroupLog4NetMultiThreadTest";
            while (_testFixture.IsLoggingDone(logGroupName, "LASTMESSAGE")) { }

            DescribeLogStreamsResponse describeLogstreamsResponse = _testFixture.Client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {
                Descending = true,
                LogGroupName = logGroupName,
                OrderBy = "LastEventTime"
            }).Result;


            int testCount = 0;
            if (describeLogstreamsResponse.LogStreams.Count > 0)
            {
                foreach (var logStream in describeLogstreamsResponse.LogStreams)
                {
                    GetLogEventsResponse getLogEventsResponse = _testFixture.Client.GetLogEventsAsync(new GetLogEventsRequest
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

            _testFixture.LogGroupNameList.Add(logGroupName);
        }

        [Fact]
        public void MultiThreadBufferFullTest()
        {
            _testFixture.GetLog4NetLogger("log4net.config", "MultiThreadBufferFullTest");

            var tasks = new List<Task>();
            var count = 200;
            var totcount = 0;
            for (int i = 0; i < 2; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => Log4NetThread(count)));
                totcount = totcount + count;
            }
            Task.WaitAll(tasks.ToArray(), 10000);

            string logGroupName = "AWSLog4NetGroupMultiThreadBufferFullTest";
            while (_testFixture.IsLoggingDone(logGroupName, "maximum")) { }
            Assert.True(!(_testFixture.IsLoggingDone(logGroupName, "maximum")));
            _testFixture.LogGroupNameList.Add(logGroupName);
        }

        void Log4NetThread(int count)
        {
            for (int i = 0; i < count-1; i++)
            {
                _testFixture.Logger.Debug(string.Format("Test logging message {0} Log4Net, Thread Id:{1}", i, Thread.CurrentThread.ManagedThreadId));
            }
            _testFixture.Logger.Debug("LASTMESSAGE");
        }
        #endregion
    }
}
