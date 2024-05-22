using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Texture : ITexture, IDisposable
    {
        private readonly TextureCreateInfo _info;
        private readonly Pipeline _pipeline;
        private readonly MTLDevice _device;

        public MTLTexture MTLTexture;
        public TextureCreateInfo Info => _info;
        public int Width => Info.Width;
        public int Height => Info.Height;
        public int Depth => Info.Depth;
        public MTLPixelFormat MtlPixelFormat;

        public Texture(MTLDevice device, Pipeline pipeline, TextureCreateInfo info)
        {
            _device = device;
            _pipeline = pipeline;
            _info = info;

            var format = FormatTable.GetFormat(Info.Format, _device);

            MtlPixelFormat = format;

            var descriptor = new MTLTextureDescriptor
            {
                PixelFormat = format,
                Usage = MTLTextureUsage.Unknown,
                SampleCount = (ulong)Info.Samples,
                TextureType = Info.Target.Convert(),
                Width = (ulong)Info.Width,
                Height = (ulong)Info.Height,
                MipmapLevelCount = (ulong)Info.Levels
            };

            if (info.Target == Target.Texture3D)
            {
                descriptor.Depth = (ulong)Info.Depth;
            }
            else if (info.Target != Target.Cubemap)
            {
                descriptor.ArrayLength = (ulong)Info.Depth;
            }

            descriptor.Swizzle = GetSwizzle(info, descriptor.PixelFormat);

            MTLTexture = _device.NewTexture(descriptor);
        }

        public Texture(MTLDevice device, Pipeline pipeline, TextureCreateInfo info, MTLTexture sourceTexture, int firstLayer, int firstLevel)
        {
            _device = device;
            _pipeline = pipeline;
            _info = info;

            var format = FormatTable.GetFormat(Info.Format, _device);
            var textureType = Info.Target.Convert();
            NSRange levels;
            levels.location = (ulong)firstLevel;
            levels.length = (ulong)Info.Levels;
            NSRange slices;
            slices.location = (ulong)firstLayer;
            slices.length = 1;

            if (info.Target != Target.Texture3D && info.Target != Target.Cubemap)
            {
                slices.length = (ulong)Info.Depth;
            }

            var swizzle = GetSwizzle(info, format);

            MtlPixelFormat = format;
            MTLTexture = sourceTexture.NewTextureView(format, textureType, levels, slices, swizzle);
        }

        private MTLTextureSwizzleChannels GetSwizzle(TextureCreateInfo info, MTLPixelFormat pixelFormat)
        {
            var swizzleR = Info.SwizzleR.Convert();
            var swizzleG = Info.SwizzleG.Convert();
            var swizzleB = Info.SwizzleB.Convert();
            var swizzleA = Info.SwizzleA.Convert();

            if (info.Format == Format.R5G5B5A1Unorm ||
                info.Format == Format.R5G5B5X1Unorm ||
                info.Format == Format.R5G6B5Unorm)
            {
                (swizzleB, swizzleR) = (swizzleR, swizzleB);
            }
            else if (pixelFormat == MTLPixelFormat.ABGR4Unorm || info.Format == Format.A1B5G5R5Unorm)
            {
                var tempB = swizzleB;
                var tempA = swizzleA;

                swizzleB = swizzleG;
                swizzleA = swizzleR;
                swizzleR = tempA;
                swizzleG = tempB;
            }

            return new MTLTextureSwizzleChannels
            {
                red = swizzleR,
                green = swizzleG,
                blue = swizzleB,
                alpha = swizzleA
            };
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            var blitCommandEncoder = // Invoke helper shaderGetOrCreateBlitEncoder();

            if (destination is Texture destinationTexture)
            {
                blitCommandEncoder.CopyFromTexture(
                    MTLTexture,
                    (ulong)firstLayer,
                    (ulong)firstLevel,
                    destinationTexture.MTLTexture,
                    (ulong)firstLayer,
                    (ulong)firstLevel,
                    MTLTexture.ArrayLength,
                    MTLTexture.MipmapLevelCount);
            }
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            var blitCommandEncoder = // Invoke helper shaderGetOrCreateBlitEncoder();

            if (destination is Texture destinationTexture)
            {
                blitCommandEncoder.CopyFromTexture(
                    MTLTexture,
                    (ulong)srcLayer,
                    (ulong)srcLevel,
                    destinationTexture.MTLTexture,
                    (ulong)dstLayer,
                    (ulong)dstLevel,
                    MTLTexture.ArrayLength,
                    MTLTexture.MipmapLevelCount);
            }
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            // var blitCommandEncoder = // Invoke helper shaderGetOrCreateBlitEncoder();
            //
            // if (destination is Texture destinationTexture)
            // {
            //
            // }
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            var blitCommandEncoder = // Invoke helper shaderGetOrCreateBlitEncoder();

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (MTLTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            var handle = range.Handle;
            MTLBuffer mtlBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref handle));

            blitCommandEncoder.CopyFromTexture(
                MTLTexture,
                (ulong)layer,
                (ulong)level,
                new MTLOrigin(),
                new MTLSize { width = MTLTexture.Width, height = MTLTexture.Height, depth = MTLTexture.Depth },
                mtlBuffer,
                (ulong)range.Offset,
                bytesPerRow,
                bytesPerImage);
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            return new Texture(_device, _pipeline, info, MTLTexture, firstLayer, firstLevel);
        }

        public PinnedSpan<byte> GetData()
        {
            throw new NotImplementedException();
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            throw new NotImplementedException();
        }

        // TODO: Handle array formats
        public unsafe void SetData(IMemoryOwner<byte> data)
        {
            var blitCommandEncoder = // Invoke helper shaderGetOrCreateBlitEncoder();

            var dataSpan = data.Memory.Span;
            var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
            var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
            dataSpan.CopyTo(bufferSpan);

            int width = Info.Width;
            int height = Info.Height;
            int depth = Info.Depth;
            int levels = Info.GetLevelsClamped();
            bool is3D = Info.Target == Target.Texture3D;

            int offset = 0;

            for (int level = 0; level < levels; level++)
            {
                int mipSize = Info.GetMipSize(level);

                int endOffset = offset + mipSize;

                if ((uint)endOffset > (uint)dataSpan.Length)
                {
                    return;
                }

                blitCommandEncoder.CopyFromBuffer(
                    mtlBuffer,
                    (ulong)offset,
                    (ulong)Info.GetMipStride(level),
                    (ulong)mipSize,
                    new MTLSize { width = (ulong)width, height = (ulong)height, depth = is3D ? (ulong)depth : 1 },
                    MTLTexture,
                    0,
                    (ulong)level,
                    new MTLOrigin()
                );

                offset += mipSize;

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (is3D)
                {
                    depth = Math.Max(1, depth >> 1);
                }
            }
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level)
        {
            var blitCommandEncoder = // Invoke helper shaderGetOrCreateBlitEncoder();

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (MTLTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            unsafe
            {
                var dataSpan = data.Memory.Span;
                var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
                var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
                dataSpan.CopyTo(bufferSpan);

                blitCommandEncoder.CopyFromBuffer(
                    mtlBuffer,
                    0,
                    bytesPerRow,
                    bytesPerImage,
                    new MTLSize { width = MTLTexture.Width, height = MTLTexture.Height, depth = MTLTexture.Depth },
                    MTLTexture,
                    (ulong)layer,
                    (ulong)level,
                    new MTLOrigin()
                );
            }
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level, Rectangle<int> region)
        {
            var blitCommandEncoder = // Invoke helper shaderGetOrCreateBlitEncoder();

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (MTLTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            unsafe
            {
                var dataSpan = data.Memory.Span;
                var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
                var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
                dataSpan.CopyTo(bufferSpan);

                blitCommandEncoder.CopyFromBuffer(
                    mtlBuffer,
                    0,
                    bytesPerRow,
                    bytesPerImage,
                    new MTLSize { width = (ulong)region.Width, height = (ulong)region.Height, depth = 1 },
                    MTLTexture,
                    (ulong)layer,
                    (ulong)level,
                    new MTLOrigin { x = (ulong)region.X, y = (ulong)region.Y }
                );
            }
        }

        public void SetStorage(BufferRange buffer)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        private int GetBufferDataLength(int length)
        {
            if (NeedsD24S8Conversion())
            {
                return length * 2;
            }

            return length;
        }

        private Format GetCompatibleGalFormat(Format format)
        {
            if (NeedsD24S8Conversion())
            {
                return Format.D32FloatS8Uint;
            }

            return format;
        }

        private void CopyDataToBuffer(Span<byte> storage, ReadOnlySpan<byte> input)
        {
            if (NeedsD24S8Conversion())
            {
                FormatConverter.ConvertD24S8ToD32FS8(storage, input);
                return;
            }

            input.CopyTo(storage);
        }

        private ReadOnlySpan<byte> GetDataFromBuffer(ReadOnlySpan<byte> storage, int size, Span<byte> output)
        {
            if (NeedsD24S8Conversion())
            {
                if (output.IsEmpty)
                {
                    output = new byte[GetBufferDataLength(size)];
                }

                FormatConverter.ConvertD32FS8ToD24S8(output, storage);
                return output;
            }

            return storage;
        }

        private bool PrepareOutputBuffer()
        {
            if (NeedsD24S8Conversion())
            {
                return true;
            }

            return false;
        }

        private void CopyDataToOutputBuffer()
        {
            if (NeedsD24S8Conversion())
            {
                // Invoke helper shader
            }
        }

        private bool NeedsD24S8Conversion()
        {
            return FormatTable.IsD24S8(Info.Format) && MtlPixelFormat == MTLPixelFormat.Depth32FloatStencil8;
        }

        public void Release()
        {
            Dispose();
        }

        public void Dispose()
        {
            MTLTexture.SetPurgeableState(MTLPurgeableState.Volatile);
        }
    }
}
