using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Xunit;

namespace AWS.Logger.TestUtils
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. 
    // In the Build tab select "Produce outputs on build".

    //TestClass to dispose test generated LogGroups.
    public class TestFixture : IAsyncLifetime
    {
        public List<string> LogGroupNameList;
        public AmazonCloudWatchLogsClient Client;

        public Task InitializeAsync()
        {
            Client = new AmazonCloudWatchLogsClient(Amazon.RegionEndpoint.USWest2);
            LogGroupNameList = new List<string>();

            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            foreach (var logGroupName in LogGroupNameList)
            {
                try
                {
                    if (!(string.IsNullOrEmpty(logGroupName)))
                    {
                        var describeLogGroupsResponse = await Client.DescribeLogGroupsAsync(
                            new DescribeLogGroupsRequest
                            {
                                LogGroupNamePrefix = logGroupName
                            });

                        foreach (var logGroup in describeLogGroupsResponse.LogGroups)
                        {
                            if (!(string.IsNullOrEmpty(logGroup.LogGroupName)))
                            {
                                var response = await Client.DeleteLogGroupAsync(new DeleteLogGroupRequest
                                {
                                    LogGroupName = logGroup.LogGroupName
                                });
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
        }
    }
}
