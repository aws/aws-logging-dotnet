using System;
using Microsoft.Extensions.Logging;
using AWS.Logger.Core;
using System.Text;
using System.Collections.Generic;

namespace AWS.Logger.AspNetCore
{
    /// <summary>
    /// Implementation of the Microsoft.Extensions.Logging.ILogger.
    /// </summary>
    public class AWSLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IAWSLoggerCore _core;
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly Func<LogLevel, object, Exception, string> _customFormatter;

        private bool _includeScopes;
        /// <summary>
        /// Prefix log messages with scopes created with ILogger.BeginScope
        /// </summary>
        public bool IncludeScopes
        {
            get
            {
                return this._includeScopes;
            }
            set
            {
                if(value && this.ScopeProvider == NullExternalScopeProvider.Instance)
                {
                    this.ScopeProvider = new LoggerExternalScopeProvider();
                }
                this._includeScopes = value;
            }
        }

        /// <summary>
        /// Include log level in log message
        /// </summary>
        public bool IncludeLogLevel { get; set; } = Constants.IncludeLogLevelDefault;

        /// <summary>
        /// Include category in log message
        /// </summary>
        public bool IncludeCategory { get; set; } = Constants.IncludeCategoryDefault;

        /// <summary>
        /// Include event id in log message
        /// </summary>
        public bool IncludeEventId { get; set; } = Constants.IncludeEventIdDefault;

        /// <summary>
        /// Include new line in log message
        /// </summary>
        public bool IncludeNewline { get; set; } = Constants.IncludeNewlineDefault;

        /// <summary>
        /// Include exception in log message
        /// </summary>
        public bool IncludeException { get; set; } = Constants.IncludeExceptionDefault;

        internal IExternalScopeProvider ScopeProvider { get; set; } = NullExternalScopeProvider.Instance;

        /// <summary>
        /// Construct an instance of AWSLogger
        /// </summary>
        /// <param name="categoryName">The category name for the logger which can be used for filtering.</param>
        /// <param name="core">The core logger that is used to send messages to AWS.</param>
        /// <param name="filter">Filter function that will only allow messages to be sent to AWS if it returns true. If the value is null all messages are sent.</param>
        /// <param name="customFormatter">A custom formatter which accepts a LogLevel, a state, and an exception and returns the formatted log message.</param>
        public AWSLogger(string categoryName, IAWSLoggerCore core, Func<string, LogLevel, bool> filter, Func<LogLevel, object, Exception, string> customFormatter = null)
        {
            _categoryName = categoryName;
            _core = core;
            _filter = filter;
            _customFormatter = customFormatter;

            // This is done in the constructor to ensure the logic in the setter is run during initialization.
            this.IncludeScopes = Constants.IncludeScopesDefault;
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return ScopeProvider?.Push(state) ?? NullScope.Instance;
        }

        /// <summary>
        /// Test to see if the log level is enabled for logging. This is evaluated by running the filter function passed into the constructor.
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            if (_filter == null)
                return true;
            return _filter(_categoryName, logLevel);
        }

        /// <summary>
        /// Log the message
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="logLevel"></param>
        /// <param name="eventId"></param>
        /// <param name="state"></param>
        /// <param name="exception"></param>
        /// <param name="formatter"></param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            string message;
            if (_customFormatter == null)
            {
                if (formatter == null)
                    throw new ArgumentNullException(nameof(formatter));

                var messageText = formatter(state, exception);
                // If neither a message nor an exception are provided, don't log anything (there's nothing to log, after all).
                if (string.IsNullOrEmpty(messageText) && exception == null)
                    return;

                // Format of the logged text, optional components are in {}
                //  {[LogLevel] }{ Scopes: => }{Category: }{EventId: }MessageText {Exception}{\n}
                var messageBuilder = new StringBuilder();
                Action<string> addToBuilder = token =>
                {
                    if (string.IsNullOrEmpty(token))
                        return;

                    if (messageBuilder.Length > 0)
                        messageBuilder.Append(" ");
                    messageBuilder.Append(token);
                };

                if (IncludeLogLevel)
                {
                    addToBuilder($"[{logLevel}]");
                }

                GetScopeInformation(messageBuilder);

                if (IncludeCategory)
                {
                    addToBuilder($"{_categoryName}:");
                }
                if (IncludeEventId)
                {
                    addToBuilder($"[{eventId}]:");
                }

                addToBuilder(messageText);

                if (IncludeException)
                {
                    addToBuilder($"{exception}");
                }
                if (IncludeNewline)
                {
                    messageBuilder.Append(Environment.NewLine);
                }

                message = messageBuilder.ToString();
            }
            else
            {
                message = _customFormatter(logLevel, state, exception);

                // If neither a message nor an exception are provided, don't log anything (there's nothing to log, after all).
                if (string.IsNullOrEmpty(message) && exception == null)
                    return;
            }

            _core.AddMessage(message);
        }

        private void GetScopeInformation(StringBuilder messageBuilder)
        {
            var scopeProvider = ScopeProvider;

            if (IncludeScopes && scopeProvider != null)
            {
                var initialLength = messageBuilder.Length;

                scopeProvider.ForEachScope((scope, builder) =>
                {
                    if(messageBuilder.Length > 0)
                    {
                        messageBuilder.Append(" ");
                    }
                    messageBuilder.Append(scope.ToString());
                }, (messageBuilder));

                if (messageBuilder.Length > initialLength)
                {
                    messageBuilder.Append(" =>");
                }
            }
        }
    }
}
