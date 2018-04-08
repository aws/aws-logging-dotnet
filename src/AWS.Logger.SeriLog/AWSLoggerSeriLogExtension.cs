using Serilog;
using Serilog.Formatting;
using Serilog.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace AWS.Logger.SeriLog
{
    public static class AWSLoggerSeriLogExtension
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
        public static LoggerConfiguration AWSSeriLog(
                  this LoggerSinkConfiguration loggerConfiguration,
                  IConfiguration configuration, 
                  IFormatProvider iFormatProvider = null,
                  ITextFormatter textFormatter = null )
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
            return AWSSeriLog(loggerConfiguration, config, iFormatProvider, textFormatter);
        }


        /// <summary>
        /// AWSSeriLogger target that is called when the customer
        /// explicitly creates a configuration of type AWSLoggerConfig 
        /// to set the SeriLogger configuration.
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static LoggerConfiguration AWSSeriLog(
                  this LoggerSinkConfiguration loggerConfiguration,
                   AWSLoggerConfig configuration = null,
                   IFormatProvider iFormatProvider = null, 
                   ITextFormatter textFormatter = null)
        {
            return loggerConfiguration.Sink(new AWSSink(configuration, iFormatProvider, textFormatter));
        }
    }
}
