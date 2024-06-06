using SharpMetal.Metal;
using System;

namespace Ryujinx.Graphics.Metal
{
    public readonly struct CommandBufferScoped : IDisposable
    {
        private readonly CommandBufferPool _pool;
        public MTLCommandBuffer CommandBuffer { get; }
        public int CommandBufferIndex { get; }

        public CommandBufferScoped(CommandBufferPool pool, MTLCommandBuffer commandBuffer, int commandBufferIndex)
        {
            _pool = pool;
            CommandBuffer = commandBuffer;
            CommandBufferIndex = commandBufferIndex;
        }

        public void AddDependant()
        {
            // _pool.AddDependant(CommandBufferIndex, );
        }

        public void AddWaitable()
        {
            // _pool.AddWaitable(CommandBufferIndex, );
        }

        public void GetFence()
        {
            // return _pool.GetFence(CommandBufferIndex);
        }

        public void Dispose()
        {
            // _pool?.Return(this);
        }
    }
}
