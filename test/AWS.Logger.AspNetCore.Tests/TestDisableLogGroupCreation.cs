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
    public class TestDisableLogGroupCreation : TestConfigurationBase
    {
        [Fact]
        public void TestMissingDisableLogGroupCreation()
        {
            var config = LoggerConfigSectionSetup("disableLogGroupCreationMissing.json", null);
            var typed = new AWSLoggerConfigSection(config);
            Assert.False(typed.Config.DisableLogGroupCreation);
        }

        [Fact]
        public void TestTrueDisableLogGroupCreation()
        {
            var config = LoggerConfigSectionSetup("disableLogGroupCreationTrue.json", null);
            var typed = new AWSLoggerConfigSection(config);
            Assert.True(typed.Config.DisableLogGroupCreation);
        }
    }
}
