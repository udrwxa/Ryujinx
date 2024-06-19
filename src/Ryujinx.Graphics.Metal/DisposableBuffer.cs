using SharpMetal.Metal;
using System;

namespace Ryujinx.Graphics.Metal
{
    public readonly struct DisposableBuffer : IDisposable
    {
        public MTLBuffer Value { get; }

        public DisposableBuffer(MTLBuffer buffer)
        {
            Value = buffer;
        }

        public void Dispose()
        {
            if (Value != IntPtr.Zero)
            {
                Value.SetPurgeableState(MTLPurgeableState.Empty);
                Value.Dispose();
            }
        }
    }
}
