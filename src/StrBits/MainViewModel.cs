using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using StrBits.Core;

namespace StrBits;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public const int PreviewBytes = 256;
    private string _input = "Hello, binary! 👋";
    private TypeOption _selectedType = BinaryConverter.Types[0];
    private int _encodingIndex;
    private int _byteOrderIndex;
    private bool _groupNibbles;
    private string _status = "";
    private int _outputTab;

    public MainViewModel() => Refresh();
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<TypeOption> Types => BinaryConverter.Types;
    public string[] Encodings { get; } = ["UTF-8", "UTF-16", "UTF-32", "ASCII"];
    public string[] ByteOrders { get; } = ["Little endian", "Big endian"];

    public string Input
    {
        get => _input;
        set { if (Set(ref _input, value ?? "")) Refresh(); }
    }
    public TypeOption SelectedType
    {
        get => _selectedType;
        set { if (value is not null && Set(ref _selectedType, value)) Refresh(); }
    }
    public int EncodingIndex
    {
        get => _encodingIndex;
        set { if (value is >= 0 and <= 3 && Set(ref _encodingIndex, value)) Refresh(); }
    }
    public int ByteOrderIndex
    {
        get => _byteOrderIndex;
        set { if (value is >= 0 and <= 1 && Set(ref _byteOrderIndex, value)) Refresh(); }
    }
    public bool GroupNibbles
    {
        get => _groupNibbles;
        set { if (Set(ref _groupNibbles, value)) Refresh(); }
    }
    public int OutputTab { get => _outputTab; set => Set(ref _outputTab, value); }
    public string Status
    {
        get => _status;
        set
        {
            if (Set(ref _status, value)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatus)));
        }
    }
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    public ConversionResult? Result { get; private set; }
    public bool IsText => SelectedType.Kind == DataKind.Text;
    public bool HasByteOrder => IsText ? EncodingIndex is 1 or 2 : SelectedType.Kind is not
        (DataKind.Int8 or DataKind.UInt8 or DataKind.Boolean or DataKind.Hex or DataKind.Base64);
    public bool IsValid => Result is not null;
    public bool HasError => !IsValid;
    public string Error { get; private set; } = "";
    public string TypeDescription => SelectedType.Description;
    public string InputCount => $"{Input.Length:N0} / {BinaryConverter.MaxInputLength:N0} UTF-16 code units";
    public string BinaryPreview { get; private set; } = "";
    public string HexPreview { get; private set; } = "";
    public string Base64Preview { get; private set; } = "";
    public string PreviewCaption => Result is null ? "" :
        Result.Bytes.Length > PreviewBytes ? $"Preview: up to {PreviewBytes:N0} of {Result.Bytes.Length:N0} bytes · copy and export include all bytes" :
        $"{Result.Bytes.Length:N0} bytes";
    public string OriginalSize => Result is null ? "—" : $"{Result.OriginalUtf8Bytes:N0} B";
    public string OriginalDetail => Result is null ? "UTF-8 input text" : $"UTF-8 input · {Result.UnicodeScalars:N0} Unicode scalars";
    public string PayloadSize => Result is null ? "—" : $"{Result.Bytes.Length:N0} B";
    public string PayloadDetail => Result is null ? "Encoded value" : $"{Result.BitCount:N0} bits · {SizeComparison}";
    private string SizeComparison => Result is null || Result.OriginalUtf8Bytes == 0 ? "ratio n/a" :
        $"{(double)Result.Bytes.Length / Result.OriginalUtf8Bytes:0.##}× input UTF-8";
    public string BinaryTextSize => Result is null ? "—" : $"{Result.BitCount:N0} B";
    public string BinaryTextDetail => "ASCII digits only · excludes spaces and line breaks";
    public string Entropy => Result is null ? "—" : $"{Result.Entropy:0.000}";
    public string EntropyDetail => "Shannon byte entropy · bits / byte (0–8)";
    public string Ones => Result is null ? "—" : $"{Result.Ones:N0}";
    public string Zeros => Result is null ? "—" : $"{Result.Zeros:N0}";
    public double OnesPercent => Result is null || Result.BitCount == 0 ? 0 : 100.0 * Result.Ones / Result.BitCount;
    public string BalanceCaption => Result is null ? "No data" : $"{OnesPercent:0.0}% ones · {Result.Histogram.Count(c => c > 0):N0} distinct byte values";
    public int[] Histogram => Result?.Histogram ?? new int[256];
    public string Interpretation => Result is null ? "Select a type and enter a value to explore its representation." :
        (string.IsNullOrEmpty(Result.Note) ? TypeDescription : Result.Note);
    public bool ShowInterpretation => IsValid && !IsText;
    public string StoredValue => Result is null ? "—" : Result.Normalized.Length == 0 ? "(empty)" :
        Result.Normalized.Length > 200 ? Result.Normalized[..200] + "…" : Result.Normalized;
    public string LayoutCaption => IsText ? Encodings[EncodingIndex] + (HasByteOrder ? " · " + ByteOrders[ByteOrderIndex] : "") :
        HasByteOrder ? ByteOrders[ByteOrderIndex] : "Byte order preserved";

    public void LoadExample() => Input = SelectedType.Example;

    public void Refresh()
    {
        try
        {
            Result = BinaryConverter.Convert(Input, SelectedType.Kind, (TextEncoding)EncodingIndex, (ByteOrder)ByteOrderIndex);
            Error = "";
            var preview = Result.Bytes.Take(PreviewBytes).ToArray();
            var lineWidth = GroupNibbles ? 4 : 8;
            BinaryPreview = string.Join(Environment.NewLine, preview.Chunk(lineWidth)
                .Select(line => string.Join("  ", line.Select(b => ConversionResult.FormatByte(b, GroupNibbles)))));
            HexPreview = string.Join(Environment.NewLine, preview.Chunk(16)
                .Select(line => string.Join(" ", line.Select(b => b.ToString("X2")))));
            // Truncate at a complete Base64 quantum, so the preview is a prefix of the full output.
            Base64Preview = Convert.ToBase64String(Result.Bytes.AsSpan(0,
                Result.Bytes.Length > PreviewBytes ? PreviewBytes / 3 * 3 : Result.Bytes.Length));
            Status = "";
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or EncoderFallbackException)
        {
            Result = null;
            BinaryPreview = HexPreview = Base64Preview = "";
            Error = ex switch
            {
                OverflowException => "This value is outside the selected type’s range. Choose a wider type or adjust the input.",
                EncoderFallbackException => "This text cannot be represented in the selected encoding. Try UTF-8 or check for incomplete Unicode characters.",
                _ => $"Invalid {SelectedType.Name.Split('·')[0].Trim()} value. {ex.Message}"
            };
            Status = "";
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public string GetCopyText() => Result is null ? "" : OutputTab switch
    {
        1 => Result.ToHex(),
        2 => Convert.ToBase64String(Result.Bytes),
        _ => Result.ToBinary(GroupNibbles)
    };

    public string CreateReport()
    {
        if (Result is null) return "";
        var culture = CultureInfo.InvariantCulture;
        return $"StrBits — binary inspection\nType: {SelectedType.Name}\nLayout: {LayoutCaption}\n" +
            $"Original input:\n{Input}\n\nStored value:\n{Result.Normalized}\n\n" +
            $"UTF-8 input bytes: {Result.OriginalUtf8Bytes}\nUnicode scalars: {Result.UnicodeScalars}\n" +
            $"UTF-16 code units: {Input.Length}\nPayload bytes: {Result.Bytes.Length}\nPayload bits: {Result.BitCount}\n" +
            $"Binary text bytes (ASCII digits only): {Result.BitCount}\nOnes: {Result.Ones}\nZeros: {Result.Zeros}\n" +
            $"Byte entropy (bits/byte): {Result.Entropy.ToString("F6", culture)}\n{Interpretation}\n\n" +
            $"Binary (storage order, MSB first per byte):\n{Result.ToBinary(GroupNibbles)}\n\n" +
            $"Hex:\n{Result.ToHex()}\n\nBase64:\n{Convert.ToBase64String(Result.Bytes)}\n";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
