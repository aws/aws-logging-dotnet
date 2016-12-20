using System;

using log4net.Appender;
using log4net.Core;


namespace AWS.Logger.Log4Net.FilterTests
{
    /// <summary>
    /// A mock Log4net appender that sends logging messages to a mock AWS Logger Core.
    /// </summary>
    public class FakeAWSAppender : AppenderSkeleton
    {
        public FakeCoreLogger _core = new FakeCoreLogger();

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (_core == null)
                return;

            _core.AddMessage(RenderLoggingEvent(loggingEvent));
        }
    }
}
