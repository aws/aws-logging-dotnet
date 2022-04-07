﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLog;
using NLog.Targets;
using NLog.Config;

using NLog.AWS.Logger;

namespace ProgrammaticConfigurationExample
{
    class Program
    {
        static void Main(string[] args)
        {
            ConfigureNLog(args.Length > 0 ? args[0] : null);
            
            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info("Check the AWS Console CloudWatch Logs console in us-east-1");
            logger.Info("to see messages in the log streams for the");
            logger.Info("log group NLog.ProgrammaticConfigurationExample");
        }

        static void ConfigureNLog(string Key = null)
        {
            var config = new LoggingConfiguration();

            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);

            var awsTarget = new AWSTarget()
            {
                LogGroup = "NLog.ProgrammaticConfigurationExample",
                Region = "us-east-1"
            };

            if (Key != null)
            {
                awsTarget.LogStreamNameUniqueKey = Key;
                awsTarget.LogStreamNameSuffix = string.Empty;
            };

            config.AddTarget("aws", awsTarget);

            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, awsTarget));

            LogManager.Configuration = config;
        }
    }
}
