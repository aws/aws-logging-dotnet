using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AWS.Logger.Core;
using System.Collections.Concurrent;

namespace AWS.Logger.AspNetCore.Tests
{
   
    /// <summary>
    /// FakeCoreLogger class used to make mock test calls instead of the actual AWS CloudWatchLogs.
    /// Implements the IAWSLoggerCore interface of AWS Logger Core
    /// </summary>
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
