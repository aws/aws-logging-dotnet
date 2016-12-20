using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;
using log4net.Config;

namespace ConfigExample
{
    class Program
    {
        static void Main(string[] args)
        {
            // Log4net is configured in the log4net.config which adds the AWS appender.
            XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));

            ILog log = LogManager.GetLogger(typeof(Program));
            log.Info("Check the AWS Console CloudWatch Logs console in us-east-1");
            log.Info("to see messages in the log streams for the");
            log.Info("log group Log4net.ConfigExample");
        }
    }
}
