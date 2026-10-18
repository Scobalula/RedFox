using System.Reflection;

namespace RedFox.Tests.Graphics3D;

public class UploadTestProxy : DispatchProxy
{
    public ulong FrameIndex { get; set; } = 1;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return targetMethod?.Name switch
        {
            "get_FrameIndex" => FrameIndex,
            "get_IsDisposed" => false,
            "Dispose" => null,
            _ => throw new NotSupportedException(targetMethod?.Name),
        };
    }
}
