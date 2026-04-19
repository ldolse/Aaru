/***************************************************************************
Aaru Data Preservation Suite
----------------------------------------------------------------------------

Filename       : Program.cs
Author(s)      : Translation Agent

Component      : Test Vector Generator for Filter Functions

--[ Description ] ----------------------------------------------------------

    Generates test vectors by CALLING ACTUAL C# LIBRARY FUNCTIONS
    for filter identification.

--[ Usage ] ----------------------------------------------------------------

    dotnet run --project Aaru.Tests.Filters

    Output: filter_test_vectors.json

****************************************************************************/

using System.Text.Json;
using System.Text.Json.Serialization;
using Aaru.Filters;

namespace Aaru.Tests.Filters;

public static class Program
{
    public static void Main(string[] args)
    {
        var vectors = new Dictionary<string, List<FilterTestVector>>();

        // Filter identification tests
        vectors["filter_identify"] = GenerateFilterIdentifyVectors();

        // Write output
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        string outputPath = args.Length > 0 ? args[0] : "filter_test_vectors.json";
        string json = JsonSerializer.Serialize(vectors, options);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"Generated {vectors.Values.Sum(v => v.Count)} test vectors to {outputPath}");
    }

    private static List<FilterTestVector> GenerateFilterIdentifyVectors()
    {
        var vectors = new List<FilterTestVector>();

        // Test GZip identification
        byte[] gzipData = new byte[]
        {
            0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03
        };
        
        var gzipFilter = new GZip();
        bool gzipIdentifies = gzipFilter.Identify(gzipData);
        
        vectors.Add(new FilterTestVector
        {
            Name = "gzip_identify_valid",
            InputHex = Convert.ToHexString(gzipData).ToLower(),
            ExpectedHex = gzipIdentifies ? "true" : "false",
            Description = "GZip filter identifies valid gzip header"
        });

        // Test non-gzip data
        byte[] nonGzipData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        bool nonGzipIdentifies = gzipFilter.Identify(nonGzipData);
        
        vectors.Add(new FilterTestVector
        {
            Name = "gzip_identify_invalid",
            InputHex = Convert.ToHexString(nonGzipData).ToLower(),
            ExpectedHex = nonGzipIdentifies ? "true" : "false",
            Description = "GZip filter rejects non-gzip data"
        });

        return vectors;
    }
}

/// <summary>
/// Test vector for filter functions.
/// </summary>
public class FilterTestVector
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
