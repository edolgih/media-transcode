using System.Globalization;
using Transcode.Core.Videos;

namespace Transcode.Core.Inspection;

/*
Это reader SPS constraint-флагов из hex dump codec extradata, возвращённого ffprobe.
Неполный или нечитаемый заголовок не считается сброшенными флагами.
*/
/// <summary>
/// Reads SPS constraint flags from ffprobe's codec-extradata hex dump.
/// Unknown or incomplete headers yield no facts, rather than cleared flags.
/// </summary>
internal static class H264SpsFlagsReader
{
    internal static H264SpsFlags? Read(string? hexDump)
    {
        var data = DecodeHexDump(hexDump);
        if (data is null || data.Length == 0)
        {
            return null;
        }

        return data[0] == 1 ? ReadAvcConfiguration(data) : ReadAnnexB(data);
    }

    private static byte[]? DecodeHexDump(string? hexDump)
    {
        if (string.IsNullOrWhiteSpace(hexDump))
        {
            return null;
        }

        var bytes = new List<byte>();
        foreach (var row in hexDump.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = row.IndexOf(':');
            if (colon < 0 ||
                !int.TryParse(row.AsSpan(0, colon), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset) ||
                offset != bytes.Count)
            {
                return null;
            }

            var hex = row[(colon + 1)..].TrimStart();
            // ffprobe отделяет hex-колонки от ASCII-колонки двумя пробелами.
            // ffprobe separates the hex columns from the ASCII column with two spaces.
            var asciiStart = hex.IndexOf("  ", StringComparison.Ordinal);
            if (asciiStart >= 0)
            {
                hex = hex[..asciiStart];
            }

            var rowStart = bytes.Count;
            foreach (var word in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length is not (2 or 4))
                {
                    return null;
                }

                for (var i = 0; i < word.Length; i += 2)
                {
                    if (!byte.TryParse(word.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                    {
                        return null;
                    }

                    bytes.Add(value);
                }
            }

            if (bytes.Count - rowStart is < 1 or > 16)
            {
                return null;
            }
        }

        return bytes.ToArray();
    }

    private static H264SpsFlags? ReadAvcConfiguration(ReadOnlySpan<byte> data)
    {
        // AVCDecoderConfigurationRecord: шесть байт, затем SPS NAL units с указанием длины.
        // AVCDecoderConfigurationRecord: six bytes, then length-prefixed SPS NAL units.
        if (data.Length < 6)
        {
            return null;
        }

        var count = data[5] & 0x1f;
        var position = 6;
        H264SpsFlags? flags = null;
        for (var i = 0; i < count; i++)
        {
            if (data.Length - position < 2)
            {
                return null;
            }

            var size = (data[position] << 8) | data[position + 1];
            position += 2;
            if (size > data.Length - position || !TryReadSps(data.Slice(position, size), ref flags))
            {
                return null;
            }

            position += size;
        }

        return flags;
    }

    private static H264SpsFlags? ReadAnnexB(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> startCode = [0, 0, 1];
        var start = data.IndexOf(startCode);
        if (start < 0)
        {
            return null;
        }

        // Принимаются трёх- и четырёхбайтные start code с ведущими нулевыми байтами.
        // Both three- and four-byte start codes are accepted, with leading zero bytes.
        for (var i = 0; i < start; i++)
        {
            if (data[i] != 0)
            {
                return null;
            }
        }

        H264SpsFlags? flags = null;
        while (start >= 0)
        {
            data = data[(start + 3)..];
            var next = data.IndexOf(startCode);
            var nalEnd = next < 0 ? data.Length : next;
            // Исключаются Annex B trailing_zero_8bits и zero_byte перед четырёхбайтным start code:
            // они не входят в NAL payload.
            // Exclude Annex B trailing_zero_8bits and the zero_byte preceding
            // a four-byte start code; neither belongs to the NAL payload.
            while (nalEnd > 0 && data[nalEnd - 1] == 0)
            {
                nalEnd--;
            }

            var nal = data[..nalEnd];
            if (nal.IsEmpty ||
                ((nal[0] & 0x1f) == 7 && !TryReadSps(nal, ref flags)))
            {
                return null;
            }

            start = next;
        }

        return flags;
    }

    private static bool TryReadSps(ReadOnlySpan<byte> nal, ref H264SpsFlags? flags)
    {
        // NAL header, profile_idc, constraint flags, level_idc. Для чтения флагов
        // не требуется RBSP unescaping: emulation prevention не может предшествовать этому байту.
        // NAL header, profile_idc, constraint flags, level_idc. No RBSP unescaping
        // is needed to read the flags: emulation prevention cannot precede this byte.
        if (nal.Length < 4 || (nal[0] & 0x9f) != 7 || (nal[2] & 0x03) != 0)
        {
            return false;
        }

        flags = new H264SpsFlags(
            ConstraintSet4Flag: flags?.ConstraintSet4Flag == true || (nal[2] & 0x08) != 0,
            ConstraintSet5Flag: flags?.ConstraintSet5Flag == true || (nal[2] & 0x04) != 0);
        return true;
    }
}
