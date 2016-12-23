using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;

namespace AWS.Logger.TestUtils
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".

    //TestClass to dispose test generated LogGroups.
    public class TestFixture : IDisposable
    {
        public List<string> logGroupNameList;
        public List<string> regionList;
        public AmazonCloudWatchLogsClient client;

        public TestFixture()
        {
            logGroupNameList = new List<string>();
            regionList = new List<string>();
        }

        public void Dispose()
        {
            var comboList = logGroupNameList.Zip(regionList, (l, r) => new { LogGroupName = l, Region = r });
            foreach (var element in comboList)
            {
                if(!(string.IsNullOrEmpty(element.Region)))
                {
                        client = new AmazonCloudWatchLogsClient(
                    Amazon.RegionEndpoint.GetBySystemName(element.Region));

                    if (!(string.IsNullOrEmpty(element.LogGroupName)))
                    {
                        var response = client.DeleteLogGroupAsync(new DeleteLogGroupRequest
                        {
                            LogGroupName = element.LogGroupName
                        }).Result;
                    }
                }
                
            }
        }
    }
}
