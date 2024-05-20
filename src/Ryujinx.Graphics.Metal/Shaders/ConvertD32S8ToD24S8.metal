#include <metal_stdlib>

using namespace metal;

struct StrideArguments {
    int pixelCount;
    int dstStartOffset;
};

struct InData {
    device const uint *in_data [[id(1)]];
};

struct OutData {
    device uint *out_data [[id(2)]];
};

kernel void stride_copy(constant StrideArguments& args [[buffer(0)]],
                        device const uint* in_data [[buffer(1)]],
                        device uint* out_data [[buffer(2)]],
                        uint3 gid [[thread_position_in_grid]],
                        uint3 groupSize [[threads_per_grid]])
{
    // Determine what slice of the stride copies this invocation will perform.
    int invocations = int(groupSize.x * groupSize.y * groupSize.z);
    int copiesRequired = args.pixelCount;

    // Find the copies that this invocation should perform.

    // - Copies that all invocations perform.
    int allInvocationCopies = copiesRequired / invocations;

    // - Extra remainder copy that this invocation performs.
    int index = int(gid.x);
    int extra = (index < (copiesRequired % invocations)) ? 1 : 0;

    int copyCount = allInvocationCopies + extra;

    // Finally, get the starting offset. Make sure to count extra copies.
    int startCopy = allInvocationCopies * index + min(copiesRequired % invocations, index);

    int srcOffset = startCopy * 2;
    int dstOffset = args.dstStartOffset + startCopy;

    // Perform the conversion for this region.
    for (int i = 0; i < copyCount; i++)
    {
        float depth = uintBitsToFloat(in_data[srcOffset++]);
        uint stencil = in_data[srcOffset++];

        uint rescaledDepth = uint(clamp(depth, 0.0f, 1.0f) * 16777215.0f);

        out_data[dstOffset++] = (rescaledDepth << 8) | (stencil & 0xff);
    }
}
