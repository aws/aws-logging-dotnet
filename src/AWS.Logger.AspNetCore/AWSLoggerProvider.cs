using Microsoft.Extensions.Logging;
using AWS.Logger.Core;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace AWS.Logger.AspNetCore
{
    /// <summary>
    /// Implementation of the ILoggerProvider which is used to create instances of ILogger.
    /// </summary>
    public class AWSLoggerProvider : ILoggerProvider
    {       
        private readonly IAWSLoggerCore _core;
        private readonly AWSLoggerConfigSection _configSection;
        private readonly Func<LogLevel, object, Exception, string> _customFormatter;
        private Func<string, LogLevel, bool> _filter;

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
            _core = new AWSLoggerCore(config, "ILogger");
            _filter = filter;
            _customFormatter = formatter;
        }

        public AWSLoggerProvider(AWSLoggerConfigSection configSection, Func<LogLevel, object, Exception, string> formatter = null) 
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
            _configSection = configSection;
            _core = new AWSLoggerCore(_configSection.Config, "ILogger");
        }        

        /// <summary>
        /// Called by the ILoggerFactory to create an ILogger
        /// </summary>
        /// <param name="categoryName">The category name of the logger which can be used for filtering.</param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            if (_configSection != null)
            {
                _filter = CreateConfigSectionFilter(_configSection.LogLevels, categoryName);
            }
            return new AWSLogger(categoryName, _core, _filter, _customFormatter);
        }

        /// <summary>
        /// Disposes the provider.
        /// </summary>
        public void Dispose()
        {
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
    }
}
