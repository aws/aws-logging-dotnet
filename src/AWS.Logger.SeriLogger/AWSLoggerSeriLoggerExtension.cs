using Serilog;
using Serilog.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace AWS.Logger.SeriLogger
{
    public static class AWSLoggerSeriLoggerExtension
    {
        internal const string LOG_GROUP = "Serilog:LogGroup";
        internal const string REGION = "Serilog:Region";
        internal const string PROFILE = "Serilog:Profile";
        internal const string BATCH_PUSH_INTERVAL = "Serilog:BatchPushInterval";
        internal const string BATCH_PUSH_SIZE_IN_BYTES = "Serilog:BatchPushSizeInBytes";
        internal const string MAX_QUEUED_MESSAGES = "Serilog:MaxQueuedMessages";
        internal const string LOG_STREAM_NAME_SUFFIX = "Serilog:LogStreamNameSuffix";
        internal const string LIBRARY_LOG_FILE_NAME = "Serilog:LibraryLogFileName";

        /// <summary>
        /// AWSSeriLogger target that is called when the customer is using 
        /// Serilog.Settings.Configuration to set the SeriLogger configuration
        /// using a Json input.
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static LoggerConfiguration AWSSeriLogger(
                  this LoggerSinkConfiguration loggerConfiguration,
                  IConfiguration configuration)
        {
            AWSLoggerConfig config = new AWSLoggerConfig();

            config.LogGroup = configuration[LOG_GROUP];
            if (configuration[REGION] != null)
            {
                config.Region = configuration[REGION];
            }
            if (configuration[PROFILE] != null)
            {
                config.Profile = configuration[PROFILE];
            }
            if (configuration[BATCH_PUSH_INTERVAL] != null)
            {
                config.BatchPushInterval = TimeSpan.FromMilliseconds(Int32.Parse(configuration[BATCH_PUSH_INTERVAL]));
            }
            if (configuration[BATCH_PUSH_SIZE_IN_BYTES] != null)
            {
                config.BatchSizeInBytes = Int32.Parse(configuration[BATCH_PUSH_SIZE_IN_BYTES]);
            }
            if (configuration[MAX_QUEUED_MESSAGES] != null)
            {
                config.MaxQueuedMessages = Int32.Parse(configuration[MAX_QUEUED_MESSAGES]);
            }
            if (configuration[LOG_STREAM_NAME_SUFFIX] != null)
            {
                config.LogStreamNameSuffix = configuration[LOG_STREAM_NAME_SUFFIX];
            }
            if (configuration[LIBRARY_LOG_FILE_NAME] != null)
            {
                config.LibraryLogFileName = configuration[LIBRARY_LOG_FILE_NAME];
            }
            return AWSSeriLogger(loggerConfiguration, config);
        }
        /// <summary>
        /// AWSSeriLogger target that is called when the customer
        /// explicitly creates a configuration of type AWSLoggerConfig 
        /// to set the SeriLogger configuration.
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static LoggerConfiguration AWSSeriLogger(
                  this LoggerSinkConfiguration loggerConfiguration,
                  AWSLoggerConfig configuration = null)
        {
            return loggerConfiguration.Sink(new AWSLogger(configuration));
        }
    }
}
