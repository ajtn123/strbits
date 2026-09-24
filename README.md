# StrBits

A desktop binary inspector built with Avalonia 12 and .NET 10. Enter a value, select its type, and explore its storage representation and statistics as you type. All conversion happens locally.

## Run

Install the .NET 10 SDK, then run from this folder:

```sh
dotnet run --project src/StrBits
```

Open `StrBits.slnx` to work with the solution in an IDE. The desktop app uses Avalonia's Windows, macOS, and Linux backends. The included UI tests run without opening a desktop window; native macOS and Linux execution has not been verified here.

## Features

- Strings in UTF-8, UTF-16, UTF-32, or strict ASCII, without a byte order mark.
- Signed and unsigned 8-, 16-, 32-, and 64-bit integers.
- IEEE 754 half, single, and double precision floating point, with sign, exponent, fraction, and classification details.
- .NET decimal, Boolean, hexadecimal bytes, and Base64 bytes.
- Little or big endian byte order where applicable.
- Binary, hexadecimal, and Base64 output, optional nibble grouping, clipboard copying, and text report export.
- Original UTF-8 size, Unicode scalar count, raw payload size, binary text size, bit counts, byte frequency, and Shannon byte entropy.
- Validation for invalid syntax, overflow, and text that cannot be encoded; an example is provided for every type.
- Light and dark styles follow the system appearance, including changes while the app is open.

Changing types preserves your input. Click **Use example** to load a valid value for the selected type. Numeric input uses a period as its decimal separator and does not accept digit grouping separators. Floating point accepts `NaN`, `Infinity`, and `-Infinity`; finite input can round or overflow to infinity.

## Reading the output

Bytes appear in storage order. Bits within each byte always run from most significant to least significant. For example, Int32 `42` in little endian is `00101010 00000000 00000000 00000000`, and in big endian is `00000000 00000000 00000000 00101010`.

UTF-16 and UTF-32 honor byte order. UTF-8, ASCII, single-byte types, hex, and Base64 do not need a byte order setting. Signed integers use two's complement. Decimal is explicitly serialized as four 32-bit words: low, middle, high, flags. Byte order applies within each word, with sign and scale in the flags word.

The statistics distinguish three sizes:

| Statistic | Meaning |
| --- | --- |
| Original text | Bytes needed to encode the exact input text as UTF-8, even when that text describes a number. |
| Raw binary storage | Bytes in the converted value; runtime object overhead is excluded. |
| Binary as text | Bytes to save the `0` and `1` digits as ASCII/UTF-8, excluding formatting spaces and line breaks. This is eight times the raw byte count. |

Unicode scalars differ from UTF-16 code units and visible characters: an emoji can use two code units, and a visible character can contain several scalars. Byte entropy ranges from 0 to 8 bits per byte and describes the observed distribution, not guaranteed compression savings. Empty input has zero entropy by convention. Histogram bars are scaled relative to the most frequent byte.

Input is limited to 32,768 UTF-16 code units. Output previews show up to 256 bytes (255 for a truncated Base64 preview to retain complete three-byte groups). Statistics, copying, and exports use the entire converted payload.

## Build and test

```sh
dotnet build StrBits.slnx -c Release
dotnet test StrBits.slnx -c Release
dotnet publish src/StrBits -c Release -o artifacts/app
```

The published app requires the .NET 10 runtime. On Windows, start `artifacts/app/StrBits.exe`.

The test suite covers storage patterns, numeric limits, Unicode, malformed input, byte order, decimal layout, special floating point values, statistics, preview limits, error recovery, UI input and selection bindings, clipboard output, and window rendering.

Avalonia's build telemetry can be disabled in restricted build environments with `AVALONIA_TELEMETRY_OPTOUT=1`. In PowerShell:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet test StrBits.slnx -c Release
```

## Project layout

- `src/StrBits.Core`: conversion and statistics, independent of the UI.
- `src/StrBits`: Avalonia views, view model, histogram, clipboard and export actions.
- `tests/StrBits.Tests`: xUnit tests and Avalonia headless UI tests.
