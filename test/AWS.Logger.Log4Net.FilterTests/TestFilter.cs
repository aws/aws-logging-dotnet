using Xunit;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using log4net.Repository.Hierarchy;
using log4net;
using log4net.Layout;
using log4net.Core;

namespace AWS.Logger.Log4Net.FilterTests
{
    public class TestFilter
    {
        static Assembly repositoryAssembly = typeof(TestFilter).GetTypeInfo().Assembly;


        [Fact]
        public void FilterLogLevel()
        {
            FakeAWSAppender awsAppender;
            ILog logger;
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository(repositoryAssembly);
            PatternLayout patternLayout = new PatternLayout();

            patternLayout.ConversionPattern = "%-4timestamp [%thread] %-5level %logger %ndc - %message%newline";
            patternLayout.ActivateOptions();

            awsAppender = new FakeAWSAppender();
            awsAppender.Layout = patternLayout;

            var filter = new log4net.Filter.LevelRangeFilter();
            filter.LevelMax = Level.Fatal;
            filter.LevelMin = Level.Warn;
            awsAppender.AddFilter(filter);

            awsAppender.ActivateOptions();

            hierarchy.Root.AddAppender(awsAppender);

            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
            logger = LogManager.GetLogger(repositoryAssembly, "FilterLogLevel");

            logger.Debug("debug");
            logger.Info("information");
            logger.Warn("warning");
            logger.Error("error");
            logger.Fatal("fatal");

            Assert.Equal(3, awsAppender._core.ReceivedMessages.Count);
            Assert.True(awsAppender._core.ReceivedMessages.ElementAt(0).Contains("warning"));
            Assert.True(awsAppender._core.ReceivedMessages.ElementAt(1).Contains("error"));
            Assert.True(awsAppender._core.ReceivedMessages.ElementAt(2).Contains("fatal"));
        }

        [Fact]
        public void CustomFilter()
        {
            FakeAWSAppender awsAppender;
            ILog logger;
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository(repositoryAssembly);
            PatternLayout patternLayout = new PatternLayout();

            patternLayout.ConversionPattern = "%logger %ndc - %message%newline";
            patternLayout.ActivateOptions();

            awsAppender = new FakeAWSAppender();
            awsAppender.Layout = patternLayout;

            var filterName = new log4net.Filter.LoggerMatchFilter();
            filterName.LoggerToMatch = "badCategory";
            filterName.AcceptOnMatch = false;
            awsAppender.AddFilter(filterName);

            awsAppender.ActivateOptions();

            hierarchy.Root.AddAppender(awsAppender);

            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;

            logger = LogManager.GetLogger(repositoryAssembly,"goodCategory");

            logger.Debug("trace");
            logger.Warn("warning");

            Assert.Equal(2, awsAppender._core.ReceivedMessages.Count);
            Assert.True(awsAppender._core.ReceivedMessages.ElementAt(1).Contains("warning"));
            string val;
            while (!awsAppender._core.ReceivedMessages.IsEmpty)
            {
                awsAppender._core.ReceivedMessages.TryDequeue(out val);
            }

            logger = LogManager.GetLogger(repositoryAssembly,"badCategory");

            logger.Debug("trace");
            logger.Warn("warning");

            Assert.Equal(0, awsAppender._core.ReceivedMessages.Count);
        }
    }
}
