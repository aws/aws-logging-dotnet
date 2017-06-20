using AWS.Logger;
using System;
using System.Linq;

namespace Microsoft.Extensions.Configuration
{
    public static class ConfigurationSectionExtensions
    {
        //Default configuration block on the appsettings.json
        //Customer's information will be fetched from this block unless otherwise set.
        const string DEFAULT_BLOCK = "AWS.Logging";

        public static AWSLoggerConfigSection GetAWSLoggingConfigSection(this IConfiguration configSection, string configSectionInfoBlockName = DEFAULT_BLOCK)
        {
            var loggerConfigSection = configSection.GetSection(configSectionInfoBlockName);
            AWSLoggerConfigSection configObj = null;

            if (loggerConfigSection[AWSLoggerConfigSection.LOG_GROUP] != null)
            {
                configObj = new AWSLoggerConfigSection(loggerConfigSection);
            }

            return configObj;
        }
    }

    /// <summary>
    /// This class stores the configuration section information to connect to AWS and how the messages should be sent and the LogLevel section details
    /// </summary>
    public class AWSLoggerConfigSection
    {
        public AWSLoggerConfig Config { get; set; } = new AWSLoggerConfig();

        public IConfiguration LogLevels { get; set; } = null;

        internal const string LOG_GROUP = "LogGroup";
        internal const string CHECK_LOG_GROUP_EXISTANCE = "CheckLogGroupExistance";
        internal const string LOG_STREAM = "LogStream";
        internal const string BATCH_PUSH_INTERVAL = "BatchPushInterval";
        internal const string BATCH_PUSH_SIZE_IN_BYTES = "BatchPushSizeInBytes";
        internal const string LOG_LEVEL = "LogLevel";
        internal const string MAX_QUEUED_MESSAGES = "MaxQueuedMessages";
        internal const string LOG_STREAM_NAME_SUFFIX = "LogStreamNameSuffix";
        internal const string LIBRARY_LOG_FILE_NAME = "LibraryLogFileName";

        public AWSLoggerConfigSection(IConfiguration loggerConfigSection)
        {
            Config.LogGroup = loggerConfigSection[LOG_GROUP];

            if (loggerConfigSection[CHECK_LOG_GROUP_EXISTANCE] != null)
            {
                Config.CheckLogGroupExistance = Boolean.Parse(loggerConfigSection[CHECK_LOG_GROUP_EXISTANCE]);
            }

            if (loggerConfigSection[LOG_STREAM] != null)
            {
                Config.LogStream = loggerConfigSection[LOG_STREAM];
            }

            if (loggerConfigSection[BATCH_PUSH_INTERVAL] != null)
            {
                Config.BatchPushInterval = TimeSpan.FromMilliseconds(Int32.Parse(loggerConfigSection[BATCH_PUSH_INTERVAL]));
            }

            if (loggerConfigSection[BATCH_PUSH_SIZE_IN_BYTES] != null)
            {
                Config.BatchSizeInBytes = Int32.Parse(loggerConfigSection[BATCH_PUSH_SIZE_IN_BYTES]);
            }

            if (loggerConfigSection[MAX_QUEUED_MESSAGES] != null)
            {
                Config.MaxQueuedMessages = Int32.Parse(loggerConfigSection[MAX_QUEUED_MESSAGES]);
            }

            if (loggerConfigSection[LOG_STREAM_NAME_SUFFIX] != null)
            {
                Config.LogStreamNameSuffix = loggerConfigSection[LOG_STREAM_NAME_SUFFIX];
            }

            if (loggerConfigSection[LIBRARY_LOG_FILE_NAME] != null)
            {
                Config.LibraryLogFileName = loggerConfigSection[LIBRARY_LOG_FILE_NAME];
            }

            var logLevels = loggerConfigSection.GetSection(LOG_LEVEL);

            if (logLevels != null && logLevels.GetChildren().Count() > 0)
            {
                LogLevels = logLevels;
            }
        }
    }
}
