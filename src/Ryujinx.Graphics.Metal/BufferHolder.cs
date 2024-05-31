using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class BufferHolder : IDisposable
    {
        private readonly MetalRenderer _renderer;
        private readonly MTLDevice _device;
        private readonly MTLBuffer _mtlBuffer;

        private CacheByRange<BufferHolder> _cachedConvertedBuffers;

        public int Size { get; }

        private readonly IntPtr _map;

        public BufferHolder(MetalRenderer renderer, MTLDevice device, MTLBuffer buffer, int size, int offset = 0)
        {
            _renderer = renderer;
            _device = device;
            _mtlBuffer = buffer;

            Size = size;

            _map = buffer.NativePtr + offset;
        }

        public BufferHandle GetHandle()
        {
            var handle = (ulong)_mtlBuffer.NativePtr;
            return Unsafe.As<ulong, BufferHandle>(ref handle);
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

        public MTLBuffer GetBuffer(int offset, int size, bool isWrite = false)
        {
            if (isWrite)
            {
                SignalWrite(offset, size);
            }

            return _mtlBuffer;
        }

        public void SignalWrite(int offset, int size)
        {
            if (offset == 0 && size == Size)
            {
                _cachedConvertedBuffers.Clear();
            }
            else
            {
                _cachedConvertedBuffers.ClearRange(offset, size);
            }
        }

        private bool BoundToRange(int offset, ref int size)
        {
            if (offset >= Size)
            {
                return false;
            }

            size = Math.Min(Size - offset, size);

            return true;
        }

        public PinnedSpan<byte> GetData(int offset, int size)
        {
            Span<byte> result;

            if (_map != IntPtr.Zero)
            {
                result = GetDataStorage(offset, size);

                return PinnedSpan<byte>.UnsafeFromSpan(result);
            }

            throw new NullReferenceException("Buffer is not mapped!");
        }

        public unsafe void SetData(int offset, ReadOnlySpan<byte> data, Action endRenderPass = null)
        {
            int dataSize = Math.Min(data.Length, Size - offset);
            if (dataSize == 0)
            {
                return;
            }

            // TODO: Mirrors

            if (_map != IntPtr.Zero)
            {
                // TODO: CHECK IF NEEDS FLUSH
                bool isRented = false;
                bool needsFlush = isRented && false;

                if (!needsFlush)
                {
                    data[..dataSize].CopyTo(new Span<byte>((void*)(_map + offset), dataSize));

                    // TODO: Mirrors

                    SignalWrite(offset, dataSize);

                    return;
                }
            }

            // TODO: Mirrors
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

        public unsafe Span<byte> GetDataStorage(int offset, int size)
        {
            int mappingSize = Math.Min(size, Size - offset);

            if (_map != IntPtr.Zero)
            {
                return new Span<byte>((void*)(_map + offset), mappingSize);
            }

            throw new InvalidOperationException("Failed to read buffer data.");
        }

        public static unsafe void Copy(
            MTLBuffer src,
            MTLBuffer dst,
            int srcOffset,
            int dstOffset,
            int size)
        {

        }

        public MTLBuffer GetBufferI8ToI16(int offset, int size)
        {
            if (!BoundToRange(offset, ref size))
            {
                return new MTLBuffer(IntPtr.Zero);
            }

            var key = new I8ToI16CacheKey(_renderer);

            if (!_cachedConvertedBuffers.TryGetValue(offset, size, key, out var holder))
            {
                holder = _renderer.BufferManager.Create((size * 2 + 3) & ~3);

                _renderer.HelperShader.ConvertI8ToI16(_renderer, this, holder, offset, size);

                key.SetBuffer(holder.GetBuffer());

                _cachedConvertedBuffers.Add(offset, size, key, holder);
            }

            return holder.GetBuffer();
        }

        public MTLBuffer GetAlignedVertexBuffer(int offset, int size, int stride, int alignment)
        {
            if (!BoundToRange(offset, ref size))
            {
                return new MTLBuffer(IntPtr.Zero);
            }

            var key = new AlignedVertexBufferCacheKey(_renderer, stride, alignment);

            if (!_cachedConvertedBuffers.TryGetValue(offset, size, key, out var holder))
            {
                int alignedStride = (stride + (alignment - 1)) & -alignment;

                // holder = _gd.BufferManager.Create(_gd, (size / stride) * alignedStride, baseType: BufferAllocationType.DeviceLocal);
                //
                // _gd.PipelineInternal.EndRenderPass();
                // _gd.HelperShader.ChangeStride(_gd, cbs, this, holder, offset, size, stride, alignedStride);

                key.SetBuffer(holder.GetBuffer());

                _cachedConvertedBuffers.Add(offset, size, key, holder);
            }

            return holder.GetBuffer();
        }

        // TODO: Topology conversion patterns
        public MTLBuffer GetBufferTopologyConversion(int offset, int size, /*IndexBufferPattern pattern, */int indexSize)
        {
            if (!BoundToRange(offset, ref size))
            {
                return new MTLBuffer(IntPtr.Zero);
            }

            var key = new TopologyConversionCacheKey(_renderer, /*pattern, */indexSize);

            if (!_cachedConvertedBuffers.TryGetValue(offset, size, key, out var holder))
            {
                // The destination index size is always I32.

                int indexCount = size / indexSize;

                // int convertedCount = pattern.GetConvertedCount(indexCount);

                // holder = _gd.BufferManager.Create(_gd, convertedCount * 4, baseType: BufferAllocationType.DeviceLocal);
                //
                // _gd.PipelineInternal.EndRenderPass();
                // _gd.HelperShader.ConvertIndexBuffer(_gd, cbs, this, holder, pattern, indexSize, offset, indexCount);

                key.SetBuffer(holder.GetBuffer());

                _cachedConvertedBuffers.Add(offset, size, key, holder);
            }

            return holder.GetBuffer();
        }

        public bool TryGetCachedConvertedBuffer(int offset, int size, ICacheKey key, out BufferHolder holder)
        {
            return _cachedConvertedBuffers.TryGetValue(offset, size, key, out holder);
        }

        public void AddCachedConvertedBuffer(int offset, int size, ICacheKey key, BufferHolder holder)
        {
            _cachedConvertedBuffers.Add(offset, size, key, holder);
        }

        public void AddCachedConvertedBufferDependency(int offset, int size, ICacheKey key, Dependency dependency)
        {
            _cachedConvertedBuffers.AddDependency(offset, size, key, dependency);
        }

        public void RemoveCachedConvertedBuffer(int offset, int size, ICacheKey key)
        {
            _cachedConvertedBuffers.Remove(offset, size, key);
        }

        public void Dispose()
        {
            // TODO: Force command flush
            // _renderer.PipelineInternal?.FlushCommandsIfWeightExceeding(_buffer, (ulong)Size);

            _mtlBuffer.SetPurgeableState(MTLPurgeableState.Empty);
            _mtlBuffer.Dispose();

            _cachedConvertedBuffers.Dispose();
        }
    }
}
