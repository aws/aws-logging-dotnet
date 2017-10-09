using System;
using Serilog;

using Serilog.Core;
using Serilog.Events;
using Microsoft.Extensions.Configuration;
using Amazon.Runtime;
using AWS.Logger.Core;
using System.Linq;
using System.Text;

namespace AWS.Logger.SeriLogger
{
    /// <summary>
    /// A Serilog sink that can be used with the Serilogger logging library to send messages to AWS.
    /// </summary>
    public class AWSLogger: ILogEventSink
    {
        AWSLoggerCore _core = null;
        IFormatProvider _iformatDriver;

        /// <summary>
        /// Default constructor
        /// </summary>
        public AWSLogger()
        {
        }
        /// <summary>
        /// Constructor called by AWSLoggerSeriLoggerExtension
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="iFormatProvider"></param>
        public AWSLogger(AWSLoggerConfig loggerConfiguration,IFormatProvider iFormatProvider = null)
        {
            _core = new AWSLoggerCore(loggerConfiguration, "SeriLogger");
            _iformatDriver = iFormatProvider;
        }

        /// <summary>
        /// Method called to pass the LogEvent to the AWSLogger Sink
        /// </summary>
        /// <param name="logEvent"></param>
        public void Emit(LogEvent logEvent)
        {
            StringBuilder formattedMessage = new StringBuilder();
            var message = logEvent.RenderMessage(_iformatDriver);
            formattedMessage.AppendLine(message);
            if (logEvent.Exception != null)
            {
                formattedMessage.AppendLine(logEvent.Exception.ToString());
            }
            _core.AddMessage(formattedMessage.ToString());
        }

        private class DisposableScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
