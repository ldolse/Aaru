/***************************************************************************
Aaru Data Preservation Suite
----------------------------------------------------------------------------

Filename       : Program.cs
Author(s)      : Translation Agent

Component      : Test Vector Generator for Partition Detection

--[ Description ] ----------------------------------------------------------

    Generates test vectors for partition detection.

--[ Usage ] ----------------------------------------------------------------

    dotnet run --project Aaru.Tests.Partitions

    Output: partitions_test_vectors.json

****************************************************************************/

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aaru.Tests.Partitions;

public static class Program
{
    public static void Main(string[] args)
    {
        var vectors = new Dictionary<string, List<PartitionTestVector>>();

        // Partition detection note
        vectors["partition_detection"] = new List<PartitionTestVector>
        {
            new PartitionTestVector
            {
                Name = "partition_detection_note",
                InputHex = "",
                ExpectedHex = "",
                Description = "Partition detection requires real disk images with known partition tables. " +
                             "Use integration tests with actual disk images containing MBR, GPT, etc."
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        string outputPath = args.Length > 0 ? args[0] : "partitions_test_vectors.json";
        string json = JsonSerializer.Serialize(vectors, options);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"Generated {vectors.Values.Sum(v => v.Count)} test vectors to {outputPath}");
    }
}

public class PartitionTestVector
{
    public string Name { get; set; } = "";
    public string InputHex { get; set; } = "";
    public string ExpectedHex { get; set; } = "";
    public string Description { get; set; } = "";
}
