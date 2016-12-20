using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;
using log4net.Repository.Hierarchy;
using log4net.Core;
using log4net.Appender;
using log4net.Layout;

using AWS.Logger.Log4net;

namespace ProgrammaticConfigurationExample
{
    class Program
    {
        static void Main(string[] args)
        {
            ConfigureLog4net();

            ILog log = LogManager.GetLogger(typeof(Program));
            log.Info("Check the AWS Console CloudWatch Logs console in us-east-1");
            log.Info("to see messages in the log streams for the");
            log.Info("log group Log4net.ProgrammaticConfigurationExample");
        }

        static void ConfigureLog4net()
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
            PatternLayout patternLayout = new PatternLayout();

            patternLayout.ConversionPattern = "%-4timestamp [%thread] %-5level %logger %ndc - %message%newline";
            patternLayout.ActivateOptions();

            AWSAppender appender = new AWSAppender();
            appender.Layout = patternLayout;

            // Set log group and region. Assume credentials will be found using the default profile or IAM credentials.
            appender.LogGroup = "Log4net.ProgrammaticConfigurationExample";
            appender.Region = "us-east-1";

            appender.ActivateOptions();
            hierarchy.Root.AddAppender(appender);

            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
        }
    }
}
