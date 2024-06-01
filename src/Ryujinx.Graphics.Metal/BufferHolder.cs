using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public class BufferHolder : IDisposable
    {
        public int Size { get; }

        private readonly IntPtr _map;
        private readonly MTLBuffer _mtlBuffer;

        public BufferHolder(MTLBuffer buffer, int size)
        {
            _mtlBuffer = buffer;
            _map = buffer.Contents;

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
            Span<byte> result;

            if (_map != IntPtr.Zero)
            {
                result = GetDataStorage(offset, size);

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
                // TODO: Check if needs flush

                data[..dataSize].CopyTo(new Span<byte>((void*)(_map + offset), dataSize));

                SignalWrite(offset, dataSize);

                return;
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
