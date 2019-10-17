using System;
using Serilog;
using AWS.Logger.SeriLog;
using AWS.Logger;

namespace SerilogTestCode
{
    class Program
    {
        static void Main(string[] args)
        {
            AWSLoggerConfig configuration = new AWSLoggerConfig("Serilog.ConfigExample")
            {
                Region = "us-east-1"
            };           

            var logger = new LoggerConfiguration()
                .WriteTo.AWSSeriLog(configuration)
                .CreateLogger();

            logger.Information("Hello!");
        }
    }
}
