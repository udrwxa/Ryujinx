using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public class BufferHolder : IDisposable
    {
        public int Size { get; }

        private readonly IntPtr _map;
        private readonly MTLBuffer _mtlBuffer;

        public readonly MultiFenceHolder _waitable;

        private readonly ReaderWriterLockSlim _flushLock;
        private FenceHolder _flushFence;
        private int _flushWaiting;

        public BufferHolder(MTLBuffer buffer, int size)
        {
            _mtlBuffer = buffer;
            _map = buffer.Contents;
            _waitable = new MultiFenceHolder(size);

            Size = size;
        }

        public MTLBuffer GetBuffer()
        {
            return _mtlBuffer;
        }

        public MTLBuffer GetBuffer(bool isWrite)
        {
            if (isWrite)
            {
                SignalWrite(0, Size);
            }

            return _mtlBuffer;
        }

        public MTLBuffer GetBuffer(int offset, int size, bool isWrite)
        {
            if (isWrite)
            {
                SignalWrite(offset, size);
            }

            return _mtlBuffer;
        }

        public PinnedSpan<byte> GetData(int offset, int size)
        {
            _flushLock.EnterReadLock();

            WaitForFlushFence();

            Span<byte> result;

            if (_map != IntPtr.Zero)
            {
                result = GetDataStorage(offset, size);

                // Need to be careful here, the buffer can't be unmapped while the data is being used.
                // _buffer.IncrementReferenceCount();

                _flushLock.ExitReadLock();

                return PinnedSpan<byte>.UnsafeFromSpan(result);
            }

            throw new InvalidOperationException("The buffer is not mapped");
        }

        public unsafe Span<byte> GetDataStorage(int offset, int size)
        {
            int mappingSize = Math.Min(size, Size - offset);

            if (_map != IntPtr.Zero)
            {
                return new Span<byte>((void*)(_map + offset), mappingSize);
            }

            throw new InvalidOperationException("The buffer is not mapped.");
        }

        public unsafe void SetData(int offset, ReadOnlySpan<byte> data, Action endRenderPass = null)
        {
            int dataSize = Math.Min(data.Length, Size - offset);
            if (dataSize == 0)
            {
                return;
            }

            if (_map != IntPtr.Zero)
            {
                // If persistently mapped, set the data directly if the buffer is not currently in use.
                bool isRented = true; // _buffer.HasRentedCommandBufferDependency(_gd.CommandBufferPool);

                // If the buffer is rented, take a little more time and check if the use overlaps this handle.
                bool needsFlush = isRented && _waitable.IsBufferRangeInUse(offset, dataSize, false);

                if (!needsFlush)
                {
                    WaitForFences(offset, dataSize);

                    data[..dataSize].CopyTo(new Span<byte>((void*)(_map + offset), dataSize));

                    SignalWrite(offset, dataSize);

                    return;
                }
            }
        }

        public unsafe void SetDataUnchecked(int offset, ReadOnlySpan<byte> data)
        {
            int dataSize = Math.Min(data.Length, Size - offset);
            if (dataSize == 0)
            {
                return;
            }

            if (_map != IntPtr.Zero)
            {
                data[..dataSize].CopyTo(new Span<byte>((void*)(_map + offset), dataSize));
            }
        }

        public void SetDataUnchecked<T>(int offset, ReadOnlySpan<T> data) where T : unmanaged
        {
            SetDataUnchecked(offset, MemoryMarshal.AsBytes(data));
        }

        public static void Copy(
            Pipeline pipeline,
            MTLBuffer src,
            MTLBuffer dst,
            int srcOffset,
            int dstOffset,
            int size)
        {
            pipeline.GetOrCreateBlitEncoder().CopyFromBuffer(
                src,
                (ulong)srcOffset,
                dst,
                (ulong)dstOffset,
                (ulong)size);
        }

        public void SignalWrite(int offset, int size)
        {
            if (offset == 0 && size == Size)
            {
                // TODO: Cache converted buffers
            }
            else
            {
                // TODO: Cache converted buffers
            }
        }

        public void WaitForFences()
        {
            _waitable.WaitForFences();
        }

        public void WaitForFences(int offset, int size)
        {
            _waitable.WaitForFences(offset, size);
        }

        private void ClearFlushFence()
        {
            // Assumes _flushLock is held as writer.

            if (_flushFence != null)
            {
                if (_flushWaiting == 0)
                {
                    _flushFence.Put();
                }

                _flushFence = null;
            }
        }

        private void WaitForFlushFence()
        {
            if (_flushFence == null)
            {
                return;
            }

            // If storage has changed, make sure the fence has been reached so that the data is in place.
            _flushLock.ExitReadLock();
            _flushLock.EnterWriteLock();

            if (_flushFence != null)
            {
                var fence = _flushFence;
                Interlocked.Increment(ref _flushWaiting);

                // Don't wait in the lock.

                _flushLock.ExitWriteLock();

                fence.Wait();

                _flushLock.EnterWriteLock();

                if (Interlocked.Decrement(ref _flushWaiting) == 0)
                {
                    fence.Put();
                }

                _flushFence = null;
            }

            // Assumes the _flushLock is held as reader, returns in same state.
            _flushLock.ExitWriteLock();
            _flushLock.EnterReadLock();
        }

        public void Dispose()
        {
            if (_mtlBuffer != IntPtr.Zero)
            {
                _mtlBuffer.SetPurgeableState(MTLPurgeableState.Empty);
                _mtlBuffer.Dispose();
            }
        }
    }
}
