using EqSkyTracker.Core;

namespace EqSkyTracker.Tests;

public class ReadDumpLinesTests
{
    [Fact]
    public void Utf8FileDecodesNormally()
    {
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "C\tPrimary Class Unlock - Bard\r\nC\t\tObtain Mask of Song.\r\n");
            Assert.Equal(
                ["C\tPrimary Class Unlock - Bard", "C\t\tObtain Mask of Song.", ""],
                DumpFile.ReadDumpLines(tmp));
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Cp1252BytesFallBackInsteadOfRaising()
    {
        // Regression test: EQ-style clients don't guarantee UTF-8 output --
        // a curly apostrophe (cp1252 0x92) is invalid UTF-8 and used to raise
        // and crash the parse.
        string tmp = Path.GetTempFileName();
        try
        {
            // cp1252 byte 0x92 is the curly apostrophe (U+2019); everything
            // else in this line is plain ASCII, so build the raw bytes by
            // hand rather than depending on the codepage provider being
            // registered at this point in the test run.
            byte[] raw =
            [
                .. "C\t\tObtain Ry"u8.ToArray(),
                0x92,
                .. "Gorr Talisman.\r\n"u8.ToArray(),
            ];
            File.WriteAllBytes(tmp, raw);
            List<string> lines = DumpFile.ReadDumpLines(tmp);
            Assert.Equal("C\t\tObtain Ry’Gorr Talisman.", lines[0]);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void Utf8BomIsStripped()
    {
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tmp, [0xEF, 0xBB, 0xBF, .. "C\tPrimary Class Unlock - Bard\r\n"u8.ToArray()]);
            Assert.Equal("C\tPrimary Class Unlock - Bard", DumpFile.ReadDumpLines(tmp)[0]);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
