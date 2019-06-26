using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Reflection;
using System.Linq;

namespace AWS.Logger.AspNetCore.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class TestFilter
    {
        public AWSLoggerConfigSection ConfigSection;

        public IConfiguration LoggerConfigSectionSetup(string jsonFileName,string configSectionInfoBlockName, [System.Runtime.CompilerServices.CallerFilePath]string sourceFilePath="")
        {
            var configurationBuilder = new ConfigurationBuilder()
                                       .SetBasePath(Path.GetDirectoryName(sourceFilePath))
                                       .AddJsonFile(jsonFileName);

            IConfiguration Config;
            if (configSectionInfoBlockName != null)
            {
                Config = configurationBuilder
                    .Build()
                    .GetSection(configSectionInfoBlockName);
            }

            else
            {
                Config = configurationBuilder
                      .Build()
                      .GetSection("AWS.Logging");
            }

            return Config;

        }
        [Fact]
        public void FilterLogLevel()
        {
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger("FilterLogLevel", coreLogger, AWSLoggerProvider.CreateLogLevelFilter(LogLevel.Warning));

            logger.LogTrace("trace");
            logger.LogDebug("debug");
            logger.LogInformation("information");
            logger.LogWarning("warning");
            logger.LogError("error");
            logger.LogCritical("critical");

            Assert.Equal(3, coreLogger.ReceivedMessages.Count);
            Assert.Contains("[Warning] FilterLogLevel: warning\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Error] FilterLogLevel: error\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Critical] FilterLogLevel: critical\r\n", coreLogger.ReceivedMessages);
        }

        [Fact]
        public void CustomFilter()
        {
            var coreLogger = new FakeCoreLogger();
            Func<string, LogLevel, bool> filter = (categoryName, level) =>
            {
                if (string.Equals(categoryName, "goodCategory", StringComparison.OrdinalIgnoreCase) && level >= LogLevel.Warning)
                    return true;
                return false;
            };
            var logger = new AWSLogger("goodCategory", coreLogger, filter);

            logger.LogTrace("trace");
            logger.LogWarning("warning");

            Assert.Single(coreLogger.ReceivedMessages);
            Assert.Contains("[Warning] goodCategory: warning\r\n", coreLogger.ReceivedMessages);
            string val;
            while (!coreLogger.ReceivedMessages.IsEmpty)
            {
                coreLogger.ReceivedMessages.TryDequeue(out val);
            }

            logger = new AWSLogger("badCategory", coreLogger, filter);

            logger.LogTrace("trace");
            logger.LogWarning("warning");

            Assert.Empty(coreLogger.ReceivedMessages);
        }
        [Fact]
        public void ValidAppsettingsFilter()
        {
            var configSection = LoggerConfigSectionSetup("ValidAppsettingsFilter.json",null).GetSection("LogLevel");
            if (!(configSection != null && configSection.GetChildren().Count() > 0))
            {
                configSection = null;
            }
            var categoryName = typeof(TestFilter).GetTypeInfo().FullName;
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger(
                categoryName,
                coreLogger, 
                AWSLoggerProvider.CreateConfigSectionFilter(configSection, categoryName));

            logger.LogTrace("trace");
            logger.LogDebug("debug");
            logger.LogInformation("information");
            logger.LogWarning("warning");
            logger.LogError("error");
            logger.LogCritical("critical");

            Assert.Equal(5, coreLogger.ReceivedMessages.Count);
            Assert.Contains("[Warning] AWS.Logger.AspNetCore.Tests.TestFilter: warning\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Error] AWS.Logger.AspNetCore.Tests.TestFilter: error\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Critical] AWS.Logger.AspNetCore.Tests.TestFilter: critical\r\n", coreLogger.ReceivedMessages);
        }

        [Fact]
        public void InValidAppsettingsFilter()
        {
            var configSection = LoggerConfigSectionSetup("InValidAppsettingsFilter.json",null).GetSection("LogLevel");
            if (!(configSection != null && configSection.GetChildren().Count() > 0))
            {
                configSection = null;
            }
            var categoryName = typeof(TestFilter).GetTypeInfo().FullName;
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger(
                categoryName,
                coreLogger,
                AWSLoggerProvider.CreateConfigSectionFilter(configSection, categoryName));

            logger.LogTrace("trace");
            logger.LogDebug("debug");
            logger.LogInformation("information");
            logger.LogWarning("warning");
            logger.LogError("error");
            logger.LogCritical("critical");

            Assert.Empty(coreLogger.ReceivedMessages);

            categoryName = "AWS.Log";
            logger = new AWSLogger(
                categoryName,
                coreLogger,
                AWSLoggerProvider.CreateConfigSectionFilter(configSection, categoryName));

            logger.LogTrace("trace");
            logger.LogDebug("debug");
            logger.LogInformation("information");
            logger.LogWarning("warning");
            logger.LogError("error");
            logger.LogCritical("critical");

            Assert.Equal(5, coreLogger.ReceivedMessages.Count);
            Assert.Contains("[Warning] AWS.Log: warning\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Error] AWS.Log: error\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Critical] AWS.Log: critical\r\n", coreLogger.ReceivedMessages);
        }

        [Fact]
        public void DefaultFilterCheck()
        {
            var configSection = LoggerConfigSectionSetup("DefaultFilterCheck.json", null).GetSection("LogLevel");
            if (!(configSection != null && configSection.GetChildren().Count() > 0))
            {
                configSection = null;
            }
            var categoryName = "AWS.Log";
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger(
                categoryName,
                coreLogger,
                AWSLoggerProvider.CreateConfigSectionFilter(configSection, categoryName));

            logger.LogTrace("trace");
            logger.LogDebug("debug");
            logger.LogInformation("information");
            logger.LogWarning("warning");
            logger.LogError("error");
            logger.LogCritical("critical");

            Assert.Equal(5, coreLogger.ReceivedMessages.Count);
            Assert.Contains("[Warning] AWS.Log: warning\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Error] AWS.Log: error\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Critical] AWS.Log: critical\r\n", coreLogger.ReceivedMessages);
        }

        [Fact]
        public void MissingLogLevelCheck()
        {
            var configSection = LoggerConfigSectionSetup("MissingLogLevelCheck.json", null).GetSection("LogLevel");
            if (!(configSection != null && configSection.GetChildren().Count() > 0))
            {
                configSection = null;
            }
            var categoryName = typeof(TestFilter).GetTypeInfo().FullName;
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger(
                categoryName,
                coreLogger,
                AWSLoggerProvider.CreateConfigSectionFilter(configSection, categoryName));

            logger.LogTrace("trace");
            logger.LogDebug("debug");
            logger.LogInformation("information");
            logger.LogWarning("warning");
            logger.LogError("error");
            logger.LogCritical("critical");

            Assert.Equal(6, coreLogger.ReceivedMessages.Count);
            Assert.Contains("[Warning] AWS.Logger.AspNetCore.Tests.TestFilter: warning\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Error] AWS.Logger.AspNetCore.Tests.TestFilter: error\r\n", coreLogger.ReceivedMessages);
            Assert.Contains("[Critical] AWS.Logger.AspNetCore.Tests.TestFilter: critical\r\n", coreLogger.ReceivedMessages);

        }
    }
}
