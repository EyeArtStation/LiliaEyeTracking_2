using UnityEngine;
using UnityEngine.Rendering;
using XDPaint.Core;

namespace XDPaint.Utils
{
    public class CommandBufferBuilder
    {
        private CommandBuffer commandBuffer;
        public CommandBuffer CommandBuffer => commandBuffer;

        public void Release()
        {
            if (commandBuffer != null)
            {
                commandBuffer.Release();
                commandBuffer = null;
            }
        }

        public CommandBufferBuilder()
        {
            commandBuffer = new CommandBuffer();
        }
        
        public CommandBufferBuilder(string name)
        {
            commandBuffer = new CommandBuffer {name = name};
        }

        public CommandBufferBuilder LoadOrtho()
        {
            GL.LoadOrtho();
            return this;
        }

        public CommandBufferBuilder Clear()
        {
            commandBuffer.Clear();
            return this;
        }
        
        public CommandBufferBuilder SetRenderTarget(RenderTargetIdentifier renderTargetIdentifier)
        {
            commandBuffer.SetRenderTarget(renderTargetIdentifier);
            return this;
        }
        
        public CommandBufferBuilder ClearRenderTarget()
        {
            commandBuffer.ClearRenderTarget(false, true, Constants.Color.ClearWhite);
            return this;
        }
        
        public CommandBufferBuilder ClearRenderTarget(Color backgroundColor)
        {
            commandBuffer.ClearRenderTarget(false, true, backgroundColor);
            return this;
        }
        
        public CommandBufferBuilder ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor)
        {
            commandBuffer.ClearRenderTarget(clearDepth, clearColor, backgroundColor);
            return this;
        }

        public CommandBufferBuilder DrawMesh(Mesh mesh, Material material, params int[] passes)
        {
            foreach (var pass in passes)
            {
                commandBuffer.DrawMesh(mesh, Matrix4x4.identity, material, 0, pass);
            }
            return this;
        }

        public CommandBufferBuilder DrawMesh(Mesh mesh, Material material, params PaintPass[] passes)
        {
            foreach (var pass in passes)
            {
                commandBuffer.DrawMesh(mesh, Matrix4x4.identity, material, 0, (int)pass);
            }
            return this;
        }

        public CommandBufferBuilder DrawMesh(Mesh mesh, Material material, PaintPass pass = 0)
        {
            if ((int)pass == -1)
            {
                for (var i = 0; i < material.passCount; i++)
                {
                    commandBuffer.DrawMesh(mesh, Matrix4x4.identity, material, 0, i);
                }
            }
            else
            {
                commandBuffer.DrawMesh(mesh, Matrix4x4.identity, material, 0, (int)pass);
            }
            return this;
        }
                
        public CommandBufferBuilder DrawMeshInstanced(Mesh mesh, Material material, Matrix4x4[] matrix, params int[] passes)
        {
            foreach (var pass in passes)
            {
                commandBuffer.DrawMeshInstanced(mesh, 0, material, pass, matrix);
            }
            return this;
        }

        public CommandBufferBuilder DrawRenderer(Renderer renderer, Material material, int subMesh, params int[] passes)
        {
            foreach (var pass in passes)
            {
                commandBuffer.DrawRenderer(renderer, material, subMesh, pass);
            }
            return this;
        }
        
        public CommandBufferBuilder DrawRenderer(Renderer renderer, Material material, int subMesh, params PaintWorldPass[] passes)
        {
            foreach (var pass in passes)
            {
                commandBuffer.DrawRenderer(renderer, material, subMesh, (int)pass);
            }
            return this;
        }
        
        public CommandBufferBuilder DrawMesh(Mesh mesh, Matrix4x4 matrix4X4, Material material, int subMesh, params PaintWorldPass[] passes)
        {
            foreach (var pass in passes)
            {
                commandBuffer.DrawMesh(mesh, matrix4X4, material, subMesh, (int)pass);
            }
            return this;
        }

        public void Execute()
        {
            Graphics.ExecuteCommandBuffer(commandBuffer);
        }
    }
}