using Ryujinx.Graphics.GAL;
using System;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class CounterQueueEvent : ICounterEvent
    {
        public event EventHandler<ulong> OnResult;

        public bool ClearCounter { get; private set; }
        public int QueryIndex { get; private set; }

        public bool Disposed { get; private set; }
        public bool Invalid { get; set; }

        public ulong DrawIndex { get; }

        private readonly CounterQueue _queue;
        private readonly BufferManager _bufferManager;

        private readonly object _lock = new();
        private ulong _result = ulong.MaxValue;
        private double _divisor = 1f;

        public CounterQueueEvent(CounterQueue queue, BufferManager bufferManager,, ulong drawIndex)
        {
            _queue = queue;
            _bufferManager = bufferManager;

            DrawIndex = drawIndex;
            QueryIndex = bufferManager.VisibilityBuffer.AssignSequentialIndex();
        }

        internal void Clear()
        {
            ClearCounter = true;
        }

        internal void Complete(bool withResult, double divisor)
        {
            _divisor = divisor;
        }

        internal bool TryConsume(ref ulong result, bool block, AutoResetEvent wakeSignal = null)
        {
            lock (_lock)
            {
                if (Disposed)
                {
                    return true;
                }

                if (ClearCounter)
                {
                    result = 0;
                }

                ulong queryResult;

                if (block)
                {
                    queryResult = ;
                }
                else
                {
                    if (!_.TryGetResult(out queryResult))
                    {
                        return false;
                    }
                }

                result += _divisor == 1 ? queryResult : (ulong)Math.Ceiling(queryResult / _divisor);

                _result = result;

                OnResult?.Invoke(this, result);

                Dispose();

                return true;

            }
        }

        public void Flush()
        {
            if (Disposed)
            {
                return;
            }

            // Tell the queue to process all events up to this one.
            _queue.FlushTo(this);
        }

        public bool ReserveForHostAccess()
        {
            return true;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
