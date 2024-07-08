using Ryujinx.Graphics.GAL;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class VisibilityBuffer : IDisposable
    {
        private const int MaxQueriesPerBuffer = 1024;

        private const int EntrySize = 8;
        private const int BufferSize = EntrySize * MaxQueriesPerBuffer;

        private readonly BufferHolder _buffer;
        public readonly BufferHandle Handle;

        private readonly BufferManager _bufferManager;

        public VisibilityBuffer(BufferManager bufferManager)
        {
            _bufferManager = bufferManager;

            Handle = _bufferManager.CreateWithHandle(BufferSize, out _buffer);
        }

        public int AssignSequentialIndex()
        {
            return 0;
        }

        public void Dispose()
        {
            _bufferManager.Delete(Handle);
        }
    }
}
