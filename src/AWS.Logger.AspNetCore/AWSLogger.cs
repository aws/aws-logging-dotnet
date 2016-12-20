using System;
using Microsoft.Extensions.Logging;
using AWS.Logger.Core;

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

        /// <summary>
        /// Construct an instance of AWSLogger
        /// </summary>
        /// <param name="categoryName">The category name for the logger which can be used for filtering.</param>
        /// <param name="core">The core logger that is used to send messages to AWS.</param>
        /// <param name="filter">Filter function that will only allow messages to be sent to AWS if it returns true. If the value is null all messages are sent.</param>
        public AWSLogger(string categoryName, IAWSLoggerCore core, Func<string, LogLevel, bool> filter)
        {
            _categoryName = categoryName;
            _core = core;
            _filter = filter;
        }

        /// <summary>
        /// Currently scopes are not supported and a dummy instance of IDisposable is returned but not used.
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="state"></param>
        /// <returns></returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            return new DisposableScope();
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
            {
                return;
            }
            var message = formatter(state, exception);
            _core.AddMessage(message);
        }

        private class DisposableScope : IDisposable
        {
            public void Dispose()
            {

            }
        }
    }
}
