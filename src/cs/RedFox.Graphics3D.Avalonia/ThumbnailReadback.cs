using Avalonia.Media.Imaging;

namespace RedFox.Graphics3D.Avalonia;

internal readonly record struct ThumbnailReadback(uint Buffer, nint Fence, int Width, int Height, TaskCompletionSource<Bitmap?> Completion);
