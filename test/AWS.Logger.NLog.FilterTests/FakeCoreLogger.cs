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
        private SemaphoreSlim _flushTriggerEvent;
        private ManualResetEventSlim _flushCompletedEvent;
        private CancellationTokenSource _closeTokenSource;
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
                if (_flushTriggerEvent == null)
                {
                    _flushTriggerEvent = new SemaphoreSlim(0, 1);
                    _flushCompletedEvent = new ManualResetEventSlim(false);
                    _closeTokenSource = new CancellationTokenSource();
                    Task.Run(async () =>
                    {
                        bool flushNow = false;
                        do
                        {
                            while (_pendingMessages.TryDequeue(out var msg))
                            {
                                await Task.Delay(50);
                                ReceivedMessages.Enqueue(msg);
                            }
                            if (flushNow)
                                _flushCompletedEvent.Set();
                            flushNow = await _flushTriggerEvent.WaitAsync(_asyncDelay);
                        } while (!_closeTokenSource.IsCancellationRequested);
                    }, _closeTokenSource.Token);
                }

                _pendingMessages.Enqueue(message);
            }
        }

        public void Flush()
        {
            if (_flushTriggerEvent != null)
            {
                bool lockTaken = false;
                try
                {
                    // Ensure only one thread executes the flush operation
                    System.Threading.Monitor.TryEnter(_flushTriggerEvent, ref lockTaken);
                    if (lockTaken)
                    {
                        _flushCompletedEvent.Reset();
                        _flushTriggerEvent.Release();   // Signal Monitor-Task to start premature flush
                    }
                    _flushCompletedEvent.Wait(TimeSpan.FromSeconds(15));
                }
                finally
                {
                    if (lockTaken)
                        System.Threading.Monitor.Exit(_flushTriggerEvent);
                }
            }
        }

        public void Close()
        {
            Flush();
            _closeTokenSource.Cancel();
        }

        public void StartMonitor()
        {
            _flushTriggerEvent = new SemaphoreSlim(0, 1);
        }
    }
}
