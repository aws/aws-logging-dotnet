using System;
using Serilog;

using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Microsoft.Extensions.Configuration;
using Amazon.Runtime;
using AWS.Logger.Core;
using System.Linq;
using System.Text;
using System.IO;

namespace AWS.Logger.SeriLog
{
    /// <summary>
    /// A Serilog sink that can be used with the Serilogger logging library to send messages to AWS.
    /// </summary>
    public class AWSSink: ILogEventSink
    {
        AWSLoggerCore _core = null;
        ITextFormatter _textFormatter;

        /// <summary>
        /// Default constructor
        /// </summary>
        public AWSSink()
        {
        }
        /// <summary>
        /// Constructor called by AWSLoggerSeriLoggerExtension
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="iFormatProvider"></param>
        public AWSSink(AWSLoggerConfig loggerConfiguration, ITextFormatter textFormatter)
        {
            _core = new AWSLoggerCore(loggerConfiguration, "SeriLogger");
            _textFormatter = textFormatter;
        }

        /// <summary>
        /// Method called to pass the LogEvent to the AWSLogger Sink
        /// </summary>
        /// <param name="logEvent"></param>
        public void Emit(LogEvent logEvent)
        {
            StringBuilder formattedMessage = new StringBuilder();
            var message = RenderLogEvent(logEvent);
            formattedMessage.AppendLine(message);

            if (logEvent.Exception != null)
            {
                formattedMessage.AppendLine(logEvent.Exception.ToString());
            }
            _core.AddMessage(formattedMessage.ToString());
        }

        private string RenderLogEvent(LogEvent logEvent)
        {
            using (var writer = new StringWriter())
            {
                _textFormatter.Format(logEvent, writer);
                writer.Flush();
                return writer.ToString();
            }
        }

        private class DisposableScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
