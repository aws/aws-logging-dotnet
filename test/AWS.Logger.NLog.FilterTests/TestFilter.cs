using Xunit;
using System;
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
            Assert.Contains("warning", proxyawsTarget._core.ReceivedMessages.ElementAt(0));
            Assert.Contains("error", proxyawsTarget._core.ReceivedMessages.ElementAt(1));
            Assert.Contains("fatal", proxyawsTarget._core.ReceivedMessages.ElementAt(2));
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

            var rule = new LoggingRule("CustomFilter", LogLevel.Warn, LogLevel.Fatal, fakeawsTarget);
            rule.FilterDefaultAction = FilterResult.Log;
            rule.Filters.Add(filter);

            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;

            var logger = LogManager.GetLogger("CustomFilter");

            logger.Trace("goodCategory|trace");
            logger.Fatal("goodCategory|fatal");

            Assert.Single(fakeawsTarget._core.ReceivedMessages);
            Assert.Contains("fatal", fakeawsTarget._core.ReceivedMessages.ElementAt(0));

            string val;
            while (!fakeawsTarget._core.ReceivedMessages.IsEmpty)
            {
                fakeawsTarget._core.ReceivedMessages.TryDequeue(out val);
            }

            logger.Trace("badCategory|trace");
            logger.Warn("badCategory|warning");

            Assert.Empty(fakeawsTarget._core.ReceivedMessages);
        }

        [Fact]
        public void AsyncFlushLogLevel()
        {
            var config = new LoggingConfiguration();
            FakeAWSTarget proxyawsTarget = new FakeAWSTarget(TimeSpan.FromSeconds(10));
            config.AddTarget("FakeAWSTarget", proxyawsTarget);
            config.AddRule(LogLevel.Warn, LogLevel.Fatal, proxyawsTarget, "FilterLogLevel");
            LogManager.Configuration = config;
            var logger = LogManager.GetLogger("FilterLogLevel");

            logger.Trace("trace");
            logger.Debug("debug");
            logger.Info("information");
            logger.Warn("warning");

            Assert.Empty(proxyawsTarget._core.ReceivedMessages);
            LogManager.Flush();
            Assert.Single(proxyawsTarget._core.ReceivedMessages);

            logger.Error("error");
            logger.Fatal("fatal");

            LogManager.Flush();

            Assert.Equal(3, proxyawsTarget._core.ReceivedMessages.Count);
            Assert.Contains("warning", proxyawsTarget._core.ReceivedMessages.ElementAt(0));
            Assert.Contains("error", proxyawsTarget._core.ReceivedMessages.ElementAt(1));
            Assert.Contains("fatal", proxyawsTarget._core.ReceivedMessages.ElementAt(2));
        }

    }
}
