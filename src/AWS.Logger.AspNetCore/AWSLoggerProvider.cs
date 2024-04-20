using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using AWS.Logger.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AWS.Logger.AspNetCore
{
    /// <summary>
    /// Implementation of the ILoggerProvider which is used to create instances of ILogger.
    /// </summary>
    public class AWSLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentDictionary<string, AWSLogger> _loggers = new ConcurrentDictionary<string, AWSLogger>();
        private IExternalScopeProvider _scopeProvider;
        private readonly IAWSLoggerCore _core;
        private readonly AWSLoggerConfigSection _configSection;
        private readonly Func<LogLevel, object, Exception, string> _customFormatter;
        private Func<string, LogLevel, bool> _customFilter;

        private static readonly string _assemblyVersion = typeof(AWSLoggerProvider).GetTypeInfo().Assembly.GetName().Version?.ToString() ?? string.Empty;
        private static readonly string _userAgentString = $"aws-logger-aspnetcore#{_assemblyVersion}";

        // Constants
        private const string DEFAULT_CATEGORY_NAME = "Default";

        /// <summary>
        /// Creates the logging provider with the configuration information to connect to AWS and how the messages should be sent.
        /// </summary>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="formatter">A custom formatter which accepts a LogLevel, a state, and an exception and returns the formatted log message.</param>
        public AWSLoggerProvider(AWSLoggerConfig config, Func<LogLevel, object, Exception, string> formatter = null)
            : this(config, LogLevel.Trace, formatter)
        {
        }

        /// <summary>
        /// Creates the logging provider with the configuration information to connect to AWS and how the messages should be sent.
        /// </summary>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="minLevel">The minimum log level for messages to be written.</param>
        /// <param name="formatter">A custom formatter which accepts a LogLevel, a state, and an exception and returns the formatted log message.</param>
        public AWSLoggerProvider(AWSLoggerConfig config, LogLevel minLevel, Func<LogLevel, object, Exception, string> formatter = null)
            : this(config, CreateLogLevelFilter(minLevel), formatter)
        {
        }

        /// <summary>
        /// Creates the logging provider with the configuration information to connect to AWS and how the messages should be sent.
        /// </summary>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="filter">A filter function that has the logger category name and log level which can be used to filter messages being sent to AWS.</param>
        /// <param name="formatter">A custom formatter which accepts a LogLevel, a state, and an exception and returns the formatted log message.</param>
        public AWSLoggerProvider(AWSLoggerConfig config, Func<string, LogLevel, bool> filter, Func<LogLevel, object, Exception, string> formatter = null)
        {
            _scopeProvider = NullExternalScopeProvider.Instance;
            _core = new AWSLoggerCore(config, _userAgentString);
            _customFilter = filter;
            _customFormatter = formatter;
        }

        /// <summary>
        /// Creates the logging provider with the configuration section information to connect to AWS and how the messages should be sent. Also contains the LogLevel details
        /// </summary>
        /// <param name="configSection">Contains configuration on how to connect to AWS and how the log messages should be sent. Also contains the LogeLevel details based upon which the filter values would be set</param>
        /// <param name="formatter">A custom formatter which accepts a LogLevel, a state, and an exception and returns the formatted log message.</param>
        public AWSLoggerProvider(AWSLoggerConfigSection configSection, Func<LogLevel, object, Exception, string> formatter) 
            : this(configSection)
        {
            _customFormatter = formatter;
        }

        /// <summary>
        /// Creates the logging provider with the configuration section information to connect to AWS and how the messages should be sent. Also contains the LogLevel details
        /// </summary>
        /// <param name="configSection">Contains configuration on how to connect to AWS and how the log messages should be sent. Also contains the LogeLevel details based upon which the filter values would be set</param>
        public AWSLoggerProvider(AWSLoggerConfigSection configSection)
        {
            _scopeProvider = configSection.IncludeScopes ? new LoggerExternalScopeProvider() : NullExternalScopeProvider.Instance;
            _configSection = configSection;
            _core = new AWSLoggerCore(_configSection.Config, _userAgentString);
        }        

        /// <summary>
        /// Called by the ILoggerFactory to create an ILogger
        /// </summary>
        /// <param name="categoryName">The category name of the logger which can be used for filtering.</param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            var name = string.IsNullOrEmpty(categoryName) ? DEFAULT_CATEGORY_NAME : categoryName;

            var filter = _customFilter;
            if (_configSection != null && filter == null)
            {
                filter = CreateConfigSectionFilter(_configSection.LogLevels, name);
            }

            return _loggers.GetOrAdd(name, loggerName => new AWSLogger(categoryName, _core, filter, _customFormatter)
            {
                ScopeProvider = _scopeProvider,
                IncludeScopes = _configSection?.IncludeScopes ?? Constants.IncludeScopesDefault,
                IncludeLogLevel = _configSection?.IncludeLogLevel ?? Constants.IncludeLogLevelDefault,
                IncludeCategory = _configSection?.IncludeCategory ?? Constants.IncludeCategoryDefault,
                IncludeEventId = _configSection?.IncludeEventId ?? Constants.IncludeEventIdDefault,
                IncludeNewline = _configSection?.IncludeNewline ?? Constants.IncludeNewlineDefault,
                IncludeException = _configSection?.IncludeException ?? Constants.IncludeExceptionDefault
            });
        }

        /// <summary>
        /// Disposes the provider.
        /// </summary>
        public void Dispose()
        {
            _core.Close();
        }

        /// <summary>
        /// Creates a simple filter based on a minimum log level.
        /// </summary>
        /// <param name="minLevel"></param>
        /// <returns></returns>
        public static Func<string, LogLevel, bool> CreateLogLevelFilter(LogLevel minLevel)
        {
            return (category, logLevel) => logLevel >= minLevel;
        }

        /// <summary>
        /// Creates a filter based upon the prefix of the category name given to the logger
        /// </summary>
        /// <param name="logLevels">Contains the configuration details of the Log levels</param>
        /// <param name="categoryName">Identifier name that is given to a logger</param>
        /// <returns></returns>
        public static Func<string, LogLevel, bool> CreateConfigSectionFilter(IConfiguration logLevels, string categoryName)
        {
            string name = categoryName;
            foreach (var prefix in GetKeyPrefixes(name))
            {
                LogLevel level;
                if (TryGetSwitch(prefix, logLevels, out level))
                {
                    return (n, l) => l >= level;
                }
            }
            return (n, l) => false;
        }


        /// <summary>
        /// This method fetches the prefix name from the supplied category name of the logger. In case of no prefix match "Default" value is returned.
        /// </summary>
        /// <param name="name">The category name parameter given to a logger</param>
        /// <returns></returns>
        private static IEnumerable<string> GetKeyPrefixes(string name)
        {
            while (!string.IsNullOrEmpty(name))
            {
                yield return name;
                var lastIndexOfDot = name.LastIndexOf('.');
                if (lastIndexOfDot == -1)
                {
                    yield return "Default";
                    break;
                }
                name = name.Substring(0, lastIndexOfDot);
            }
        }

        /// <summary>
        /// This method gets the prefix name from the function CreateConfigSectionFilter and checks if there is a filter that matches.
        /// </summary>
        /// <param name="name">The prefix name supplied by the function CreateConfigSectionFilter. The filter matching operation would be based upon this supplied value. </param>
        /// <param name="logLevels">The Configuration section supplied by the user that deals with the logLevels.</param>
        /// <param name="level">The LogLevel that was found to be a match.</param>
        /// <returns></returns>
        public static bool TryGetSwitch(string name, IConfiguration logLevels, out LogLevel level)
        {
            var switches = logLevels;
            if (switches == null)
            {
                level = LogLevel.Trace;
                return true;
            }

            var value = switches[name];
            if (string.IsNullOrEmpty(value))
            {
                level = LogLevel.None;
                return false;
            }
            else if (Enum.TryParse<LogLevel>(value, out level))
            {
                return true;
            }
            else
            {
                var message = $"Configuration value '{value}' for category '{name}' is not supported.";
                throw new InvalidOperationException(message);
            }
        }

        /// <inheritdoc />
        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;

            foreach (var logger in _loggers)
            {
                logger.Value.ScopeProvider = _scopeProvider;
            }
        }
    }
}
