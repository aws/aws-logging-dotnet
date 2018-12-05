using Amazon.Runtime;
using AWS.Logger.Log4net;
using log4net;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.Linq;

namespace BasicAWSCredentialsConfigurationExample
{
    public class Program
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(Program));

        private const string logLayoutPattern = "%utcdate{yyyy-MM-ddTHH:mm:ss.fffZ} [%-5level] %logger - %message%newline";
        private const string logGroup = "Log4net.BasicAWSCredentialsConfigurationExample";
        private const string awsRegion = "ap-southeast-1";

        public static void Main(string[] args)
        {
            var credentials = CreateCredentials(args);
            var appender = CreateAppender(credentials);

            //Get a reference to an existing logger name "Program" in log4net.config
            var hierarchy = (Hierarchy)log.Logger.Repository;
            var logger = hierarchy.GetLogger(nameof(Program)) as Logger;

            //Attach the CloudWatch Logs appender
            logger.AddAppender(appender);

            //Start writing log to CloudWatch Logs
            log.Info($"Check the AWS Console CloudWatch Logs console in {awsRegion} region");
            log.Info("to see messages in the log streams for the");
            log.Info($"log group {logGroup}");
        }

        private static BasicAWSCredentials CreateCredentials(string[] args)
        {
            if (args == null || !args.Any())
            {
                throw new ArgumentNullException(
                    "args",
                    "Please pass AWS API key and secret key as command line arguments"
                );
            }

            //Warning!!! This is only a simple example, not for a production code.
            var awsApiKey = args[0];
            var awsSecretKey = args[1];

            //Output values to verify that we have pass a values from command line arguments
            Console.WriteLine(
                $"{nameof(awsApiKey)}: {awsApiKey.Substring(0, 8)}, " +
                $"{nameof(awsSecretKey)}: {awsSecretKey.Substring(0, 8)}"
            );

            return new BasicAWSCredentials(awsApiKey, awsSecretKey);
        }

        private static AWSAppender CreateAppender(AWSCredentials credentials)
        {
            var patternLayout = new PatternLayout
            {
                ConversionPattern = logLayoutPattern
            };
            patternLayout.ActivateOptions();

            var appender = new AWSAppender
            {
                Layout = patternLayout,
                Credentials =credentials,
                LogGroup = logGroup,
                Region = awsRegion
            };

            appender.ActivateOptions();
            return appender;
        }
    }
}