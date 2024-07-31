namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class Defaults
    {
        public const string LocalNamePrefix = "temp";

        public const string PerPatchAttributePrefix = "patchAttr";
        public const string IAttributePrefix = "inAttr";
        public const string OAttributePrefix = "outAttr";

        public const string StructPrefix = "struct";

        public const string ArgumentNamePrefix = "a";

        public const string UndefinedName = "0";

        public const int MaxVertexBuffers = 16;

        public const uint ZeroBufferIndex = MaxVertexBuffers;
        public const uint ConstantBuffersIndex = MaxVertexBuffers + 1;
        public const uint StorageBuffersIndex = MaxVertexBuffers + 2;
        public const uint TexturesIndex = MaxVertexBuffers + 3;
        public const uint ImagesIndex = MaxVertexBuffers + 4;

        public const uint ConstantBuffersSetIndex = 0;
        public const uint StorageBuffersSetIndex = 1;
        public const uint TexturesSetIndex = 2;
        public const uint ImagesSetIndex = 3;

        public const int TotalClipDistances = 8;
    }
}
