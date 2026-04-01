namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// JPEG marker byte values that delimit segments in the bitstream.
/// </summary>
public enum JpegMarker : byte
{
    /// <summary>Start of image marker.</summary>
    SOI = 0xD8,

    /// <summary>End of image marker.</summary>
    EOI = 0xD9,

    /// <summary>Baseline DCT start of frame marker.</summary>
    SOF0 = 0xC0,

    /// <summary>Extended sequential DCT start of frame marker.</summary>
    SOF1 = 0xC1,

    /// <summary>Progressive DCT start of frame marker.</summary>
    SOF2 = 0xC2,

    /// <summary>Define Huffman table marker.</summary>
    DHT = 0xC4,

    /// <summary>Define quantization table marker.</summary>
    DQT = 0xDB,

    /// <summary>Start of scan marker.</summary>
    SOS = 0xDA,

    /// <summary>Restart marker 0.</summary>
    RST0 = 0xD0,

    /// <summary>Restart marker 1.</summary>
    RST1 = 0xD1,

    /// <summary>Restart marker 2.</summary>
    RST2 = 0xD2,

    /// <summary>Restart marker 3.</summary>
    RST3 = 0xD3,

    /// <summary>Restart marker 4.</summary>
    RST4 = 0xD4,

    /// <summary>Restart marker 5.</summary>
    RST5 = 0xD5,

    /// <summary>Restart marker 6.</summary>
    RST6 = 0xD6,

    /// <summary>Restart marker 7.</summary>
    RST7 = 0xD7,

    /// <summary>Define restart interval marker.</summary>
    DRI = 0xDD,

    /// <summary>Application segment 0 marker.</summary>
    APP0 = 0xE0,

    /// <summary>Application segment 1 marker.</summary>
    APP1 = 0xE1,

    /// <summary>Application segment 2 marker.</summary>
    APP2 = 0xE2,

    /// <summary>Application segment 14 marker.</summary>
    APP14 = 0xEE,

    /// <summary>Comment marker.</summary>
    COM = 0xFE,
}
