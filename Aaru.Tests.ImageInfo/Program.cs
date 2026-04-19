/***************************************************************************
Aaru Data Preservation Suite
----------------------------------------------------------------------------

Filename       : Program.cs
Author(s)      : Aaru Test Team

Component      : Test Vector Generator for Image Info

--[ Description ] ----------------------------------------------------------

    Generates JSON dumps of optical media image information for testing.
    Reuses existing Aaru.Core.ImageInfo and CLI logic.

--[ Usage ] ----------------------------------------------------------------

    dotnet run --project Aaru.Tests.ImageInfo -- input.ccd output.json [fixture-name]

    Output: JSON file with complete image metadata and sector checksums

****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aaru.CommonTypes;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Interfaces;
using Aaru.CommonTypes.Structs;
using Aaru.Core;
using Aaru.Filters;
using Aaru.Images;
using Aaru.Logging;
using Track = Aaru.CommonTypes.Structs.Track;

namespace Aaru.Tests.ImageInfo;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run -- <input-image> <output-json> [fixture-name]");
            Console.WriteLine("  input-image:   Path to optical media image file");
            Console.WriteLine("  output-json:   Path for JSON output");
            Console.WriteLine("  fixture-name:  Optional fixture identifier");
            return;
        }

        string inputPath = args[0];
        string outputPath = args[1];
        string fixtureName = args.Length > 2 ? args[2] : Path.GetFileNameWithoutExtension(inputPath);

        // Get absolute paths
        inputPath = Path.GetFullPath(inputPath);
        outputPath = Path.GetFullPath(outputPath);

        // CloneCD requires working directory to be same as .ccd file
        // because it looks for .img/.sub files relative to CWD, not .ccd location
        string inputDir = Path.GetDirectoryName(inputPath);
        string originalDir = Directory.GetCurrentDirectory();
        
        if (!string.IsNullOrEmpty(inputDir))
        {
            Directory.SetCurrentDirectory(inputDir);
            inputPath = Path.GetFileName(inputPath);
        }

        Console.WriteLine($"Generating test dump for: {inputPath}");
        Console.WriteLine($"Working directory: {Directory.GetCurrentDirectory()}");

        try
        {
            // Initialize plugins
            var pluginRegister = PluginRegister.Singleton;
            
            // Get all plugin registrators from referenced assemblies
            var registrators = new List<IPluginRegister>();
            
            // Add filters
            registrators.Add(new Filters.Register());
            // Add image formats
            registrators.Add(new Images.Register());
            
            // Initialize plugins
            pluginRegister.InitPlugins(registrators);
            
            // Get filter using plugin detection
            IFilter inputFilter = pluginRegister.GetFilter(inputPath);
            if (inputFilter == null)
            {
                Console.WriteLine($"ERROR: Cannot open file: {inputPath}");
                return;
            }

            IBaseImage imageFormat = ImageFormat.Detect(inputFilter);
            if (imageFormat == null)
            {
                Console.WriteLine("ERROR: Cannot identify image format");
                return;
            }

            ErrorNumber opened = imageFormat.Open(inputFilter);
            if (opened != ErrorNumber.NoError)
            {
                Console.WriteLine($"ERROR: Cannot open image: {opened}");
                return;
            }

            // Generate JSON dump
            var dump = GenerateDump(imageFormat, fixtureName, inputPath);

            // Write JSON
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(dump, options);
            File.WriteAllText(outputPath, json);

            Console.WriteLine($"Generated dump:");
            Console.WriteLine($"  Tracks: {dump.Tracks.Count}");
            Console.WriteLine($"  Total sectors: {dump.ImageInfo.Sectors}");
            Console.WriteLine($"  Sector samples: {dump.SectorSamples.Count}");
            Console.WriteLine($"  Output: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            // Restore original working directory
            if (!string.IsNullOrEmpty(originalDir))
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }
    }

    private static ImageDump GenerateDump(IBaseImage image, string fixtureName, string sourceFile)
    {
        var dump = new ImageDump
        {
            FixtureName = fixtureName,
            SourceFile = sourceFile,
            GeneratedAt = DateTime.UtcNow,
            ImageInfo = new ImageInfoData
            {
                MediaType = image.Info.MediaType.ToString(),
                Sectors = image.Info.Sectors,
                SectorSize = image.Info.SectorSize,
                ImageSize = image.Info.ImageSize,
                CreationTime = image.Info.CreationTime != DateTime.MinValue ? image.Info.CreationTime : null,
                LastModificationTime = image.Info.LastModificationTime != DateTime.MinValue ? image.Info.LastModificationTime : null
            },
            Tracks = new List<TrackData>(),
            Metadata = new MetadataData(),
            SectorSamples = new List<SectorSample>()
        };

        // Add tracks
        if (image is IOpticalMediaImage optical)
        {
            foreach (var track in optical.Tracks)
            {
                dump.Tracks.Add(new TrackData
                {
                    Sequence = (int)track.Sequence,
                    StartSector = (long)track.StartSector,
                    EndSector = (long)track.EndSector,
                    BytesPerSector = (int)track.BytesPerSector
                });
            }

            // Add readable tags
            dump.Metadata.ReadableMediaTags = image.Info.ReadableMediaTags?
                .Select(t => t.ToString()).ToList();
            dump.Metadata.ReadableSectorTags = image.Info.ReadableSectorTags?
                .Select(t => t.ToString()).ToList();

            // Sample sectors
            dump.SectorSamples = SampleSectors(optical, image.Info.Sectors);
        }

        return dump;
    }

    private static List<SectorSample> SampleSectors(IOpticalMediaImage optical, ulong totalSectors)
    {
        var samples = new List<SectorSample>();

        if (totalSectors == 0)
            return samples;

        // Sample strategy: first, last, and evenly spaced (10 total)
        int sampleCount = Math.Min(10, (int)totalSectors);
        var addresses = new List<ulong>();

        if (sampleCount == 1)
        {
            addresses.Add(0);
        }
        else
        {
            addresses.Add(0); // First
            addresses.Add(totalSectors - 1); // Last

            // Evenly spaced in between
            int remaining = sampleCount - 2;
            for (int i = 1; i <= remaining; i++)
            {
                ulong addr = ((ulong)i * (totalSectors - 1)) / (ulong)(remaining + 1);
                addresses.Add(addr);
            }
            addresses.Sort();
        }

        // Read and checksum (use ReadSectorLong for raw sectors)
        foreach (ulong addr in addresses)
        {
            try
            {
                // Try ReadSectorLong first (raw sectors) - track 1
                ErrorNumber errno = optical.ReadSectorLong(addr, 1, out byte[] data, out SectorStatus status);

                if (errno != ErrorNumber.NoError || data == null)
                {
                    // Fall back to regular ReadSector
                    errno = optical.ReadSector(addr, false, out data, out _);
                }

                if (errno == ErrorNumber.NoError && data != null)
                {
                    using var md5 = MD5.Create();
                    byte[] hash = md5.ComputeHash(data);

                    samples.Add(new SectorSample
                    {
                        Address = (long)addr,
                        Md5 = BitConverter.ToString(hash).Replace("-", "").ToLower(),
                        Size = data.Length,
                        Description = addr == 0 ? "First sector" :
                                    addr == totalSectors - 1 ? "Last sector" :
                                    $"Sample at {(double)addr / totalSectors * 100:F1}%"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not read sector {addr}: {ex.Message}");
            }
        }

        return samples;
    }
}

public class ImageDump
{
    public string FixtureName { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public ImageInfoData ImageInfo { get; set; } = new();
    public List<TrackData> Tracks { get; set; } = new();
    public MetadataData Metadata { get; set; } = new();
    public List<SectorSample> SectorSamples { get; set; } = new();
}

public class ImageInfoData
{
    public string MediaType { get; set; } = "";
    public ulong Sectors { get; set; }
    public uint SectorSize { get; set; }
    public ulong ImageSize { get; set; }
    public DateTime? CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
}

public class TrackData
{
    public int Sequence { get; set; }
    public long StartSector { get; set; }
    public long EndSector { get; set; }
    public int BytesPerSector { get; set; }
}

public class MetadataData
{
    public List<string>? ReadableMediaTags { get; set; }
    public List<string>? ReadableSectorTags { get; set; }
}

public class SectorSample
{
    public long Address { get; set; }
    public string Md5 { get; set; } = "";
    public int Size { get; set; }
    public string Description { get; set; } = "";
}
