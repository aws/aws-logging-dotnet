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
    public class TestDontCreateLogGroup : TestConfigurationBase
    {
        [Fact]
        public void TestMissingDontCreateLogGroup()
        {
            var config = LoggerConfigSectionSetup("dontCreateLogGroupMissing.json", null);
            var typed = new AWSLoggerConfigSection(config);
            Assert.False(typed.Config.DontCreateLogGroup);
        }

        [Fact]
        public void TestTrueDontCreateLogGroup()
        {
            var config = LoggerConfigSectionSetup("dontCreateLogGroupTrue.json", null);
            var typed = new AWSLoggerConfigSection(config);
            Assert.True(typed.Config.DontCreateLogGroup);
        }
    }
}
