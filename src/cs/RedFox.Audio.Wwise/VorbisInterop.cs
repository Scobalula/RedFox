using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.Audio.Wwise;

internal static unsafe partial class VorbisInterop
{
    private const string Library = "vorbis";

    public const int InfoSize = 256;

    public const int CommentSize = 256;

    public const int DspStateSize = 512;

    public const int BlockSize = 512;

    [LibraryImport(Library, EntryPoint = "vorbis_info_init")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial void InitializeInfo(void* info);

    [LibraryImport(Library, EntryPoint = "vorbis_info_clear")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial void ClearInfo(void* info);

    [LibraryImport(Library, EntryPoint = "vorbis_comment_init")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial void InitializeComment(void* comment);

    [LibraryImport(Library, EntryPoint = "vorbis_comment_clear")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial void ClearComment(void* comment);

    [LibraryImport(Library, EntryPoint = "vorbis_synthesis_headerin")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial int ReadHeader(void* info, void* comment, OggPacket* packet);

    [LibraryImport(Library, EntryPoint = "vorbis_synthesis_init")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial int InitializeSynthesis(void* dspState, void* info);

    [LibraryImport(Library, EntryPoint = "vorbis_block_init")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial int InitializeBlock(void* dspState, void* block);

    [LibraryImport(Library, EntryPoint = "vorbis_synthesis")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial int Synthesize(void* block, OggPacket* packet);

    [LibraryImport(Library, EntryPoint = "vorbis_synthesis_blockin")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial int BlockIn(void* dspState, void* block);

    [LibraryImport(Library, EntryPoint = "vorbis_synthesis_pcmout")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial int GetPcm(void* dspState, float*** pcm);

    [LibraryImport(Library, EntryPoint = "vorbis_synthesis_read")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial int ReadPcm(void* dspState, int samples);

    [LibraryImport(Library, EntryPoint = "vorbis_synthesis_restart")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial int Restart(void* dspState);

    [LibraryImport(Library, EntryPoint = "vorbis_block_clear")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial void ClearBlock(void* block);

    [LibraryImport(Library, EntryPoint = "vorbis_dsp_clear")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]

    public static partial void ClearDspState(void* dspState);
}
