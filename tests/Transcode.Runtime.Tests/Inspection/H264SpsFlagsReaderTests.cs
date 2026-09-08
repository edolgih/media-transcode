using System.Text;
using FluentAssertions;
using Transcode.Core.Inspection;
using Transcode.Core.Videos;

namespace Transcode.Runtime.Tests.Inspection;

public sealed class H264SpsFlagsReaderTests
{
    // Фактический layout ffprobe extradata из проблемного H.264 High@4.1 исходника.
    // Actual ffprobe extradata layout from the problematic H.264 High@4.1 source.
    internal const string SourceExtradata = """

        00000000: 0164 0829 ffe1 001a 6764 0829 acd9 8078  .d.)....gd.)...x
        00000010: 065b 0110 0000 0300 1000 0003 0308 f183  .[..............
        00000020: 19a0 0100 0768 e978 7c4c 84c0            .....h.x|L..

        """;

    [Fact]
    public void Read_WhenSourceExtradataIsPresent_ReadsConstraintSet4()
    {
        H264SpsFlagsReader.Read(SourceExtradata).Should().Be(new H264SpsFlags(true, false));
    }

    [Theory]
    [InlineData("00", false, false)]
    [InlineData("08", true, false)]
    [InlineData("04", false, true)]
    [InlineData("0C", true, true)]
    [InlineData("F0", false, false)]
    public void Read_WhenAvcSpsFlagsVary_ReadsSpsRatherThanConfigurationSummary(
        string flags, bool expected4, bool expected5)
    {
        // Summary в avcC намеренно расходится с некоторыми значениями SPS.
        // avcC's summary deliberately disagrees with some SPS values.
        var data = Convert.FromHexString($"01640829FFE100056764{flags}29800100026880");

        var actual = H264SpsFlagsReader.Read(ToHexDump(data));

        actual.Should().Be(new H264SpsFlags(expected4, expected5));
    }

    [Theory]
    [InlineData("01640029FFE200056764082980000567640429800100026880")]
    [InlineData("0000000167640829800000016764042980000000016880")]
    public void Read_WhenMultipleSpsHeadersArePresent_PreservesFlagsFromEach(string hex)
    {
        H264SpsFlagsReader.Read(ToHexDump(Convert.FromHexString(hex)))
            .Should().Be(new H264SpsFlags(true, true));
    }

    [Theory]
    [InlineData("0000000167640029800000016880")]
    [InlineData("000001096000000167640029800000016880")]
    public void Read_WhenAnnexBHasClearedFlags_ReturnsKnownFalse(string hex)
    {
        H264SpsFlagsReader.Read(ToHexDump(Convert.FromHexString(hex)))
            .Should().Be(new H264SpsFlags(false, false));
    }

    [Fact]
    public void Read_WhenTruncatedSpsPrecedesFourByteStartCode_ReturnsUnknown()
    {
        // У SPS есть NAL header, profile и флаги, но нет level_idc.
        // Ведущий ноль следующего start code не должен завершать этот заголовок.
        // The SPS has a NAL header, profile and flags, but no level_idc.
        // The next start code's leading zero must not complete that header.
        var data = Convert.FromHexString("00000001676400000000016880");

        H264SpsFlagsReader.Read(ToHexDump(data)).Should().BeNull();
    }

    [Theory]
    [InlineData("01640829FF")]
    [InlineData("01640829FFE00100026880")]
    [InlineData("01640829FFE1001A676408")]
    [InlineData("01640829FFE100056864082980")]
    [InlineData("01640829FFE20005676400298000056764")]
    [InlineData("000000016764")]
    [InlineData("000000016880")]
    [InlineData("0000000167640029800000016764")]
    [InlineData("6764082980")]
    public void Read_WhenSpsHeadersAreMissingOrIncomplete_ReturnsUnknown(string hex)
    {
        H264SpsFlagsReader.Read(ToHexDump(Convert.FromHexString(hex))).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a hex dump")]
    [InlineData("00000000: 01zz  deadbeef")]
    [InlineData("00000001: 0164 0829  deadbeef")]
    public void Read_WhenHexDumpIsUnavailableOrMalformed_ReturnsUnknown(string? dump)
    {
        H264SpsFlagsReader.Read(dump).Should().BeNull();
    }

    private static string ToHexDump(byte[] data)
    {
        var dump = new StringBuilder();
        for (var offset = 0; offset < data.Length; offset += 16)
        {
            dump.Append($"{offset:x8}: ");
            var length = Math.Min(16, data.Length - offset);
            var hex = string.Join(" ", data.AsSpan(offset, length).ToArray().Chunk(2).Select(Convert.ToHexString));
            // ASCII-колонка из похожих на hex символов не должна декодироваться.
            // An ASCII column consisting of hex-looking characters must not be decoded.
            dump.Append(hex.PadRight(39)).Append("  deadbeef:01234567\r\n");
        }

        return dump.ToString();
    }
}
