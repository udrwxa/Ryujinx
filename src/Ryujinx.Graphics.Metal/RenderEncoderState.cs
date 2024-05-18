using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct RenderEncoderState
    {
        private readonly MTLDevice _device;
        private MTLFunction? _vertexFunction = null;
        private MTLFunction? _fragmentFunction = null;
        private MTLVertexDescriptor? _vertexDescriptor = null;
        private MTLDepthStencilState? _depthStencilState = null;

        private MTLCompareFunction _depthCompareFunction = MTLCompareFunction.Always;
        private bool _depthWriteEnabled = false;

        private MTLStencilDescriptor _backFaceStencil = new();
        private MTLStencilDescriptor _frontFaceStencil = new();

        public PrimitiveTopology Topology = PrimitiveTopology.Triangles;
        public MTLCullMode CullMode = MTLCullMode.None;
        public MTLWinding Winding = MTLWinding.Clockwise;

        private MTLViewport[] _viewports = [];
        private MTLScissorRect[] _scissors = [];
        public readonly MTLViewport[] Viewports => _viewports;

        struct StateChange
        {
            public bool pipeline = false;

            public StateChange() {}
        };
        private StateChange _stateChange;

        public RenderEncoderState(MTLDevice device)
        {
            _device = device;
        }

        public unsafe void SetEncoderState(MTLRenderCommandEncoder renderCommandEncoder, MTLRenderPassDescriptor descriptor)
        {
            // Pipeline
            if (_stateChange.pipeline)
            {
                var renderPipelineDescriptor = new MTLRenderPipelineDescriptor();
                if (_vertexDescriptor != null)
                {
                    renderPipelineDescriptor.VertexDescriptor = _vertexDescriptor.Value;
                }

                if (_vertexFunction != null)
                {
                    renderPipelineDescriptor.VertexFunction = _vertexFunction.Value;
                }

                if (_fragmentFunction != null)
                {
                    renderPipelineDescriptor.FragmentFunction = _fragmentFunction.Value;
                }

                const int MaxColorAttachments = 8;
                for (int i = 0; i < MaxColorAttachments; i++)
                {
                    var renderAttachment = descriptor.ColorAttachments.Object((ulong)i);
                    if (renderAttachment.Texture != IntPtr.Zero)
                    {
                        var attachment = renderPipelineDescriptor.ColorAttachments.Object((ulong)i);
                        attachment.SetBlendingEnabled(true);
                        attachment.PixelFormat = renderAttachment.Texture.PixelFormat;
                        attachment.SourceAlphaBlendFactor = MTLBlendFactor.SourceAlpha;
                        attachment.DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
                        attachment.SourceRGBBlendFactor = MTLBlendFactor.SourceAlpha;
                        attachment.DestinationRGBBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
                    }
                }

                renderPipelineDescriptor.DepthAttachmentPixelFormat = descriptor.DepthAttachment.Texture.PixelFormat;

                var error = new NSError(IntPtr.Zero);
                var pipelineState = _device.NewRenderPipelineState(renderPipelineDescriptor, ref error);
                if (error != IntPtr.Zero)
                {
                    Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Render Pipeline State: {StringHelper.String(error.LocalizedDescription)}");
                }

                renderCommandEncoder.SetRenderPipelineState(pipelineState);
            }

            // Face culling
            renderCommandEncoder.SetCullMode(CullMode);
            renderCommandEncoder.SetFrontFacingWinding(Winding);

            // Depth and stencil
            if (_depthStencilState != null)
            {
                renderCommandEncoder.SetDepthStencilState(_depthStencilState.Value);
            }

            // Viewport and scissor
            if (_viewports.Length > 0)
            {
                fixed (MTLViewport* pMtlViewports = _viewports)
                {
                    renderCommandEncoder.SetViewports((IntPtr)pMtlViewports, (ulong)_viewports.Length);
                }
            }

            if (_scissors.Length > 0)
            {
                fixed (MTLScissorRect* pMtlScissors = _scissors)
                {
                    renderCommandEncoder.SetScissorRects((IntPtr)pMtlScissors, (ulong)_scissors.Length);
                }
            }

            // Reset state
            _stateChange = new();
        }

        public void UpdateProgram(MTLFunction vertexFunction, MTLFunction fragmentFunction)
        {
            _vertexFunction = vertexFunction;
            _fragmentFunction = fragmentFunction;

            _stateChange.pipeline = true;
        }

        public void UpdateVertexDescriptor(MTLVertexDescriptor vertexDescriptor)
        {
            _vertexDescriptor = vertexDescriptor;

            _stateChange.pipeline = true;
        }

        public MTLDepthStencilState UpdateStencilState(MTLStencilDescriptor backFace, MTLStencilDescriptor frontFace)
        {
            _backFaceStencil = backFace;
            _frontFaceStencil = frontFace;

            _depthStencilState = _device.NewDepthStencilState(new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = _depthCompareFunction,
                DepthWriteEnabled = _depthWriteEnabled,
                BackFaceStencil = _backFaceStencil,
                FrontFaceStencil = _frontFaceStencil
            });

            return _depthStencilState.Value;
        }

        public MTLDepthStencilState UpdateDepthState(MTLCompareFunction depthCompareFunction, bool depthWriteEnabled)
        {
            _depthCompareFunction = depthCompareFunction;
            _depthWriteEnabled = depthWriteEnabled;

            var state = _device.NewDepthStencilState(new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = _depthCompareFunction,
                DepthWriteEnabled = _depthWriteEnabled,
                BackFaceStencil = _backFaceStencil,
                FrontFaceStencil = _frontFaceStencil
            });

            _depthStencilState = state;

            return state;
        }

        public void UpdateScissors(MTLScissorRect[] scissors)
        {
            _scissors = scissors;
        }

        public void UpdateViewport(MTLViewport[] viewports)
        {
            _viewports = viewports;
        }
    }
}
