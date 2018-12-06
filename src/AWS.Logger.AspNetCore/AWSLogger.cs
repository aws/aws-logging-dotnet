using System;
using Microsoft.Extensions.Logging;
using AWS.Logger.Core;
using System.Text;

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

        /// <summary>
        /// Prefix log messages with scopes created with ILogger.BeginScope
        /// </summary>
        public bool IncludeScopes { get; set; }

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
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            return AWSLogScope.Push(_categoryName, state);
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
                message = formatter(state, exception);
            }
            else
            {
                message = _customFormatter(logLevel, state, exception);
            }

            if (string.IsNullOrEmpty(message) && exception == null)
                // If neither a message nor an exception are provided, don't log anything (there's nothing to log, after all).
                return;

            var scope = IncludeScopes ? AddScopeInformation() : string.Empty;
            if (exception != null && _customFormatter==null)
            {
                message = string.Concat(scope, message, Environment.NewLine, exception.ToString(), Environment.NewLine);
            }
            else
            {
                message = string.Concat(scope, message, Environment.NewLine);
            }
            _core.AddMessage(message);
        }

        private static string AddScopeInformation()
        {
            var deepestScope = AWSLogScope.Current;
            var current = deepestScope;

            var sb = new StringBuilder();
            while (current != null)
            {
                var scopeLog = $"=> {current}";
                sb.Insert(0, scopeLog);

                current = current.Parent;
                if (current != null)
                    sb.Insert(0, " ");
            }

            if (deepestScope != null)
                sb.Append(": ");

            return sb.ToString();
        }
    }
}
