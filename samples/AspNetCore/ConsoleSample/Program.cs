using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace ConsoleSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new AWS.Logger.AWSLoggerConfig("AspNetCore.ConsoleSample");
            config.Region = "us-east-1";

            LoggerFactory logFactory = new LoggerFactory();

            logFactory.AddAWSProvider(config);
            var logger = logFactory.CreateLogger<Program>();

            logger.LogInformation("Check the AWS Console CloudWatch Logs console in us-east-1");
            logger.LogInformation("to see messages in the log streams for the");
            logger.LogInformation("log group AspNetCore.ConsoleSample");
        }
    }
}
