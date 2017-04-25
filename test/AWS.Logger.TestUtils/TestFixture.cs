using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using System.Threading;
using System.Diagnostics;

namespace AWS.Logger.TestUtils
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. 
    // In the Build tab select "Produce outputs on build".

    //TestClass to dispose test generated LogGroups.
    public class TestFixture : IDisposable
    {
        public List<string> LogGroupNameList;
        public AmazonCloudWatchLogsClient Client;

        public TestFixture()
        {
            Client = new AmazonCloudWatchLogsClient(Amazon.RegionEndpoint.USWest2);
            LogGroupNameList = new List<string>();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            foreach(var logGroupName in LogGroupNameList)
            {
                if (!(string.IsNullOrEmpty(logGroupName)))
                {
                    var response = Client.DeleteLogGroupAsync(new DeleteLogGroupRequest
                    {
                        LogGroupName = logGroupName
                    }).Result;
                }
            }
        }
        public bool NotifyLoggingCompleted(string logGroupName, string filterPattern)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (timer.Elapsed.TotalSeconds<10) 
            {
                Thread.Sleep(500);
                if(IsFilterPatternExists(logGroupName, filterPattern))
                {
                    break;
                }
            }
            return IsFilterPatternExists(logGroupName, filterPattern);
        }
        public bool IsFilterPatternExists(string logGroupName, string filterPattern)
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
                        FilterPattern = filterPattern,
                        LogGroupName = logGroupName,
                        LogStreamNames = logStreamNames
                    }).Result;

                return filterLogEventsResponse.Events.Count > 0;
            }
            else
            {
                return false;
            }
        }
    }
}
