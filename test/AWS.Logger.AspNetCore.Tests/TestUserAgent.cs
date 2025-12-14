using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using AWS.Logger.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace AWS.Logger.AspNetCore.Tests
{
    public class TestUserAgent
    {
        [Fact]
        public async Task VerifyUserAgent()
        {
            var client = new AmazonCloudWatchLogsClient();

            var userAgentCaptured = new List<string>();
            client.AfterResponseEvent += (sender, args) =>
            {
                var we = args as WebServiceResponseEventArgs;
                if (we == null || we.Response is not PutLogEventsResponse)
                    return;

                var userAgent = we.Response.ResponseMetadata.Metadata["User-Agent"];
                userAgentCaptured.Add(userAgent);
            };

            var core = new AWSLoggerCore(new AWSLoggerConfig
            {
                LogGroup = "/aws/logging-tests/verify-useragent",
                PreconfiguredServiceClient = client
            }, "aws-logger-aspnetcore#0.0.0.0");

            core.AddMessage("Test message for User-Agent verification");
            core.Flush();
            
            var start = DateTime.UtcNow;
            while (DateTime.UtcNow < start.AddSeconds(10))
            {
                if (userAgentCaptured.Count == 1)
                    break;

                await Task.Delay(100);
            }

            Assert.Single(userAgentCaptured);
            Assert.Contains("ft/aws-logger-aspnetcore#0.0.0.0", userAgentCaptured[0]);

            core.AddMessage("Test message for User-Agent verification");
            core.Flush();

            start = DateTime.UtcNow;
            while (DateTime.UtcNow < start.AddSeconds(10))
            {
                if (userAgentCaptured.Count == 2)
                    break;

                await Task.Delay(100);
            }

            Assert.Equal(2, userAgentCaptured.Count);
            // The user agent should not change between calls. This is verify the bug fix https://github.com/aws/aws-logging-dotnet/issues/340
            // where the user agent string kept growing with each call. Logging library was more susceptible this because it reuses the same underlying
            // PutLogEventsRequest instance for each push to send logs.
            Assert.Equal(userAgentCaptured[0], userAgentCaptured[1]);
        }
    }
}
