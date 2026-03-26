using System.Buffers.Binary;

namespace RedFox.Graphics2D.Jpeg;

/// <summary>
/// JPEG image decoder supporting baseline and progressive modes. Reads a JPEG bitstream
/// and produces a <see cref="DecodedJpegImage"/> containing decoded component planes.
/// </summary>
public sealed class JpegDecoder(Stream stream)
{
    private readonly Stream _stream = stream;
    private readonly JpegQuantizationTable?[] _quantTables = new JpegQuantizationTable?[4];
    private readonly JpegHuffmanTable?[] _dcTables = new JpegHuffmanTable?[4];
    private readonly JpegHuffmanTable?[] _acTables = new JpegHuffmanTable?[4];
    private JpegFrame? _frame;
    private int _restartInterval;
    private JpegMarker? _pendingMarker;
    private int _eobRun;

    /// <summary>Decodes the JPEG bitstream and returns the resulting image.</summary>
    /// <returns>A <see cref="DecodedJpegImage"/> containing the decoded pixel data and metadata.</returns>
    public DecodedJpegImage Decode()
    {
        ReadMarker(out var marker);
        if (marker != JpegMarker.SOI)
            throw new InvalidDataException("Invalid JPEG: missing SOI marker.");

        while (true)
        {
            ReadMarker(out marker);

            switch (marker)
            {
                case JpegMarker.SOF0:
                case JpegMarker.SOF1:
                    ParseFrameHeader(progressive: false);
                    break;

                case JpegMarker.SOF2:
                    ParseFrameHeader(progressive: true);
                    break;

                case JpegMarker.DHT:
                    ParseHuffmanTable();
                    break;

                case JpegMarker.DQT:
                    ParseQuantizationTable();
                    break;

                case JpegMarker.DRI:
                    ParseRestartInterval();
                    break;

                case JpegMarker.SOS:
                    ParseScanAndDecode();
                    break;

                case JpegMarker.EOI:
                    return BuildResult();

                default:
                    // Skip unknown/unsupported marker segments
                    if (HasSegmentLength(marker))
                        SkipSegment();
                    break;
            }
        }
    }

    private void ReadMarker(out JpegMarker marker)
    {
        // Check if a marker was already consumed by the bit reader
        if (_pendingMarker.HasValue)
        {
            marker = _pendingMarker.Value;
            _pendingMarker = null;
            return;
        }

        // Scan forward for the next 0xFF marker prefix, skipping any
        // non-marker bytes (e.g. padding or leftover entropy data)
        int b;
        while (true)
        {
            b = _stream.ReadByte();
            if (b < 0)
                throw new InvalidDataException("Unexpected end of JPEG stream.");
            if (b == 0xFF)
                break;
        }

        // Skip fill bytes (consecutive 0xFF)
        do
        {
            b = _stream.ReadByte();
            if (b < 0)
                throw new InvalidDataException("Unexpected end of JPEG stream.");
        } while (b == 0xFF);

        // 0xFF 0x00 is byte stuffing — shouldn't appear here, but skip if it does
        if (b == 0x00)
        {
            // Recurse to find the real marker
            ReadMarker(out marker);
            return;
        }

        marker = (JpegMarker)b;
    }

    private int ReadSegmentLength()
    {
        Span<byte> buf = stackalloc byte[2];
        _stream.ReadExactly(buf);
        return BinaryPrimitives.ReadUInt16BigEndian(buf);
    }

    private byte[] ReadSegment()
    {
        int length = ReadSegmentLength();
        if (length < 2)
            throw new InvalidDataException("Invalid JPEG segment length.");

        var data = new byte[length - 2];
        _stream.ReadExactly(data);
        return data;
    }

    private void SkipSegment()
    {
        int length = ReadSegmentLength();
        if (length < 2)
            throw new InvalidDataException("Invalid JPEG segment length.");

        if (_stream.CanSeek)
        {
            _stream.Seek(length - 2, SeekOrigin.Current);
        }
        else
        {
            var discard = new byte[Math.Min(length - 2, 4096)];
            int remaining = length - 2;
            while (remaining > 0)
            {
                int toRead = Math.Min(remaining, discard.Length);
                _stream.ReadExactly(discard.AsSpan(0, toRead));
                remaining -= toRead;
            }
        }
    }

    private static bool HasSegmentLength(JpegMarker marker)
    {
        byte m = (byte)marker;

        if (m == 0xD8 || m == 0xD9 || m == 0x01)
            return false;
        if (m >= 0xD0 && m <= 0xD7)
            return false;

        return true;
    }

    private void ParseFrameHeader(bool progressive)
    {
        var data = ReadSegment();
        int offset = 0;

        _frame = new JpegFrame
        {
            Precision = data[offset++],
            Height = (data[offset++] << 8) | data[offset++],
            Width = (data[offset++] << 8) | data[offset++],
            Progressive = progressive,
        };

        int componentCount = data[offset++];

        int maxH = 1, maxV = 1;

        for (int i = 0; i < componentCount; i++)
        {
            int id = data[offset++];
            int samplingByte = data[offset++];
            int h = samplingByte >> 4;
            int v = samplingByte & 0x0F;
            int qId = data[offset++];

            _frame.Components[id] = new JpegFrameComponent
            {
                Id = id,
                HSample = h,
                VSample = v,
                QuantizationTableId = qId,
            };

            _frame.ComponentOrder.Add(id);

            if (h > maxH) maxH = h;
            if (v > maxV) maxV = v;
        }

        _frame.MaxHSample = maxH;
        _frame.MaxVSample = maxV;

        // Calculate MCU dimensions and block counts per component
        int mcuPixelW = maxH * 8;
        int mcuPixelH = maxV * 8;
        _frame.McuWidth = ((_frame.Width + mcuPixelW - 1) / mcuPixelW);
        _frame.McuHeight = ((_frame.Height + mcuPixelH - 1) / mcuPixelH);
        _frame.McuCount = _frame.McuWidth * _frame.McuHeight;

        foreach (var comp in _frame.Components.Values)
        {
            comp.BlocksPerRow = _frame.McuWidth * comp.HSample;
            comp.BlocksPerColumn = _frame.McuHeight * comp.VSample;

            int totalBlocks = comp.BlocksPerRow * comp.BlocksPerColumn;
            comp.Blocks = new int[totalBlocks][];
            for (int i = 0; i < totalBlocks; i++)
                comp.Blocks[i] = new int[64];
        }
    }

    private void ParseHuffmanTable()
    {
        var data = ReadSegment();
        int offset = 0;

        while (offset < data.Length)
        {
            int info = data[offset++];
            int tableClass = info >> 4;     // 0 = DC, 1 = AC
            int tableId = info & 0x0F;

            // Read 16 code-length counts
            ReadOnlySpan<byte> counts = data.AsSpan(offset, 16);
            offset += 16;

            int totalSymbols = 0;
            for (int i = 0; i < 16; i++)
                totalSymbols += counts[i];

            ReadOnlySpan<byte> values = data.AsSpan(offset, totalSymbols);
            offset += totalSymbols;

            var table = new JpegHuffmanTable(counts, values);

            if (tableClass == 0)
                _dcTables[tableId] = table;
            else
                _acTables[tableId] = table;
        }
    }

    private void ParseQuantizationTable()
    {
        var data = ReadSegment();
        int offset = 0;

        while (offset < data.Length)
        {
            int info = data[offset++];
            int precision = info >> 4;   // 0 = 8-bit, 1 = 16-bit
            int tableId = info & 0x0F;

            var table = new JpegQuantizationTable { Id = tableId };

            var zigzag = JpegZigZag.Order;
            for (int i = 0; i < 64; i++)
            {
                int value;
                if (precision == 0)
                {
                    value = data[offset++];
                }
                else
                {
                    value = (data[offset] << 8) | data[offset + 1];
                    offset += 2;
                }

                table.Values[zigzag[i]] = value;
            }

            _quantTables[tableId] = table;
        }
    }

    private void ParseRestartInterval()
    {
        var data = ReadSegment();
        _restartInterval = (data[0] << 8) | data[1];
    }

    private void ParseScanAndDecode()
    {
        if (_frame is null)
            throw new InvalidDataException("SOS marker before SOF marker.");

        var data = ReadSegment();
        int offset = 0;

        int componentCount = data[offset++];
        var scanHeader = new JpegScanHeader();
        var scanComponents = new JpegScanComponent[componentCount];

        for (int i = 0; i < componentCount; i++)
        {
            int compId = data[offset++];
            int tableByte = data[offset++];

            scanComponents[i] = new JpegScanComponent
            {
                ComponentId = compId,
                DcTableId = tableByte >> 4,
                AcTableId = tableByte & 0x0F,
            };
        }

        scanHeader.Components = scanComponents;
        scanHeader.SpectralStart = data[offset++];
        scanHeader.SpectralEnd = data[offset++];
        int approx = data[offset++];
        scanHeader.SuccessiveHigh = approx >> 4;
        scanHeader.SuccessiveLow = approx & 0x0F;

        if (_frame.Progressive)
        {
            DecodeProgressiveScan(scanHeader);
        }
        else
        {
            DecodeBaselineScan(scanHeader);
        }
    }

    private void DecodeBaselineScan(JpegScanHeader scan)
    {
        var reader = new JpegBitReader(_stream);
        int mcuCount = 0;
        int restartExpected = 0;

        // Reset DC predictors
        foreach (var sc in scan.Components)
            _frame!.Components[sc.ComponentId].PreviousDc = 0;

        for (int mcuIndex = 0; mcuIndex < _frame!.McuCount; mcuIndex++)
        {
            // Handle restart interval
            if (!TryHandleRestartMarker(reader, ref mcuCount, ref restartExpected))
            {
                return;
            }

            if (_restartInterval > 0 && mcuCount == 0 && mcuIndex > 0)
            {
                foreach (var sc in scan.Components)
                    _frame.Components[sc.ComponentId].PreviousDc = 0;
            }

            int mcuRow = mcuIndex / _frame.McuWidth;
            int mcuCol = mcuIndex % _frame.McuWidth;

            foreach (var sc in scan.Components)
            {
                var comp = _frame.Components[sc.ComponentId];
                var dcTable = _dcTables[sc.DcTableId] ?? throw new InvalidDataException($"Missing DC Huffman table {sc.DcTableId}.");
                var acTable = _acTables[sc.AcTableId] ?? throw new InvalidDataException($"Missing AC Huffman table {sc.AcTableId}.");

                for (int v = 0; v < comp.VSample; v++)
                {
                    for (int h = 0; h < comp.HSample; h++)
                    {
                        int blockRow = mcuRow * comp.VSample + v;
                        int blockCol = mcuCol * comp.HSample + h;
                        int blockIndex = blockRow * comp.BlocksPerRow + blockCol;

                        if (!TryDecodeBlock(reader, comp.Blocks![blockIndex], dcTable, acTable, comp))
                        {
                            CaptureScanMarker(reader);
                            return;
                        }
                    }
                }
            }

            mcuCount++;
        }
    }

    private static bool TryDecodeBlock(JpegBitReader reader, int[] block,
        JpegHuffmanTable dcTable, JpegHuffmanTable acTable,
        JpegFrameComponent comp)
    {
        // DC coefficient
        if (!dcTable.TryDecode(reader, out int dcCategory))
        {
            return false;
        }

        int dcDiff = 0;
        if (dcCategory > 0)
        {
            if (!reader.TryReadBits(dcCategory, out int dcBits))
            {
                return false;
            }

            dcDiff = JpegHuffmanTable.Extend(dcBits, dcCategory);
        }

        comp.PreviousDc += dcDiff;
        block[0] = comp.PreviousDc;

        // AC coefficients
        var zigzag = JpegZigZag.Order;
        int k = 1;
        while (k < 64)
        {
            if (!acTable.TryDecode(reader, out int rs))
            {
                return false;
            }

            int run = rs >> 4;
            int size = rs & 0x0F;

            if (size == 0)
            {
                if (run == 0)
                    break; // EOB
                if (run == 0x0F)
                {
                    k += 16; // ZRL
                    continue;
                }
                break;
            }

            k += run;
            if (k >= 64)
                break;

            if (!reader.TryReadBits(size, out int coefficientBits))
            {
                return false;
            }

            int value = JpegHuffmanTable.Extend(coefficientBits, size);
            block[zigzag[k]] = value;
            k++;
        }

        return true;
    }

    private void DecodeProgressiveScan(JpegScanHeader scan)
    {
        _eobRun = 0;

        bool isDc = scan.SpectralStart == 0;
        bool isFirstVisit = scan.SuccessiveHigh == 0;

        // Reset DC predictors for first DC scan
        if (isDc && isFirstVisit)
        {
            foreach (var sc in scan.Components)
                _frame!.Components[sc.ComponentId].PreviousDc = 0;
        }

        if (scan.Components.Length == 1)
        {
            DecodeProgressiveScanNonInterleaved(scan, isDc, isFirstVisit);
        }
        else
        {
            DecodeProgressiveScanInterleaved(scan, isDc, isFirstVisit);
        }
    }

    private void DecodeProgressiveScanNonInterleaved(
        JpegScanHeader scan,
        bool isDc,
        bool isFirstVisit)
    {
        var reader = new JpegBitReader(_stream);
        int mcuCount = 0;
        int restartExpected = 0;

        var sc = scan.Components[0];
        var comp = _frame!.Components[sc.ComponentId];
        int totalBlocks = comp.BlocksPerRow * comp.BlocksPerColumn;

        var dcTable = isDc ? _dcTables[sc.DcTableId] : null;
        var acTable = !isDc ? _acTables[sc.AcTableId] : null;

        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            if (!TryHandleRestartMarker(reader, ref mcuCount, ref restartExpected))
            {
                return;
            }

            if (_restartInterval > 0 && mcuCount == 0 && blockIndex > 0)
            {
                _eobRun = 0;

                if (isDc && isFirstVisit)
                {
                    comp.PreviousDc = 0;
                }
            }

            var block = comp.Blocks![blockIndex];
            bool decoded = isDc
                ? isFirstVisit
                    ? TryDecodeProgressiveDcFirst(reader, block, dcTable!, comp, scan.SuccessiveLow)
                    : TryDecodeProgressiveDcRefine(reader, block, scan.SuccessiveLow)
                : isFirstVisit
                    ? TryDecodeProgressiveAcFirst(reader, block, acTable!, scan.SpectralStart, scan.SpectralEnd, scan.SuccessiveLow)
                    : TryDecodeProgressiveAcRefine(reader, block, acTable!, scan.SpectralStart, scan.SpectralEnd, scan.SuccessiveLow);

            if (!decoded)
            {
                CaptureScanMarker(reader);
                return;
            }

            mcuCount++;
        }
    }

    private void DecodeProgressiveScanInterleaved(
        JpegScanHeader scan,
        bool isDc,
        bool isFirstVisit)
    {
        var reader = new JpegBitReader(_stream);
        int mcuCount = 0;
        int restartExpected = 0;

        for (int mcuIndex = 0; mcuIndex < _frame!.McuCount; mcuIndex++)
        {
            if (!TryHandleRestartMarker(reader, ref mcuCount, ref restartExpected))
            {
                return;
            }

            if (_restartInterval > 0 && mcuCount == 0 && mcuIndex > 0)
            {
                _eobRun = 0;

                if (isDc && isFirstVisit)
                {
                    foreach (var sc2 in scan.Components)
                        _frame.Components[sc2.ComponentId].PreviousDc = 0;
                }
            }

            int mcuRow = mcuIndex / _frame.McuWidth;
            int mcuCol = mcuIndex % _frame.McuWidth;

            foreach (var sc in scan.Components)
            {
                var comp = _frame.Components[sc.ComponentId];
                var dcTable = _dcTables[sc.DcTableId];

                for (int v = 0; v < comp.VSample; v++)
                {
                    for (int h = 0; h < comp.HSample; h++)
                    {
                        int blockRow = mcuRow * comp.VSample + v;
                        int blockCol = mcuCol * comp.HSample + h;
                        int blockIndex = blockRow * comp.BlocksPerRow + blockCol;

                        var block = comp.Blocks![blockIndex];

                        bool decoded = isFirstVisit
                            ? TryDecodeProgressiveDcFirst(reader, block, dcTable!, comp, scan.SuccessiveLow)
                            : TryDecodeProgressiveDcRefine(reader, block, scan.SuccessiveLow);

                        if (!decoded)
                        {
                            CaptureScanMarker(reader);
                            return;
                        }
                    }
                }
            }

            mcuCount++;
        }
    }

    private static bool TryDecodeProgressiveDcFirst(JpegBitReader reader, int[] block,
        JpegHuffmanTable dcTable, JpegFrameComponent comp, int al)
    {
        if (!dcTable.TryDecode(reader, out int category))
        {
            return false;
        }

        int diff = 0;
        if (category > 0)
        {
            if (!reader.TryReadBits(category, out int coefficientBits))
            {
                return false;
            }

            diff = JpegHuffmanTable.Extend(coefficientBits, category);
        }

        comp.PreviousDc += diff;
        block[0] = comp.PreviousDc << al;
        return true;
    }

    private static bool TryDecodeProgressiveDcRefine(JpegBitReader reader, int[] block, int al)
    {
        if (!reader.TryReadBit(out int bit))
        {
            return false;
        }

        block[0] |= bit << al;
        return true;
    }

    /// <summary>
    /// Decodes the first visit of AC coefficients in progressive mode.
    /// </summary>
    private bool TryDecodeProgressiveAcFirst(JpegBitReader reader, int[] block,
        JpegHuffmanTable acTable, int ss, int se, int al)
    {
        // If we're in an EOB run from a previous block, skip this block
        if (_eobRun > 0)
        {
            _eobRun--;
            return true;
        }

        var zigzag = JpegZigZag.Order;
        int k = ss;

        while (k <= se)
        {
            if (!acTable.TryDecode(reader, out int rs))
            {
                return false;
            }

            int run = rs >> 4;
            int size = rs & 0x0F;

            if (size == 0)
            {
                if (run == 0x0F)
                {
                    k += 16;
                    continue;
                }

                // EOBn: end-of-band run
                // run=0 → EOB just this block
                // run>0 → (1 << run) + readBits(run) total EOBs (including this block)
                if (run > 0)
                {
                    if (!reader.TryReadBits(run, out int eobBits))
                    {
                        return false;
                    }

                    _eobRun = (1 << run) + eobBits - 1;
                }
                break;
            }

            k += run;
            if (k > se)
                break;

            if (!reader.TryReadBits(size, out int coefficientBits))
            {
                return false;
            }

            int value = JpegHuffmanTable.Extend(coefficientBits, size);
            block[zigzag[k]] = value << al;
            k++;
        }

        return true;
    }

    private bool TryDecodeProgressiveAcRefine(JpegBitReader reader, int[] block,
        JpegHuffmanTable acTable, int ss, int se, int al)
    {
        var zigzag = JpegZigZag.Order;
        int k = ss;
        int p1 = 1 << al;
        int m1 = -(1 << al);

        // If we're in an EOB run, just refine existing non-zero coefficients
        if (_eobRun > 0)
        {
            while (k <= se)
            {
                int pos = zigzag[k];
                if (block[pos] != 0)
                {
                    if (!reader.TryReadBit(out int bit))
                    {
                        return false;
                    }

                    if (bit != 0)
                    {
                        block[pos] += block[pos] > 0 ? p1 : m1;
                    }
                }
                k++;
            }
            _eobRun--;
            return true;
        }

        while (k <= se)
        {
            if (!acTable.TryDecode(reader, out int rs))
            {
                return false;
            }

            int run = rs >> 4;
            int size = rs & 0x0F;

            int sign = 0;
            if (size != 0)
            {
                if (!reader.TryReadBit(out sign))
                {
                    return false;
                }
            }
            else if (run != 0x0F)
            {
                // EOBn
                if (run > 0)
                {
                    if (!reader.TryReadBits(run, out int eobBits))
                    {
                        return false;
                    }

                    _eobRun = (1 << run) + eobBits - 1;
                }

                // Refine remaining non-zero coefficients in this block
                while (k <= se)
                {
                    int pos = zigzag[k];
                    if (block[pos] != 0)
                    {
                        if (!reader.TryReadBit(out int bit))
                        {
                            return false;
                        }

                        if (bit != 0)
                        {
                            block[pos] += block[pos] > 0 ? p1 : m1;
                        }
                    }
                    k++;
                }
                break;
            }

            // Skip <run> zero coefficients, refining existing non-zeros as we go
            int zerosToSkip = run;
            while (k <= se)
            {
                int pos = zigzag[k];
                if (block[pos] != 0)
                {
                    if (!reader.TryReadBit(out int bit))
                    {
                        return false;
                    }

                    if (bit != 0)
                    {
                        block[pos] += block[pos] > 0 ? p1 : m1;
                    }
                }
                else
                {
                    if (zerosToSkip == 0)
                        break;
                    zerosToSkip--;
                }
                k++;
            }

            // Place the new coefficient
            if (k <= se)
            {
                int pos = zigzag[k];
                if (size != 0)
                {
                    block[pos] = sign != 0 ? p1 : m1;
                }
                k++;
            }
        }

        return true;
    }

    private void CaptureScanMarker(JpegBitReader reader)
    {
        if (reader.HitMarker && !IsRestartMarker(reader.PendingMarker))
        {
            _pendingMarker = reader.PendingMarker;
        }
    }

    private static bool IsRestartMarker(JpegMarker marker)
    {
        byte m = (byte)marker;
        return m >= 0xD0 && m <= 0xD7;
    }

    /// <summary>
    /// Checks whether a restart marker boundary has been reached and, if so,
    /// resets the bit reader for the next entropy segment. Returns <c>false</c>
    /// when a non-restart marker is encountered (the caller should abort the scan).
    /// </summary>
    private bool TryHandleRestartMarker(
        JpegBitReader reader,
        ref int mcuCount,
        ref int restartExpected)
    {
        if (_restartInterval <= 0 || mcuCount != _restartInterval)
        {
            return true;
        }

        mcuCount = 0;
        reader.AlignToByte();

        if (reader.HitMarker)
        {
            var hitMarker = reader.PendingMarker;
            if (!IsRestartMarker(hitMarker))
            {
                _pendingMarker = hitMarker;
                return false;
            }

            reader.Reset();
        }
        else
        {
            ReadRestartMarker(restartExpected);
            reader.Reset();
        }

        restartExpected = (restartExpected + 1) & 7;
        return true;
    }

    private void ReadRestartMarker(int expected)
    {
        int b1 = _stream.ReadByte();
        int b2 = _stream.ReadByte();

        // Sometimes there's garbage between restart boundaries — skip to find 0xFF RST
        if (b1 != 0xFF)
        {
            while (true)
            {
                b1 = b2;
                b2 = _stream.ReadByte();
                if (b2 < 0)
                    throw new InvalidDataException("Unexpected end of stream looking for restart marker.");
                if (b1 == 0xFF && b2 >= 0xD0 && b2 <= 0xD7)
                    break;
            }
        }
    }

    private DecodedJpegImage BuildResult()
    {
        if (_frame is null)
            throw new InvalidDataException("No frame header found in JPEG.");

        int componentCount = _frame.ComponentOrder.Count;
        var componentData = new byte[componentCount][];
        var componentWidths = new int[componentCount];
        var componentHeights = new int[componentCount];
        var componentHSamples = new int[componentCount];
        var componentVSamples = new int[componentCount];

        Span<int> blockBuf = stackalloc int[64];

        for (int ci = 0; ci < componentCount; ci++)
        {
            int compId = _frame.ComponentOrder[ci];
            var comp = _frame.Components[compId];
            var qTable = _quantTables[comp.QuantizationTableId]
                ?? throw new InvalidDataException($"Missing quantization table {comp.QuantizationTableId}.");

            int compPixelW = comp.BlocksPerRow * 8;
            int compPixelH = comp.BlocksPerColumn * 8;
            var samples = new byte[compPixelW * compPixelH];

            // Dequantize + IDCT for each block, then scatter to the output plane
            for (int blockRow = 0; blockRow < comp.BlocksPerColumn; blockRow++)
            {
                for (int blockCol = 0; blockCol < comp.BlocksPerRow; blockCol++)
                {
                    int blockIndex = blockRow * comp.BlocksPerRow + blockCol;
                    var coefficients = comp.Blocks![blockIndex];

                    // Dequantize
                    for (int i = 0; i < 64; i++)
                        blockBuf[i] = coefficients[i] * qTable.Values[i];

                    // IDCT
                    JpegIdct.Transform(blockBuf);

                    // Write samples to output plane
                    int px = blockCol * 8;
                    int py = blockRow * 8;

                    for (int y = 0; y < 8; y++)
                    {
                        int dstRow = py + y;
                        if (dstRow >= compPixelH)
                            break;

                        int srcOffset = y * 8;
                        int dstOffset = dstRow * compPixelW + px;

                        for (int x = 0; x < 8; x++)
                        {
                            if (px + x >= compPixelW)
                                break;

                            samples[dstOffset + x] = (byte)blockBuf[srcOffset + x];
                        }
                    }
                }
            }

            componentData[ci] = samples;
            componentWidths[ci] = compPixelW;
            componentHeights[ci] = compPixelH;
            componentHSamples[ci] = comp.HSample;
            componentVSamples[ci] = comp.VSample;
        }

        var colorSpace = componentCount switch
        {
            1 => JpegColorSpace.Grayscale,
            3 => JpegColorSpace.YCbCr,
            4 => JpegColorSpace.Cmyk,
            _ => JpegColorSpace.YCbCr,
        };

        return new DecodedJpegImage
        {
            Width = _frame.Width,
            Height = _frame.Height,
            ComponentCount = componentCount,
            ColorSpace = colorSpace,
            ComponentData = componentData,
            ComponentWidths = componentWidths,
            ComponentHeights = componentHeights,
            ComponentHSamples = componentHSamples,
            ComponentVSamples = componentVSamples,
            MaxHSample = _frame.MaxHSample,
            MaxVSample = _frame.MaxVSample,
        };
    }
}
