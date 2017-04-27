using System;

using NLog.Targets;
using NLog.Config;
using NLog;

using AWS.Logger;
using AWS.Logger.Core;
using Amazon.Runtime;


namespace AWS.Logger.NLogger.FilterTests
{
    [Target("FakeAWSTarget")]
    public class FakeAWSTarget : TargetWithLayout
    {
        public FakeCoreLogger _core = new FakeCoreLogger();
        protected override void Write(LogEventInfo logEvent)
        {
            var message = this.Layout.Render(logEvent);
            _core.AddMessage(message);
        }
    }
}
