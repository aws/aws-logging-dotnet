using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AWS.Logger.Core;
using System.Collections.Concurrent;

namespace AWS.Logger.Log4Net.FilterTests
{
    public class FakeCoreLogger : IAWSLoggerCore
    {
        public ConcurrentQueue<string> ReceivedMessages { get; private set; } = new ConcurrentQueue<string>();

        public void AddMessage(string message)
        {
            ReceivedMessages.Enqueue(message);
        }

        public void Flush()
        {
        }

        public void Close()
        {
        }

        public void StartMonitor()
        {
        }
    }
}
