namespace RedFox.Compression.Deflate;

#pragma warning disable CS0649 // These fields are written by native zlib.

internal unsafe struct ZLibState
{
    public byte*	NextIn;
    public uint		AvailableIn;
    public uint		TotalIn;
    public byte*	NextOut;
    public uint		AvailableOut;
    public uint		TotalOut;
    public nint		Message;
    public nint		State;
    public nint		Allocate;
    public nint		Free;
    public nint		Opaque;
    public int		DataType;
    public uint		Adler;
    public uint		Reserved;
}

#pragma warning restore CS0649
