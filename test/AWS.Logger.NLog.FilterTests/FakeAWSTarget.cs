using System;
using NLog;
using NLog.Common;
using NLog.Targets;

namespace AWS.Logger.NLogger.FilterTests
{
    [Target("FakeAWSTarget")]
    public class FakeAWSTarget : TargetWithLayout
    {
        public FakeCoreLogger _core;

        public FakeAWSTarget(TimeSpan asyncDelay = default(TimeSpan))
        {
            _core = new FakeCoreLogger(asyncDelay);
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var message = this.Layout.Render(logEvent);
            _core.AddMessage(message);
        }

        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            try
            {
                _core.Flush();
                asyncContinuation(null);
            }
            catch (Exception ex)
            {
                asyncContinuation(ex);
            }
        }
    }
}
