using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics2D
{
    /// <summary>
    /// Specifies the pixel format of an image. Based on DXGI formats, with .NET-style names.
    /// </summary>
    public enum ImageFormat : uint
    {
        /// <summary>
        /// Unknown or undefined format.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 32-bit float RGBA, 128 bits per pixel, typeless.
        /// </summary>
        R32G32B32A32Typeless = 1,

        /// <summary>
        /// 32-bit float RGBA, 128 bits per pixel.
        /// </summary>
        R32G32B32A32Float = 2,

        /// <summary>
        /// 32-bit unsigned integer RGBA, 128 bits per pixel.
        /// </summary>
        R32G32B32A32Uint = 3,

        /// <summary>
        /// 32-bit signed integer RGBA, 128 bits per pixel.
        /// </summary>
        R32G32B32A32Sint = 4,

        /// <summary>
        /// 32-bit float RGB, 96 bits per pixel, typeless.
        /// </summary>
        R32G32B32Typeless = 5,

        /// <summary>
        /// 32-bit float RGB, 96 bits per pixel.
        /// </summary>
        R32G32B32Float = 6,

        /// <summary>
        /// 32-bit unsigned integer RGB, 96 bits per pixel.
        /// </summary>
        R32G32B32Uint = 7,

        /// <summary>
        /// 32-bit signed integer RGB, 96 bits per pixel.
        /// </summary>
        R32G32B32Sint = 8,

        /// <summary>
        /// 16-bit RGBA, 64 bits per pixel, typeless.
        /// </summary>
        R16G16B16A16Typeless = 9,

        /// <summary>
        /// 16-bit float RGBA, 64 bits per pixel.
        /// </summary>
        R16G16B16A16Float = 10,

        /// <summary>
        /// 16-bit unsigned normalized RGBA, 64 bits per pixel.
        /// </summary>
        R16G16B16A16Unorm = 11,

        /// <summary>
        /// 16-bit unsigned integer RGBA, 64 bits per pixel.
        /// </summary>
        R16G16B16A16Uint = 12,

        /// <summary>
        /// 16-bit signed normalized RGBA, 64 bits per pixel.
        /// </summary>
        R16G16B16A16Snorm = 13,

        /// <summary>
        /// 16-bit signed integer RGBA, 64 bits per pixel.
        /// </summary>
        R16G16B16A16Sint = 14,

        /// <summary>
        /// 32-bit RG, 64 bits per pixel, typeless.
        /// </summary>
        R32G32Typeless = 15,

        /// <summary>
        /// 32-bit float RG, 64 bits per pixel.
        /// </summary>
        R32G32Float = 16,

        /// <summary>
        /// 32-bit unsigned integer RG, 64 bits per pixel.
        /// </summary>
        R32G32Uint = 17,

        /// <summary>
        /// 32-bit signed integer RG, 64 bits per pixel.
        /// </summary>
        R32G32Sint = 18,

        /// <summary>
        /// 32-bit float + 8-bit stencil, 40 bits per pixel, typeless.
        /// </summary>
        R32G8X24Typeless = 19,

        /// <summary>
        /// 32-bit float depth + 8-bit stencil, 40 bits per pixel.
        /// </summary>
        D32FloatS8X24Uint = 20,

        /// <summary>
        /// 32-bit float + 8-bit typeless, 40 bits per pixel.
        /// </summary>
        R32FloatX8X24Typeless = 21,

        /// <summary>
        /// 32-bit typeless + 8-bit unsigned integer, 40 bits per pixel.
        /// </summary>
        X32TypelessG8X24Uint = 22,

        /// <summary>
        /// 10-bit RGB, 2-bit alpha, typeless, 32 bits per pixel.
        /// </summary>
        R10G10B10A2Typeless = 23,

        /// <summary>
        /// 10-bit RGB, 2-bit alpha, unsigned normalized, 32 bits per pixel.
        /// </summary>
        R10G10B10A2Unorm = 24,

        /// <summary>
        /// 10-bit RGB, 2-bit alpha, unsigned integer, 32 bits per pixel.
        /// </summary>
        R10G10B10A2Uint = 25,

        /// <summary>
        /// 11-bit R, 11-bit G, 10-bit B, float, 32 bits per pixel.
        /// </summary>
        R11G11B10Float = 26,

        /// <summary>
        /// 8-bit RGBA, 32 bits per pixel, typeless.
        /// </summary>
        R8G8B8A8Typeless = 27,

        /// <summary>
        /// 8-bit RGBA, 32 bits per pixel, unsigned normalized.
        /// </summary>
        R8G8B8A8Unorm = 28,

        /// <summary>
        /// 8-bit RGBA, 32 bits per pixel, unsigned normalized, sRGB.
        /// </summary>
        R8G8B8A8UnormSrgb = 29,

        /// <summary>
        /// 8-bit RGBA, 32 bits per pixel, unsigned integer.
        /// </summary>
        R8G8B8A8Uint = 30,

        /// <summary>
        /// 8-bit RGBA, 32 bits per pixel, signed normalized.
        /// </summary>
        R8G8B8A8Snorm = 31,

        /// <summary>
        /// 8-bit RGBA, 32 bits per pixel, signed integer.
        /// </summary>
        R8G8B8A8Sint = 32,

        /// <summary>
        /// 16-bit RG, 32 bits per pixel, typeless.
        /// </summary>
        R16G16Typeless = 33,

        /// <summary>
        /// 16-bit float RG, 32 bits per pixel.
        /// </summary>
        R16G16Float = 34,

        /// <summary>
        /// 16-bit unsigned normalized RG, 32 bits per pixel.
        /// </summary>
        R16G16Unorm = 35,

        /// <summary>
        /// 16-bit unsigned integer RG, 32 bits per pixel.
        /// </summary>
        R16G16Uint = 36,

        /// <summary>
        /// 16-bit signed normalized RG, 32 bits per pixel.
        /// </summary>
        R16G16Snorm = 37,

        /// <summary>
        /// 16-bit signed integer RG, 32 bits per pixel.
        /// </summary>
        R16G16Sint = 38,

        /// <summary>
        /// 32-bit typeless, 32 bits per pixel.
        /// </summary>
        R32Typeless = 39,

        /// <summary>
        /// 32-bit float depth, 32 bits per pixel.
        /// </summary>
        D32Float = 40,

        /// <summary>
        /// 32-bit float R, 32 bits per pixel.
        /// </summary>
        R32Float = 41,

        /// <summary>
        /// 32-bit unsigned integer R, 32 bits per pixel.
        /// </summary>
        R32Uint = 42,

        /// <summary>
        /// 32-bit signed integer R, 32 bits per pixel.
        /// </summary>
        R32Sint = 43,

        /// <summary>
        /// 24-bit depth + 8-bit stencil, typeless, 32 bits per pixel.
        /// </summary>
        R24G8Typeless = 44,

        /// <summary>
        /// 24-bit unsigned normalized depth + 8-bit unsigned integer stencil, 32 bits per pixel.
        /// </summary>
        D24UnormS8Uint = 45,

        /// <summary>
        /// 24-bit unsigned normalized depth + 8-bit typeless, 32 bits per pixel.
        /// </summary>
        R24UnormX8Typeless = 46,

        /// <summary>
        /// 24-bit typeless + 8-bit unsigned integer stencil, 32 bits per pixel.
        /// </summary>
        X24TypelessG8Uint = 47,

        /// <summary>
        /// 8-bit RG, 16 bits per pixel, typeless.
        /// </summary>
        R8G8Typeless = 48,

        /// <summary>
        /// 8-bit unsigned normalized RG, 16 bits per pixel.
        /// </summary>
        R8G8Unorm = 49,

        /// <summary>
        /// 8-bit unsigned integer RG, 16 bits per pixel.
        /// </summary>
        R8G8Uint = 50,

        /// <summary>
        /// 8-bit signed normalized RG, 16 bits per pixel.
        /// </summary>
        R8G8Snorm = 51,

        /// <summary>
        /// 8-bit signed integer RG, 16 bits per pixel.
        /// </summary>
        R8G8Sint = 52,

        /// <summary>
        /// 16-bit typeless, 16 bits per pixel.
        /// </summary>
        R16Typeless = 53,

        /// <summary>
        /// 16-bit float R, 16 bits per pixel.
        /// </summary>
        R16Float = 54,

        /// <summary>
        /// 16-bit unsigned normalized depth, 16 bits per pixel.
        /// </summary>
        D16Unorm = 55,

        /// <summary>
        /// 16-bit unsigned normalized R, 16 bits per pixel.
        /// </summary>
        R16Unorm = 56,

        /// <summary>
        /// 16-bit unsigned integer R, 16 bits per pixel.
        /// </summary>
        R16Uint = 57,

        /// <summary>
        /// 16-bit signed normalized R, 16 bits per pixel.
        /// </summary>
        R16Snorm = 58,

        /// <summary>
        /// 16-bit signed integer R, 16 bits per pixel.
        /// </summary>
        R16Sint = 59,

        /// <summary>
        /// 8-bit typeless, 8 bits per pixel.
        /// </summary>
        R8Typeless = 60,

        /// <summary>
        /// 8-bit unsigned normalized R, 8 bits per pixel.
        /// </summary>
        R8Unorm = 61,

        /// <summary>
        /// 8-bit unsigned integer R, 8 bits per pixel.
        /// </summary>
        R8Uint = 62,

        /// <summary>
        /// 8-bit signed normalized R, 8 bits per pixel.
        /// </summary>
        R8Snorm = 63,

        /// <summary>
        /// 8-bit signed integer R, 8 bits per pixel.
        /// </summary>
        R8Sint = 64,

        /// <summary>
        /// 8-bit unsigned normalized alpha, 8 bits per pixel.
        /// </summary>
        A8Unorm = 65,

        /// <summary>
        /// 1-bit unsigned normalized R, 1 bit per pixel.
        /// </summary>
        R1Unorm = 66,

        /// <summary>
        /// 9-bit RGB with shared 5-bit exponent, 32 bits per pixel.
        /// </summary>
        R9G9B9E5SharedExp = 67,

        /// <summary>
        /// 8-bit packed RGBG, 32 bits per pixel.
        /// </summary>
        R8G8B8G8Unorm = 68,

        /// <summary>
        /// 8-bit packed GRGB, 32 bits per pixel.
        /// </summary>
        G8R8G8B8Unorm = 69,

        /// <summary>
        /// BC1 block-compressed, typeless, 4 bits per pixel.
        /// </summary>
        BC1Typeless = 70,

        /// <summary>
        /// BC1 block-compressed, unsigned normalized, 4 bits per pixel.
        /// </summary>
        BC1Unorm = 71,

        /// <summary>
        /// BC1 block-compressed, unsigned normalized, sRGB, 4 bits per pixel.
        /// </summary>
        BC1UnormSrgb = 72,

        /// <summary>
        /// BC2 block-compressed, typeless, 8 bits per pixel.
        /// </summary>
        BC2Typeless = 73,

        /// <summary>
        /// BC2 block-compressed, unsigned normalized, 8 bits per pixel.
        /// </summary>
        BC2Unorm = 74,

        /// <summary>
        /// BC2 block-compressed, unsigned normalized, sRGB, 8 bits per pixel.
        /// </summary>
        BC2UnormSrgb = 75,

        /// <summary>
        /// BC3 block-compressed, typeless, 8 bits per pixel.
        /// </summary>
        BC3Typeless = 76,

        /// <summary>
        /// BC3 block-compressed, unsigned normalized, 8 bits per pixel.
        /// </summary>
        BC3Unorm = 77,

        /// <summary>
        /// BC3 block-compressed, unsigned normalized, sRGB, 8 bits per pixel.
        /// </summary>
        BC3UnormSrgb = 78,

        /// <summary>
        /// BC4 block-compressed, typeless, 4 bits per pixel.
        /// </summary>
        BC4Typeless = 79,

        /// <summary>
        /// BC4 block-compressed, unsigned normalized, 4 bits per pixel.
        /// </summary>
        BC4Unorm = 80,

        /// <summary>
        /// BC4 block-compressed, signed normalized, 4 bits per pixel.
        /// </summary>
        BC4Snorm = 81,

        /// <summary>
        /// BC5 block-compressed, typeless, 8 bits per pixel.
        /// </summary>
        BC5Typeless = 82,

        /// <summary>
        /// BC5 block-compressed, unsigned normalized, 8 bits per pixel.
        /// </summary>
        BC5Unorm = 83,

        /// <summary>
        /// BC5 block-compressed, signed normalized, 8 bits per pixel.
        /// </summary>
        BC5Snorm = 84,

        /// <summary>
        /// 5-bit B, 6-bit G, 5-bit R, unsigned normalized, 16 bits per pixel.
        /// </summary>
        B5G6R5Unorm = 85,

        /// <summary>
        /// 5-bit B, 5-bit G, 5-bit R, 1-bit A, unsigned normalized, 16 bits per pixel.
        /// </summary>
        B5G5R5A1Unorm = 86,

        /// <summary>
        /// 8-bit BGRA, unsigned normalized, 32 bits per pixel.
        /// </summary>
        B8G8R8A8Unorm = 87,

        /// <summary>
        /// 8-bit BGRX, unsigned normalized, 32 bits per pixel.
        /// </summary>
        B8G8R8X8Unorm = 88,

        /// <summary>
        /// 10-bit RGB, 2-bit alpha, unsigned normalized, XR bias, 32 bits per pixel.
        /// </summary>
        R10G10B10XrBiasA2Unorm = 89,

        /// <summary>
        /// 8-bit BGRA, typeless, 32 bits per pixel.
        /// </summary>
        B8G8R8A8Typeless = 90,

        /// <summary>
        /// 8-bit BGRA, unsigned normalized, sRGB, 32 bits per pixel.
        /// </summary>
        B8G8R8A8UnormSrgb = 91,

        /// <summary>
        /// 8-bit BGRX, typeless, 32 bits per pixel.
        /// </summary>
        B8G8R8X8Typeless = 92,

        /// <summary>
        /// 8-bit BGRX, unsigned normalized, sRGB, 32 bits per pixel.
        /// </summary>
        B8G8R8X8UnormSrgb = 93,

        /// <summary>
        /// BC6H block-compressed, typeless, 8 bits per pixel.
        /// </summary>
        BC6HTypeless = 94,

        /// <summary>
        /// BC6H block-compressed, unsigned float16, 8 bits per pixel.
        /// </summary>
        BC6HUF16 = 95,

        /// <summary>
        /// BC6H block-compressed, signed float16, 8 bits per pixel.
        /// </summary>
        BC6HSF16 = 96,

        /// <summary>
        /// BC7 block-compressed, typeless, 8 bits per pixel.
        /// </summary>
        BC7Typeless = 97,

        /// <summary>
        /// BC7 block-compressed, unsigned normalized, 8 bits per pixel.
        /// </summary>
        BC7Unorm = 98,

        /// <summary>
        /// BC7 block-compressed, unsigned normalized, sRGB, 8 bits per pixel.
        /// </summary>
        BC7UnormSrgb = 99,

        /// <summary>
        /// 8-bit packed YUV, 32 bits per pixel.
        /// </summary>
        AYUV = 100,

        /// <summary>
        /// 10-bit YUV, 2-bit alpha, 32 bits per pixel.
        /// </summary>
        Y410 = 101,

        /// <summary>
        /// 16-bit YUV, 64 bits per pixel.
        /// </summary>
        Y416 = 102,

        /// <summary>
        /// 8-bit YUV 4:2:0, 12 bits per pixel.
        /// </summary>
        NV12 = 103,

        /// <summary>
        /// 10-bit YUV 4:2:0, 24 bits per pixel.
        /// </summary>
        P010 = 104,

        /// <summary>
        /// 16-bit YUV 4:2:0, 24 bits per pixel.
        /// </summary>
        P016 = 105,

        /// <summary>
        /// Opaque 4:2:0 format, 12 bits per pixel.
        /// </summary>
        Format420Opaque = 106,

        /// <summary>
        /// 8-bit packed YUY2, 16 bits per pixel.
        /// </summary>
        YUY2 = 107,

        /// <summary>
        /// 10-bit YUV 4:2:2, 32 bits per pixel.
        /// </summary>
        Y210 = 108,

        /// <summary>
        /// 16-bit YUV 4:2:2, 32 bits per pixel.
        /// </summary>
        Y216 = 109,

        /// <summary>
        /// 8-bit packed NV11, 16 bits per pixel.
        /// </summary>
        NV11 = 110,

        /// <summary>
        /// 4-bit alpha-indexed, 8 bits per pixel.
        /// </summary>
        AI44 = 111,

        /// <summary>
        /// 4-bit intensity-alpha, 8 bits per pixel.
        /// </summary>
        IA44 = 112,

        /// <summary>
        /// 8-bit palette, 8 bits per pixel.
        /// </summary>
        P8 = 113,

        /// <summary>
        /// 8-bit alpha + 8-bit palette, 16 bits per pixel.
        /// </summary>
        A8P8 = 114,

        /// <summary>
        /// 4-bit BGRA, unsigned normalized, 16 bits per pixel.
        /// </summary>
        B4G4R4A4Unorm = 115,

        /// <summary>
        /// 8-bit planar, 8 bits per pixel.
        /// </summary>
        P208 = 130,

        /// <summary>
        /// 8-bit planar, 8 bits per pixel.
        /// </summary>
        V208 = 131,

        /// <summary>
        /// 8-bit planar, 8 bits per pixel.
        /// </summary>
        V408 = 132,

        /// <summary>
        /// Sampler feedback min mip opaque format.
        /// </summary>
        SamplerFeedbackMinMipOpaque = 189,

        /// <summary>
        /// Sampler feedback mip region used opaque format.
        /// </summary>
        SamplerFeedbackMipRegionUsedOpaque = 190,

        /// <summary>
        /// Forces the enum to be 32 bits.
        /// </summary>
        ForceUint = 0xffffffff,
    }
}
