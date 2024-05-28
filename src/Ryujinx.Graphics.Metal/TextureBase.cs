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
    abstract class TextureBase
    {
        protected readonly TextureCreateInfo _info;
        protected readonly Pipeline _pipeline;
        protected readonly MTLDevice _device;

        public TextureCreateInfo Info => _info;
        public int Width => Info.Width;
        public int Height => Info.Height;
        public int Depth => Info.Depth;

        public TextureBase(MTLDevice device, Pipeline pipeline, TextureCreateInfo info)
        {
            _device = device;
            _pipeline = pipeline;
            _info = info;
        }

        public virtual MTLTexture GetHandle() => throw new NotImplementedException();
    }
}
