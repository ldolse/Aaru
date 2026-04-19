/***************************************************************************
Aaru Data Preservation Suite
----------------------------------------------------------------------------

Filename       : Program.cs
Author(s)      : Translation Agent

Component      : Test Vector Generator for CD Subchannel Decoders

--[ Description ] ----------------------------------------------------------

    Generates test vectors for subchannel decoder functions.

--[ Usage ] ----------------------------------------------------------------

    dotnet run --project Aaru.Tests.Subchannel

    Output: subchannel_test_vectors.json

****************************************************************************/

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aaru.Tests.Subchannel;

public static class Program
{
    public static void Main(string[] args)
    {
        var vectors = new Dictionary<string, List<DecoderTestVector>>();

        // BCD conversion
        vectors["bcd_conversion"] = GenerateBcdConversionVectors();

        // Write output
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        string outputPath = args.Length > 0 ? args[0] : "subchannel_test_vectors.json";
        string json = JsonSerializer.Serialize(vectors, options);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"Generated {vectors.Values.Sum(v => v.Count)} test vectors to {outputPath}");
    }

    private static List<DecoderTestVector> GenerateBcdConversionVectors()
    {
        var vectors = new List<DecoderTestVector>();

        // BCD to byte
        vectors.Add(new DecoderTestVector
        {
            Name = "bcd_0x00_to_byte",
            InputHex = "00",
            ExpectedHex = "00",
            Description = "BCD 0x00 to byte 0"
        });

        vectors.Add(new DecoderTestVector
        {
            Name = "bcd_0x09_to_byte",
            InputHex = "09",
            ExpectedHex = "09",
            Description = "BCD 0x09 to byte 9"
        });

        vectors.Add(new DecoderTestVector
        {
            Name = "bcd_0x10_to_byte",
            InputHex = "10",
            ExpectedHex = "0a",
            Description = "BCD 0x10 to byte 10"
        });

        vectors.Add(new DecoderTestVector
        {
            Name = "bcd_0x59_to_byte",
            InputHex = "59",
            ExpectedHex = "3b",
            Description = "BCD 0x59 to byte 59"
        });

        vectors.Add(new DecoderTestVector
        {
            Name = "bcd_0x99_to_byte",
            InputHex = "99",
            ExpectedHex = "63",
            Description = "BCD 0x99 to byte 99"
        });

        // Byte to BCD
        vectors.Add(new DecoderTestVector
        {
            Name = "byte_0_to_bcd",
            InputHex = "00",
            ExpectedHex = "00",
            Description = "Byte 0 to BCD 0x00"
        });

        vectors.Add(new DecoderTestVector
        {
            Name = "byte_9_to_bcd",
            InputHex = "09",
            ExpectedHex = "09",
            Description = "Byte 9 to BCD 0x09"
        });

        vectors.Add(new DecoderTestVector
        {
            Name = "byte_10_to_bcd",
            InputHex = "0a",
            ExpectedHex = "10",
            Description = "Byte 10 to BCD 0x10"
        });

        vectors.Add(new DecoderTestVector
        {
            Name = "byte_59_to_bcd",
            InputHex = "3b",
            ExpectedHex = "59",
            Description = "Byte 59 to BCD 0x59"
        });

        vectors.Add(new DecoderTestVector
        {
            Name = "byte_99_to_bcd",
            InputHex = "63",
            ExpectedHex = "99",
            Description = "Byte 99 to BCD 0x99"
        });

        return vectors;
    }
}

/// <summary>
/// Test vector for decoder functions.
/// </summary>
public class DecoderTestVector
{
    /// <summary>
    /// Test case name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Input data as hex string.
    /// </summary>
    public string InputHex { get; set; } = "";

    /// <summary>
    /// Expected output as hex string.
    /// </summary>
    public string ExpectedHex { get; set; } = "";

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string Description { get; set; } = "";
}
