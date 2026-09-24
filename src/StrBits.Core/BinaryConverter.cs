using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace StrBits.Core;

public enum DataKind { Text, Int8, UInt8, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float16, Float32, Float64, Decimal, Boolean, Hex, Base64 }
public enum TextEncoding { Utf8, Utf16, Utf32, Ascii }
public enum ByteOrder { LittleEndian, BigEndian }

public sealed record TypeOption(DataKind Kind, string Name, string Description, string Example)
{
    public override string ToString() => Name;
}

public static class BinaryConverter
{
    public const int MaxInputLength = 32768;
    public static IReadOnlyList<TypeOption> Types { get; } =
    [
        new(DataKind.Text, "String / text", "Encode text without a byte order mark. UTF-16 and UTF-32 use the selected byte order.", "Hello, binary! 👋"),
        new(DataKind.Int8, "Int8 · signed integer", "8-bit signed integer · −128 to 127 · two’s complement.", "-42"),
        new(DataKind.UInt8, "UInt8 · unsigned integer", "8-bit unsigned integer · 0 to 255.", "255"),
        new(DataKind.Int16, "Int16 · signed integer", "16-bit signed integer · −32,768 to 32,767 · two’s complement.", "-1024"),
        new(DataKind.UInt16, "UInt16 · unsigned integer", "16-bit unsigned integer · 0 to 65,535.", "65535"),
        new(DataKind.Int32, "Int32 · signed integer", "32-bit signed integer · −2,147,483,648 to 2,147,483,647 · two’s complement.", "42"),
        new(DataKind.UInt32, "UInt32 · unsigned integer", "32-bit unsigned integer · 0 to 4,294,967,295.", "4294967295"),
        new(DataKind.Int64, "Int64 · signed integer", "64-bit signed integer · −9,223,372,036,854,775,808 to 9,223,372,036,854,775,807.", "-9223372036854775808"),
        new(DataKind.UInt64, "UInt64 · unsigned integer", "64-bit unsigned integer · 0 to 18,446,744,073,709,551,615.", "18446744073709551615"),
        new(DataKind.Float16, "Float16 · half precision", "IEEE 754 · 1 sign, 5 exponent, 10 fraction bits. Use a decimal point; NaN and Infinity are supported.", "0.1"),
        new(DataKind.Float32, "Float32 · single precision", "IEEE 754 · 1 sign, 8 exponent, 23 fraction bits. Use a decimal point; NaN and Infinity are supported.", "3.1415927"),
        new(DataKind.Float64, "Float64 · double precision", "IEEE 754 · 1 sign, 11 exponent, 52 fraction bits. Use a decimal point; NaN and Infinity are supported.", "3.141592653589793"),
        new(DataKind.Decimal, "Decimal · 128-bit decimal", ".NET decimal: four 32-bit words (low, middle, high, flags). Byte order applies within each word.", "1234.5678"),
        new(DataKind.Boolean, "Boolean", "One byte: true / 1 → 00000001; false / 0 → 00000000.", "true"),
        new(DataKind.Hex, "Hexadecimal bytes", "Enter pairs of hex digits, optionally separated by whitespace. A leading 0x is allowed. Order is preserved.", "48 65 6C 6C 6F"),
        new(DataKind.Base64, "Base64 bytes", "Decode standard Base64 into bytes. Whitespace is ignored; byte order is preserved.", "SGVsbG8=")
    ];

    public static ConversionResult Convert(string input, DataKind kind, TextEncoding encoding = TextEncoding.Utf8,
        ByteOrder order = ByteOrder.LittleEndian)
    {
        if (input.Length > MaxInputLength)
            throw new FormatException($"Input is limited to {MaxInputLength:N0} UTF-16 code units.");

        var big = order == ByteOrder.BigEndian;
        var value = input.Trim();
        var culture = CultureInfo.InvariantCulture;
        var note = "";
        var normalized = value;
        byte[] bytes;
        switch (kind)
        {
            case DataKind.Text:
                Encoding codec = encoding switch
                {
                    TextEncoding.Utf8 => new UTF8Encoding(false, true),
                    TextEncoding.Utf16 => new UnicodeEncoding(big, false, true),
                    TextEncoding.Utf32 => new UTF32Encoding(big, false, true),
                    _ => Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                };
                bytes = codec.GetBytes(input);
                normalized = input;
                note = $"{codec.WebName}; no BOM. Text is preserved, including whitespace.";
                break;
            case DataKind.Int8: bytes = [unchecked((byte)sbyte.Parse(value, culture))]; break;
            case DataKind.UInt8: bytes = [byte.Parse(value, culture)]; break;
            case DataKind.Int16: bytes = IntegerBytes(unchecked((ulong)short.Parse(value, culture)), 2, big); break;
            case DataKind.UInt16: bytes = IntegerBytes(ushort.Parse(value, culture), 2, big); break;
            case DataKind.Int32: bytes = IntegerBytes(unchecked((ulong)int.Parse(value, culture)), 4, big); break;
            case DataKind.UInt32: bytes = IntegerBytes(uint.Parse(value, culture), 4, big); break;
            case DataKind.Int64: bytes = IntegerBytes(unchecked((ulong)long.Parse(value, culture)), 8, big); break;
            case DataKind.UInt64: bytes = IntegerBytes(ulong.Parse(value, culture), 8, big); break;
            case DataKind.Float16:
                var half = Half.Parse(value, NumberStyles.Float, culture);
                bytes = IntegerBytes(BitConverter.HalfToUInt16Bits(half), 2, big);
                normalized = half.ToString("R", culture);
                note = FloatNote(BitConverter.HalfToUInt16Bits(half), 5, 10, 15);
                break;
            case DataKind.Float32:
                var single = float.Parse(value, NumberStyles.Float, culture);
                bytes = IntegerBytes(BitConverter.SingleToUInt32Bits(single), 4, big);
                normalized = single.ToString("R", culture);
                note = FloatNote(BitConverter.SingleToUInt32Bits(single), 8, 23, 127);
                break;
            case DataKind.Float64:
                var dbl = double.Parse(value, NumberStyles.Float, culture);
                bytes = IntegerBytes(BitConverter.DoubleToUInt64Bits(dbl), 8, big);
                normalized = dbl.ToString("R", culture);
                note = FloatNote(BitConverter.DoubleToUInt64Bits(dbl), 11, 52, 1023);
                break;
            case DataKind.Decimal:
                var dec = decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, culture);
                bytes = decimal.GetBits(dec).SelectMany(word => IntegerBytes(unchecked((uint)word), 4, big)).ToArray();
                normalized = dec.ToString(culture);
                note = "Word order: low · middle · high · flags. The flags word stores sign and decimal scale.";
                break;
            case DataKind.Boolean:
                var boolean = value.ToLowerInvariant() switch
                {
                    "true" or "1" => true,
                    "false" or "0" => false,
                    _ => throw new FormatException("Enter true, false, 1, or 0.")
                };
                bytes = [(byte)(boolean ? 1 : 0)];
                normalized = boolean ? "true" : "false";
                break;
            case DataKind.Hex:
                if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value[2..];
                bytes = System.Convert.FromHexString(string.Concat(value.Where(c => !char.IsWhiteSpace(c))));
                normalized = System.Convert.ToHexString(bytes);
                break;
            case DataKind.Base64:
                bytes = System.Convert.FromBase64String(value);
                normalized = System.Convert.ToBase64String(bytes);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new ConversionResult(input, bytes, normalized, note);
    }

    private static byte[] IntegerBytes(ulong bits, int width, bool big)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, bits);
        var bytes = buffer[..width].ToArray();
        if (big) Array.Reverse(bytes);
        return bytes;
    }

    private static string FloatNote(ulong bits, int exponentBits, int fractionBits, int bias)
    {
        var sign = bits >> (exponentBits + fractionBits);
        var exp = (bits >> fractionBits) & ((1UL << exponentBits) - 1);
        var fraction = bits & ((1UL << fractionBits) - 1);
        var classification = exp == (1UL << exponentBits) - 1
            ? (fraction == 0 ? "Infinity" : "NaN")
            : exp == 0 ? (fraction == 0 ? "Zero" : "Subnormal") : "Normal";
        var exponent = exp == 0 ? 1 - bias : (long)exp - bias;
        return $"{classification} · sign {sign} · exponent field {exp} · fraction 0x{fraction:X}" +
               (classification is "Normal" or "Subnormal" ? $" · unbiased exponent {exponent}" : "") +
               ". Finite input can round or overflow to Infinity at this precision.";
    }
}

public sealed class ConversionResult
{
    public string Input { get; }
    public byte[] Bytes { get; }
    public string Normalized { get; }
    public string Note { get; }
    public int OriginalUtf8Bytes { get; }
    public int UnicodeScalars { get; }
    public int BitCount => Bytes.Length * 8;
    public int Ones { get; }
    public int Zeros => BitCount - Ones;
    public int[] Histogram { get; } = new int[256];
    public double Entropy { get; }

    internal ConversionResult(string input, byte[] bytes, string normalized, string note)
    {
        Input = input;
        Bytes = bytes;
        Normalized = normalized;
        Note = note;
        OriginalUtf8Bytes = Encoding.UTF8.GetByteCount(input);
        UnicodeScalars = input.EnumerateRunes().Count();
        foreach (var b in bytes)
        {
            Ones += BitOperations.PopCount((uint)b);
            Histogram[b]++;
        }
        foreach (var count in Histogram.Where(c => c > 0))
        {
            var probability = (double)count / bytes.Length;
            Entropy -= probability * Math.Log2(probability);
        }
    }

    public string ToBinary(bool nibbles = false) => string.Join(" ", Bytes.Select(b => FormatByte(b, nibbles)));
    public string ToHex() => System.Convert.ToHexString(Bytes);
    public static string FormatByte(byte value, bool nibbles = false)
    {
        var bits = System.Convert.ToString(value, 2).PadLeft(8, '0');
        return nibbles ? bits[..4] + " " + bits[4..] : bits;
    }
}
