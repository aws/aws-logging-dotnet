using AWS.Logger;
using System;
using System.Linq;

// Placed in the Microsoft namespaces so that the extension methods are visible whenever the owning namespace
// is declared.
namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// Extensions methods for IConfiguration to lookup AWS logger configuration
    /// </summary>
    public static class ConfigurationSectionExtensions
    {
        // Default configuration block on the appsettings.json
        // Customer's information will be fetched from this block unless otherwise set.
        private const string DEFAULT_BLOCK = "Logging";

        // This library was originally written before logging standarized, or it at least we didn't realize it was standarized, on the "Logging" section in the config.
        // The library now uses "Logging" as the default section to look for config but to maintain backwards compatibility the package will fallback
        // AWS.Logging if a log group is not configured in the "Logging" config block".
        private const string LEGACY_DEFAULT_BLOCK = "AWS.Logging";

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
        public bool IncludeScopes { get; set; } = AWS.Logger.AspNetCore.Constants.IncludeScopesDefault;

        /// <summary>
        /// Gets the <see cref="AWS.Logger.AspNetCore.AWSLogger.IncludeLogLevel"/> property. This determines if log level is included in a log message.
        /// <para>
        /// The default is true.
        /// </para>
        /// </summary>
        public bool IncludeLogLevel { get; set; } = AWS.Logger.AspNetCore.Constants.IncludeLogLevelDefault;

        /// <summary>
        /// Gets the <see cref="AWS.Logger.AspNetCore.AWSLogger.IncludeCategory"/> property. This determines if category is included in a log message.
        /// <para>
        /// The default is true.
        /// </para>
        /// </summary>
        public bool IncludeCategory { get; set; } = AWS.Logger.AspNetCore.Constants.IncludeCategoryDefault;

        /// <summary>
        /// Gets the <see cref="AWS.Logger.AspNetCore.AWSLogger.IncludeEventId"/> property. This determines if event id is included in a log message.
        /// <para>
        /// The default is false.
        /// </para>
        /// </summary>
        public bool IncludeEventId { get; set; } = AWS.Logger.AspNetCore.Constants.IncludeEventIdDefault;

        /// <summary>
        /// Gets the <see cref="AWS.Logger.AspNetCore.AWSLogger.IncludeNewline"/> property. This determines if a new line is added to the end of the log message.
        /// <para>
        /// The default is true.
        /// </para>
        /// </summary>
        public bool IncludeNewline { get; set; } = AWS.Logger.AspNetCore.Constants.IncludeNewlineDefault;

        /// <summary>
        /// Gets the <see cref="AWS.Logger.AspNetCore.AWSLogger.IncludeException"/> property. This determines if exceptions are included in a log message.
        /// <para>
        /// The default is false.
        /// </para>
        /// </summary>
        public bool IncludeException { get; set; } = AWS.Logger.AspNetCore.Constants.IncludeExceptionDefault;

        internal const string LOG_GROUP = "LogGroup";
        internal const string DISABLE_LOG_GROUP_CREATION = "DisableLogGroupCreation";
        internal const string REGION = "Region";
        internal const string SERVICEURL = "ServiceUrl";
        internal const string PROFILE = "Profile";
        internal const string PROFILE_LOCATION = "ProfilesLocation";
        internal const string BATCH_PUSH_INTERVAL = "BatchPushInterval";
        internal const string BATCH_PUSH_SIZE_IN_BYTES = "BatchPushSizeInBytes";
        internal const string LOG_LEVEL = "LogLevel";
        internal const string MAX_QUEUED_MESSAGES = "MaxQueuedMessages";
        internal const string LOG_STREAM_NAME_SUFFIX = "LogStreamNameSuffix";
        internal const string LOG_STREAM_NAME_PREFIX = "LogStreamNamePrefix";
        internal const string LIBRARY_LOG_FILE_NAME = "LibraryLogFileName";
        internal const string LIBRARY_LOG_ERRORS = "LibraryLogErrors";
        internal const string FLUSH_TIMEOUT = "FlushTimeout";

        private const string INCLUDE_LOG_LEVEL_KEY = "IncludeLogLevel";
        private const string INCLUDE_CATEGORY_KEY = "IncludeCategory";
        private const string INCLUDE_NEWLINE_KEY = "IncludeNewline";
        private const string INCLUDE_EXCEPTION_KEY = "IncludeException";
        private const string INCLUDE_EVENT_ID_KEY = "IncludeEventId";
        private const string INCLUDE_SCOPES_KEY = "IncludeScopes";

        /// <summary>
        /// Construct an instance of AWSLoggerConfigSection
        /// </summary>
        /// <param name="loggerConfigSection">ConfigSection to parse</param>
        public AWSLoggerConfigSection(IConfiguration loggerConfigSection)
        {
            Config.LogGroup = loggerConfigSection[LOG_GROUP];
            Config.DisableLogGroupCreation = loggerConfigSection.GetValue<bool>(DISABLE_LOG_GROUP_CREATION);
            if (loggerConfigSection[REGION] != null)
            {
                Config.Region = loggerConfigSection[REGION];
            }
            if (loggerConfigSection[SERVICEURL] != null)
            {
                Config.ServiceUrl = loggerConfigSection[SERVICEURL];
            }
            if (loggerConfigSection[PROFILE] != null)
            {
                Config.Profile = loggerConfigSection[PROFILE];
            }
            if (loggerConfigSection[PROFILE_LOCATION] != null)
            {
                Config.ProfilesLocation = loggerConfigSection[PROFILE_LOCATION];
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
            if (loggerConfigSection[LIBRARY_LOG_ERRORS] != null)
            {
                Config.LibraryLogErrors = Boolean.Parse(loggerConfigSection[LIBRARY_LOG_ERRORS]);
            }
            if (loggerConfigSection[FLUSH_TIMEOUT] != null)
            {
                Config.FlushTimeout = TimeSpan.FromMilliseconds(Int32.Parse(loggerConfigSection[FLUSH_TIMEOUT]));
            }

            if (loggerConfigSection[INCLUDE_LOG_LEVEL_KEY] != null)
            {
                this.IncludeLogLevel = Boolean.Parse(loggerConfigSection[INCLUDE_LOG_LEVEL_KEY]);
            }
            if (loggerConfigSection[INCLUDE_CATEGORY_KEY] != null)
            {
                this.IncludeCategory = Boolean.Parse(loggerConfigSection[INCLUDE_CATEGORY_KEY]);
            }
            if (loggerConfigSection[INCLUDE_NEWLINE_KEY] != null)
            {
                this.IncludeNewline = Boolean.Parse(loggerConfigSection[INCLUDE_NEWLINE_KEY]);
            }
            if (loggerConfigSection[INCLUDE_EXCEPTION_KEY] != null)
            {
                this.IncludeException = Boolean.Parse(loggerConfigSection[INCLUDE_EXCEPTION_KEY]);
            }
            if (loggerConfigSection[INCLUDE_EVENT_ID_KEY] != null)
            {
                this.IncludeEventId = Boolean.Parse(loggerConfigSection[INCLUDE_EVENT_ID_KEY]);
            }
            if (loggerConfigSection[INCLUDE_SCOPES_KEY] != null)
            {
                this.IncludeScopes = Boolean.Parse(loggerConfigSection[INCLUDE_SCOPES_KEY]);
            }

            var logLevels = loggerConfigSection.GetSection(LOG_LEVEL);
            if (logLevels?.GetChildren().Any() == true)
            {
                this.LogLevels = logLevels;
            }
        }
    }
}
