using AWS.Logger;
using System;
using System.Linq;

namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// Extensions methods for IConfiguration to lookup AWS logger configuration
    /// </summary>
    public static class ConfigurationSectionExtensions
    {
        // Default configuration block on the appsettings.json
        // Customer's information will be fetched from this block unless otherwise set.
        const string DEFAULT_BLOCK = "Logging";

        // This library was originally written before logging standarized, or it at least we didn't realize it was standarized, on the "Logging" section in the config.
        // The library now uses "Logging" as the default section to look for config but to maintain backwards compatibility the package will fallback
        // AWS.Logging if a log group is not configured in the "Logging" config block".
        const string LEGACY_DEFAULT_BLOCK = "AWS.Logging";

        /// <summary>
        /// Loads the AWS Logger Configuration from the ConfigSection
        /// </summary>
        /// <param name="configSection">ConfigSection</param>
        /// <param name="configSectionInfoBlockName">ConfigSection SubPath to load from</param>
        /// <returns></returns>
        public static AWSLoggerConfigSection GetAWSLoggingConfigSection(this IConfiguration configSection, string configSectionInfoBlockName = DEFAULT_BLOCK)
        {
            var loggerConfigSection = configSection.GetSection(configSectionInfoBlockName);
            AWSLoggerConfigSection configObj = null;
            if (loggerConfigSection[AWSLoggerConfigSection.LOG_GROUP] != null)
            {
                configObj = new AWSLoggerConfigSection(loggerConfigSection);
            }
            // If the code was relying on the default config block and no log group was found then
            // check the legacy default block.
            else if(string.Equals(configSectionInfoBlockName, DEFAULT_BLOCK, StringComparison.InvariantCulture))
            {
                loggerConfigSection = configSection.GetSection(LEGACY_DEFAULT_BLOCK);
                if (loggerConfigSection[AWSLoggerConfigSection.LOG_GROUP] != null)
                {
                    configObj = new AWSLoggerConfigSection(loggerConfigSection);
                }
            }


            return configObj;
        }
    }

    /// <summary>
    /// This class stores the configuration section information to connect to AWS and how the messages should be sent and the LogLevel section details
    /// </summary>
    public class AWSLoggerConfigSection
    {
        /// <summary>
        /// Configuration options for logging messages to AWS
        /// </summary>
        public AWSLoggerConfig Config { get; set; } = new AWSLoggerConfig();

        /// <summary>
        /// Custom LogLevel Filters for <see cref="AWS.Logger.AspNetCore.AWSLoggerProvider"/>
        /// </summary>
        public IConfiguration LogLevels { get; set; } = null;

        /// <summary>
        /// Gets the <see cref="AWS.Logger.AspNetCore.AWSLogger.IncludeScopes"/> property. This determines if scopes - if they exist - are included in a log message.
        /// <para>
        /// The default is false.
        /// </para>
        /// </summary>
        public bool IncludeScopes { get; set; } = false;

        internal const string LOG_GROUP = "LogGroup";
        internal const string REGION = "Region";
        internal const string PROFILE = "Profile";
        internal const string BATCH_PUSH_INTERVAL = "BatchPushInterval";
        internal const string BATCH_PUSH_SIZE_IN_BYTES = "BatchPushSizeInBytes";
        internal const string LOG_LEVEL = "LogLevel";
        internal const string MAX_QUEUED_MESSAGES = "MaxQueuedMessages";
        internal const string LOG_STREAM_NAME_SUFFIX = "LogStreamNameSuffix";
        internal const string LOG_STREAM_NAME_PREFIX = "LogStreamNamePrefix";
        internal const string LIBRARY_LOG_FILE_NAME = "LibraryLogFileName";
        internal const string INCLUDE_SCOPES_NAME = "IncludeScopes";

        /// <summary>
        /// Construct an instance of AWSLoggerConfigSection
        /// </summary>
        /// <param name="loggerConfigSection">ConfigSection to parse</param>
        public AWSLoggerConfigSection(IConfiguration loggerConfigSection)
        {
            Config.LogGroup = loggerConfigSection[LOG_GROUP];
            if (loggerConfigSection[REGION] != null)
            {
                Config.Region = loggerConfigSection[REGION];
            }
            if (loggerConfigSection[PROFILE] != null)
            {
                Config.Profile = loggerConfigSection[PROFILE];
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
            if (loggerConfigSection[LOG_STREAM_NAME_PREFIX] != null)
            {
                Config.LogStreamNamePrefix = loggerConfigSection[LOG_STREAM_NAME_PREFIX];
            }
            if (loggerConfigSection[LIBRARY_LOG_FILE_NAME] != null)
            {
                Config.LibraryLogFileName = loggerConfigSection[LIBRARY_LOG_FILE_NAME];
            }
            if (loggerConfigSection[INCLUDE_SCOPES_NAME] != null)
            {
                this.IncludeScopes = Boolean.Parse(loggerConfigSection[INCLUDE_SCOPES_NAME]);
            }
            var logLevels = loggerConfigSection.GetSection(LOG_LEVEL);
            if (logLevels?.GetChildren().Any() == true)
            {
                this.LogLevels = logLevels;
            }
        }
    }
}
