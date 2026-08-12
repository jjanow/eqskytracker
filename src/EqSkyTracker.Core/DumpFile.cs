// Shared decoding for EverQuest-style /outputfile dumps.
//
// These files are written by the game client, which is not guaranteed to
// encode non-ASCII bytes (curly quotes, accented letters in item/NPC names)
// as UTF-8 -- older or non-English-locale Windows clients commonly emit the
// system codepage (e.g. cp1252) instead. Decoding must never blow up the
// parse over a single unexpected byte, so this tries UTF-8 first (the
// correct case) and falls back to progressively more permissive decodings.
using System.Text;

namespace EqSkyTracker.Core;

public static class DumpFile
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding Cp1252;

    static DumpFile()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp1252 = Encoding.GetEncoding(
            1252,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    public static string ReadDumpText(string path)
    {
        byte[] raw = File.ReadAllBytes(path);

        // "utf-8-sig": strip a leading BOM if present, then decode strictly as UTF-8.
        ReadOnlySpan<byte> bom = [0xEF, 0xBB, 0xBF];
        ReadOnlySpan<byte> body = raw.AsSpan().StartsWith(bom) ? raw.AsSpan(bom.Length) : raw.AsSpan();
        try
        {
            return StrictUtf8.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            // fall through to cp1252
        }

        try
        {
            return Cp1252.GetString(raw);
        }
        catch (DecoderFallbackException)
        {
            // fall through to latin-1
        }

        // Latin-1 (ISO-8859-1) maps every byte 0-255 to a codepoint 1:1, so
        // this can never fail -- it's the guaranteed-success final fallback,
        // matching Python's decode("latin-1", errors="replace").
        return Encoding.Latin1.GetString(raw);
    }

    /// <summary>
    /// Lines with trailing \r\n / \n stripped, without splitting on the wider
    /// set of line boundaries some line-splitting helpers recognize (some of
    /// which could otherwise appear in a cp1252-decoded line and split it
    /// unexpectedly). Deliberately mirrors Python's str.split("\n") + rstrip("\r"),
    /// not a readlines()-style API -- see the trailing-blank-line tests this
    /// exact behavior is required for.
    /// </summary>
    public static List<string> ReadDumpLines(string path)
    {
        string text = ReadDumpText(path);
        string[] parts = text.Split('\n');
        var lines = new List<string>(parts.Length);
        foreach (string part in parts)
        {
            lines.Add(part.TrimEnd('\r'));
        }
        return lines;
    }
}
