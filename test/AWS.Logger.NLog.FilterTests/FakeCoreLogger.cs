using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AWS.Logger.Core;
using System.Collections.Concurrent;

namespace AWS.Logger.NLogger.FilterTests
{
    public class FakeCoreLogger : IAWSLoggerCore
    {
        ConcurrentQueue<string> _pendingMessages = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> ReceivedMessages { get; private set; } = new ConcurrentQueue<string>();

        private SemaphoreSlim _flushEventCounter;
        TimeSpan _asyncDelay;

        public FakeCoreLogger(TimeSpan asyncDelay = default(TimeSpan))
        {
            _asyncDelay = asyncDelay;
        }

        public void AddMessage(string message)
        {
            if (_asyncDelay == TimeSpan.Zero)
            {
                ReceivedMessages.Enqueue(message);
            }
            else
            {
                if (_flushEventCounter == null)
                {
                    _flushEventCounter = new SemaphoreSlim(0, 1);
                }

                bool wasEmpty = _pendingMessages.IsEmpty;
                _pendingMessages.Enqueue(message);
                if (wasEmpty)
                {
                    Task.Run(async () =>
                    {
                        bool flushNow = false;
                        do
                        {
                            flushNow = await _flushEventCounter.WaitAsync(_asyncDelay);
                            while (_pendingMessages.TryDequeue(out var msg))
                            {
                                await Task.Delay(10);
                                ReceivedMessages.Enqueue(msg);
                            }
                        } while (flushNow);
                    });
                }
            }
        }

        public void Flush()
        {
            if (_flushEventCounter != null)
            {
                bool lockTaken = false;
                try
                {
                    // Ensure only one thread executes the flush operation
                    System.Threading.Monitor.TryEnter(_flushEventCounter, ref lockTaken);
                    if (lockTaken)
                    {
                        _flushEventCounter.Release();   // Premature release the Monitor-Task

                        bool flushStarted = false;
                        for (int i = 0; i < 15; ++i)
                        {
                            Task.Delay(100).Wait();
                            if (_flushEventCounter.CurrentCount == 0)
                            {
                                if (!flushStarted)
                                {
                                    // Monitor-Task has been released
                                    flushStarted = true;
                                    _flushEventCounter.Release();
                                }
                                else
                                {
                                    // Monitor-Task has completed flush, and awaiting again
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Someone else is waiting for flush, lets join
                        System.Threading.Monitor.TryEnter(_flushEventCounter, TimeSpan.FromSeconds(1.5), ref lockTaken);
                    }
                }
                finally
                {
                    if (lockTaken)
                        System.Threading.Monitor.Exit(_flushEventCounter);
                }
            }
        }

        public void Close()
        {
            Flush();
        }

        public void StartMonitor()
        {
            _flushEventCounter = new SemaphoreSlim(0, 1);
        }
    }
}
