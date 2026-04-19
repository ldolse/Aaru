/***************************************************************************
Aaru Data Preservation Suite
----------------------------------------------------------------------------

Filename       : Program.cs
Author(s)      : Translation Agent

Component      : Test Vector Generator for Filesystem Identification

--[ Description ] ----------------------------------------------------------

    Generates test vectors for filesystem identification.

--[ Usage ] ----------------------------------------------------------------

    dotnet run --project Aaru.Tests.Filesystems

    Output: filesystems_test_vectors.json

****************************************************************************/

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aaru.Tests.Filesystems;

public static class Program
{
    public static void Main(string[] args)
    {
        var vectors = new Dictionary<string, List<FilesystemTestVector>>();

        // Filesystem identification note
        vectors["filesystem_identify"] = new List<FilesystemTestVector>
        {
            new FilesystemTestVector
            {
                Name = "filesystem_identification_note",
                InputHex = "",
                ExpectedHex = "",
                Description = "Filesystem identification requires real image files with known filesystems. " +
                             "Use integration tests with actual CD/DVD images containing ISO9660, UDF, etc."
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        string outputPath = args.Length > 0 ? args[0] : "filesystems_test_vectors.json";
        string json = JsonSerializer.Serialize(vectors, options);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"Generated {vectors.Values.Sum(v => v.Count)} test vectors to {outputPath}");
    }
}

public class FilesystemTestVector
{
    public string Name { get; set; } = "";
    public string InputHex { get; set; } = "";
    public string ExpectedHex { get; set; } = "";
    public string Description { get; set; } = "";
}
