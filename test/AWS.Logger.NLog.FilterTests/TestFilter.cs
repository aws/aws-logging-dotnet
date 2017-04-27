using Xunit;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using NLog.Config;
using NLog;
using NLog.Filters;

namespace AWS.Logger.NLogger.FilterTests
{
    public class TestFilter
    {
        [Fact]
        public void FilterLogLevel()
        {
            var config = new LoggingConfiguration();
            FakeAWSTarget proxyawsTarget = new FakeAWSTarget();
            config.AddTarget("FakeAWSTarget", proxyawsTarget);
            config.AddRule(LogLevel.Warn, LogLevel.Fatal, proxyawsTarget, "FilterLogLevel");
            LogManager.Configuration = config;
            var logger = LogManager.GetLogger("FilterLogLevel");



            logger.Trace("trace");
            logger.Debug("debug");
            logger.Info("information");
            logger.Warn("warning");
            logger.Error("error");
            logger.Fatal("fatal");

            Assert.Equal(3, proxyawsTarget._core.ReceivedMessages.Count);
            Assert.True(proxyawsTarget._core.ReceivedMessages.ElementAt(0).Contains("warning"));
            Assert.True(proxyawsTarget._core.ReceivedMessages.ElementAt(1).Contains("error"));
            Assert.True(proxyawsTarget._core.ReceivedMessages.ElementAt(2).Contains("fatal"));
        }

        [Fact]
        public void CustomFilter()
        {

            var filter = new ConditionBasedFilter();
            filter.Condition = "starts-with('${message}','badCategory')";
            filter.Action = FilterResult.Ignore;

            FakeAWSTarget fakeawsTarget = new FakeAWSTarget();
            var config = new LoggingConfiguration();
            config.AddTarget("FakeAWSTarget", fakeawsTarget);

            var rule = new LoggingRule("CustomFilter", LogLevel.Warn,LogLevel.Fatal, fakeawsTarget);
            rule.Filters.Add(filter);

            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;

            var logger = LogManager.GetLogger("CustomFilter");

            logger.Trace("goodCategory|trace");
            logger.Fatal("goodCategory|fatal");

            Assert.Equal(1, fakeawsTarget._core.ReceivedMessages.Count);
            Assert.True(fakeawsTarget._core.ReceivedMessages.ElementAt(0).Contains("fatal"));
            string val;
            while (!fakeawsTarget._core.ReceivedMessages.IsEmpty)
            {
                fakeawsTarget._core.ReceivedMessages.TryDequeue(out val);
            }

            logger.Trace("badCategory|trace");
            logger.Warn("badCategory|warning");

            Assert.Equal(0, fakeawsTarget._core.ReceivedMessages.Count);
        }
        
    }
}
