using System;
using System.IO;
using AWS.Logger.Core;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace AWS.Logger.SeriLog
{
    /// <summary>
    /// A Serilog sink that can be used with the Serilogger logging library to send messages to AWS.
    /// </summary>
    public class AWSSink: ILogEventSink, IDisposable
    {
        AWSLoggerCore _core = null;
        IFormatProvider _iformatDriver;
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
        public AWSSink(AWSLoggerConfig loggerConfiguration, IFormatProvider iFormatProvider = null, ITextFormatter textFormatter = null)
        {
            _core = new AWSLoggerCore(loggerConfiguration, "SeriLogger");
            _iformatDriver = iFormatProvider;
            _textFormatter = textFormatter;
        }

        /// <summary>
        /// Method called to pass the LogEvent to the AWSLogger Sink
        /// </summary>
        /// <param name="logEvent"></param>
        public void Emit(LogEvent logEvent)
        {
            var message = RenderLogEvent(logEvent);
            if (logEvent.Exception != null)
            {
                message = string.Concat(message, Environment.NewLine, logEvent.Exception.ToString(), Environment.NewLine);
            }
            else
            {
                message = string.Concat(message, Environment.NewLine);
            }
            _core.AddMessage(message);
        }

        private string RenderLogEvent(LogEvent logEvent)
        { 
            if (_iformatDriver == null && _textFormatter != null)
            {
                using (var writer = new StringWriter())
                {
                    _textFormatter.Format(logEvent, writer);
                    writer.Flush();
                    return writer.ToString();
                }
            }
            return logEvent.RenderMessage(_iformatDriver);
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        _core.Close();
                    }
                    catch (Exception)
                    {
                        // .. and as ugly as THIS is, .Dispose() methods shall not throw exceptions
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
    }
}
