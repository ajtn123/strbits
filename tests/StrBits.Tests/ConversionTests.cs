using System.Globalization;
using System.Text;
using StrBits.Core;
using Xunit;

namespace StrBits.Tests;

public sealed class ConversionTests
{
    [Theory]
    [InlineData(DataKind.Int8, "-128", "80")]
    [InlineData(DataKind.Int8, "127", "7F")]
    [InlineData(DataKind.UInt8, "255", "FF")]
    [InlineData(DataKind.Int16, "-32768", "0080")]
    [InlineData(DataKind.UInt16, "65535", "FFFF")]
    [InlineData(DataKind.Int32, "42", "2A000000")]
    [InlineData(DataKind.Int32, "-2147483648", "00000080")]
    [InlineData(DataKind.UInt32, "4294967295", "FFFFFFFF")]
    [InlineData(DataKind.Int64, "-9223372036854775808", "0000000000000080")]
    [InlineData(DataKind.UInt64, "18446744073709551615", "FFFFFFFFFFFFFFFF")]
    [InlineData(DataKind.Float16, "1", "003C")]
    [InlineData(DataKind.Float32, "1", "0000803F")]
    [InlineData(DataKind.Float64, "1", "000000000000F03F")]
    [InlineData(DataKind.Float32, "-0", "00000080")]
    [InlineData(DataKind.Float64, "Infinity", "000000000000F07F")]
    [InlineData(DataKind.Boolean, "true", "01")]
    [InlineData(DataKind.Boolean, "0", "00")]
    [InlineData(DataKind.Hex, "0x48 65\n6C 6C 6F", "48656C6C6F")]
    [InlineData(DataKind.Base64, "SGVs bG8=", "48656C6C6F")]
    public void KnownValuesMatchStorage(DataKind kind, string input, string hex) =>
        Assert.Equal(hex, BinaryConverter.Convert(input, kind).ToHex());

    [Theory]
    [InlineData(DataKind.Int16, "4660", "1234")]
    [InlineData(DataKind.Int32, "42", "0000002A")]
    [InlineData(DataKind.Int64, "-2", "FFFFFFFFFFFFFFFE")]
    [InlineData(DataKind.Float16, "1", "3C00")]
    [InlineData(DataKind.Float32, "1", "3F800000")]
    [InlineData(DataKind.Float64, "1", "3FF0000000000000")]
    public void BigEndianUsesMostSignificantByteFirst(DataKind kind, string input, string hex) =>
        Assert.Equal(hex, BinaryConverter.Convert(input, kind, order: ByteOrder.BigEndian).ToHex());

    [Theory]
    [InlineData(DataKind.Int8, "128")]
    [InlineData(DataKind.Int8, "-129")]
    [InlineData(DataKind.UInt8, "256")]
    [InlineData(DataKind.UInt8, "-1")]
    [InlineData(DataKind.Int16, "32768")]
    [InlineData(DataKind.UInt16, "65536")]
    [InlineData(DataKind.Int32, "2147483648")]
    [InlineData(DataKind.UInt32, "4294967296")]
    [InlineData(DataKind.Int64, "9223372036854775808")]
    [InlineData(DataKind.UInt64, "18446744073709551616")]
    public void IntegerOverflowIsRejected(DataKind kind, string input) =>
        Assert.Throws<OverflowException>(() => BinaryConverter.Convert(input, kind));

    [Theory]
    [InlineData(DataKind.Int32, "")]
    [InlineData(DataKind.Int32, "1.5")]
    [InlineData(DataKind.Float32, "1,5")]
    [InlineData(DataKind.Decimal, "1,234")]
    [InlineData(DataKind.Boolean, "yes")]
    [InlineData(DataKind.Hex, "A")]
    [InlineData(DataKind.Hex, "GG")]
    [InlineData(DataKind.Base64, "not base64!")]
    public void MalformedValuesAreRejected(DataKind kind, string input) =>
        Assert.Throws<FormatException>(() => BinaryConverter.Convert(input, kind));

    [Theory]
    [InlineData(TextEncoding.Utf8, ByteOrder.LittleEndian, "41F09F918B")]
    [InlineData(TextEncoding.Utf16, ByteOrder.LittleEndian, "41003DD84BDC")]
    [InlineData(TextEncoding.Utf16, ByteOrder.BigEndian, "0041D83DDC4B")]
    [InlineData(TextEncoding.Utf32, ByteOrder.LittleEndian, "410000004BF40100")]
    [InlineData(TextEncoding.Utf32, ByteOrder.BigEndian, "000000410001F44B")]
    public void UnicodeUsesSelectedEncodingWithoutBom(TextEncoding encoding, ByteOrder order, string hex)
    {
        var result = BinaryConverter.Convert("A👋", DataKind.Text, encoding, order);
        Assert.Equal(hex, result.ToHex());
        Assert.Equal(2, result.UnicodeScalars);
        Assert.Equal(5, result.OriginalUtf8Bytes);
        Assert.Equal(3, result.Input.Length);
    }

    [Fact]
    public void AsciiDoesNotSilentlyReplaceUnicode() =>
        Assert.Throws<EncoderFallbackException>(() => BinaryConverter.Convert("é", DataKind.Text, TextEncoding.Ascii));

    [Fact]
    public void InvalidSurrogateIsRejected() =>
        Assert.Throws<EncoderFallbackException>(() => BinaryConverter.Convert("\uD800", DataKind.Text));

    [Fact]
    public void TextWhitespaceIsPreserved() =>
        Assert.Equal("200A20", BinaryConverter.Convert(" \n ", DataKind.Text).ToHex());

    [Theory]
    [InlineData(ByteOrder.LittleEndian, "7B000000000000000000000000000280")]
    [InlineData(ByteOrder.BigEndian, "0000007B000000000000000080020000")]
    public void DecimalPreservesSignScaleAndWordOrder(ByteOrder order, string expected) =>
        Assert.Equal(expected, BinaryConverter.Convert("-1.23", DataKind.Decimal, order: order).ToHex());

    [Fact]
    public void FloatFieldsExposeRoundingAndSpecialValues()
    {
        var one = BinaryConverter.Convert("1", DataKind.Float32);
        Assert.Contains("sign 0 · exponent field 127 · fraction 0x0", one.Note);
        Assert.Contains("unbiased exponent 0", one.Note);
        Assert.Contains("NaN", BinaryConverter.Convert("NaN", DataKind.Float32).Note);
        Assert.Contains("Subnormal", BinaryConverter.Convert("1E-45", DataKind.Float32).Note);
        var overflow = BinaryConverter.Convert("1E100", DataKind.Float32);
        Assert.Equal("Infinity", overflow.Normalized);
        Assert.Equal("0000807F", overflow.ToHex());
    }

    [Fact]
    public void StatisticsDescribeFullPayload()
    {
        var result = BinaryConverter.Convert("00 FF", DataKind.Hex);
        Assert.Equal(16, result.BitCount);
        Assert.Equal(8, result.Ones);
        Assert.Equal(8, result.Zeros);
        Assert.Equal(1, result.Entropy);
        Assert.Equal(5, result.OriginalUtf8Bytes);
        Assert.Equal(1, result.Histogram[0]);
        Assert.Equal(1, result.Histogram[255]);
        Assert.Equal("00000000 11111111", result.ToBinary());
        Assert.Equal("0000 0000 1111 1111", result.ToBinary(true));
    }

    [Fact]
    public void UniformByteDistributionHasEightBitsOfEntropy()
    {
        var input = Convert.ToHexString(Enumerable.Range(0, 256).Select(i => (byte)i).ToArray());
        Assert.Equal(8, BinaryConverter.Convert(input, DataKind.Hex).Entropy, 10);
    }

    [Theory]
    [InlineData(DataKind.Text)]
    [InlineData(DataKind.Hex)]
    [InlineData(DataKind.Base64)]
    public void EmptyPayloadHasFiniteZeroStatistics(DataKind kind)
    {
        var result = BinaryConverter.Convert("", kind);
        Assert.Empty(result.Bytes);
        Assert.Equal(0, result.Entropy);
        Assert.Equal(0, result.Ones);
        Assert.Equal(0, result.Zeros);
    }

    [Fact]
    public void InputLimitIsEnforced() => Assert.Throws<FormatException>(() =>
        BinaryConverter.Convert(new string('A', BinaryConverter.MaxInputLength + 1), DataKind.Text));

    [Fact]
    public void DecimalPointIsIndependentOfSystemCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal("0000C03F", BinaryConverter.Convert("1.5", DataKind.Float32).ToHex());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
