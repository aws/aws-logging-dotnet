using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NLog;

namespace NLog.ConfigExample
{
    class Program
    {
        static void Main(string[] args)
        {
            // NLog is configured in the NLog.config
            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Info("Check the AWS Console CloudWatch Logs console in us-east-1");
            logger.Info("to see messages in the log streams for the");
            logger.Info("log group NLog.ConfigExample");
            Console.ReadKey();
        }
    }
}
