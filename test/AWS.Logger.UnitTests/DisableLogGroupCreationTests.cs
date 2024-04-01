using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using AWS.Logger.Core;
using AWS.Logger.TestUtils;
using Xunit;
using Xunit.Abstractions;

namespace AWS.Logger.UnitTests
{
    public class DisableLogGroupCreationTests : IClassFixture<TestFixture>
    {
        /*AmazonCloudWatchLogsClient client; */


        public DisableLogGroupCreationTests(TestFixture testFixture, ITestOutputHelper output)
        {
            _testFixure = testFixture;
            _output = output;
        }

        private readonly TestFixture _testFixure;
        private readonly ITestOutputHelper _output;

        [Fact]
        public async Task TestCoreWithDisableLogGroupCreation()
        {
            var logGroupName = nameof(TestCoreWithDisableLogGroupCreation);

            using (var client = new AmazonCloudWatchLogsClient(RegionEndpoint.USWest2))
            {
                var config = new AWSLoggerConfig(logGroupName ) 
                {
                    Region = RegionEndpoint.USWest2.SystemName,
                    DisableLogGroupCreation = true,
                };

                var resourceNotFoundPromise = new TaskCompletionSource<bool>();  // true means we saw expected exception; false otherwise
                var core = new AWSLoggerCore(config, "unit");
                core.LogLibraryAlert += (sender, e) =>
                {
                    if (e.Exception is ResourceNotFoundException)
                    {
                        // saw EXPECTED exception.
                        resourceNotFoundPromise.TrySetResult(true);
                    }
                    else if (e.Exception != null)
                    {
                        _output.WriteLine("Was not expecting to see exception: {0} @{1}", e.Exception, e.ServiceUrl);
                    }
                };

                var tsk = Task.Factory.StartNew(() =>
                {    
                    core.AddMessage("Test message added at " + DateTimeOffset.UtcNow.ToString());
                    core.Flush();
                });

                await Task.WhenAny(tsk, resourceNotFoundPromise.Task);
                resourceNotFoundPromise.TrySetResult(false);
                Assert.True(await resourceNotFoundPromise.Task);

                // now we create the log group, late.
                await client.CreateLogGroupAsync(new CreateLogGroupRequest
                {
                    LogGroupName = logGroupName
                });
                _testFixure.LogGroupNameList.Add(logGroupName);

                // wait for the flusher task to finish, which should actually proceed OK, now that we've created the expected log group.
                await tsk;
                core.Close();
            }
        }

        [Fact]
        public void TestCoreWithoutDisableLogGroupCreation()
        {
            var logGroupName = nameof(TestCoreWithoutDisableLogGroupCreation) + DateTime.UtcNow.Ticks; // this one will have to be auto-created.

            using (var client = new AmazonCloudWatchLogsClient(RegionEndpoint.USWest2))
            {
                var config = new AWSLoggerConfig(logGroupName)
                {
                    Region = RegionEndpoint.USWest2.SystemName,
                    DisableLogGroupCreation = false,
                };

                var core = new AWSLoggerCore(config, "unit");
                core.AddMessage("Test message added at " + DateTimeOffset.UtcNow.ToString());
                core.Flush();
                _testFixure.LogGroupNameList.Add(logGroupName); // let's enlist the auto-created group for deletion.
                core.Close();
            }
        }
        
        [Fact]
        public async Task TestCoreWithoutDisableLogGroupCreation_With_RetentionPolicy()
        {
            var logGroupName = nameof(TestCoreWithoutDisableLogGroupCreation) + DateTime.UtcNow.Ticks; // this one will have to be auto-created.

            using (var client = new AmazonCloudWatchLogsClient(RegionEndpoint.USWest2))
            {
                var config = new AWSLoggerConfig(logGroupName)
                {
                    Region = RegionEndpoint.USWest2.SystemName,
                    DisableLogGroupCreation = false,
                    NewLogGroupRetentionInDays = 3
                };

                var core = new AWSLoggerCore(config, "unit");
                core.AddMessage("Test message added at " + DateTimeOffset.UtcNow.ToString());
                core.Flush();
                _testFixure.LogGroupNameList.Add(logGroupName); // let's enlist the auto-created group for deletion.
                var logGroupResponse = await client.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = logGroupName
                });
                var retention = logGroupResponse.LogGroups.Find(x => x.LogGroupName == logGroupName)?.RetentionInDays;
                Assert.Equal(3,retention);
                core.Close();
            }
        }

        [Fact]
        public async Task  TestCoreWithoutDisableLogGroupCreation_With_Incorrect_RetentionPolicy()
        {
            var logGroupName = nameof(TestCoreWithoutDisableLogGroupCreation) + DateTime.UtcNow.Ticks; // this one will have to be auto-created.

            using (var client = new AmazonCloudWatchLogsClient(RegionEndpoint.USWest2))
            {
                var config = new AWSLoggerConfig(logGroupName)
                {
                    Region = RegionEndpoint.USWest2.SystemName,
                    DisableLogGroupCreation = false,
                    NewLogGroupRetentionInDays = 2
                };

                var core = new AWSLoggerCore(config, "unit");
                core.AddMessage("Test message added at " + DateTimeOffset.UtcNow.ToString());
                core.Flush();
                _testFixure.LogGroupNameList.Add(logGroupName); // let's enlist the auto-created group for deletion.
                var logGroupResponse = await client.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = logGroupName
                });
                var incorrectRetention = logGroupResponse.LogGroups.Find(x => x.LogGroupName == logGroupName)?.RetentionInDays;
                Assert.Null(incorrectRetention);
                core.Close();
            }
        }
    }
}

