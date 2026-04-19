/***************************************************************************
Aaru Data Preservation Suite
----------------------------------------------------------------------------

Filename       : Program.cs
Author(s)      : Translation Agent

Component      : Test Vector Generator for CD Decoders

--[ Description ] ----------------------------------------------------------

    Generates test vectors for verifying Python CD decoder implementations
    against the C# reference implementation.

--[ Usage ] ----------------------------------------------------------------

    dotnet run --project Aaru.Tests.Decoders

    Output: decoder_test_vectors.json

****************************************************************************/

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aaru.Decoders.CD;

namespace Aaru.Tests.Decoders;

public static class Program
{
    public static void Main(string[] args)
    {
        var vectors = new Dictionary<string, List<DecoderTestVector>>();

        // Sync pattern detection
        vectors["sync_detection"] = GenerateSyncDetectionVectors();

        // Sector scrambling
        vectors["sector_scramble"] = GenerateSectorScrambleVectors();

        // User data extraction
        vectors["user_data_mode1"] = GenerateUserDataMode1Vectors();

        // User data extraction Mode2
        vectors["user_data_mode2"] = GenerateUserDataMode2Vectors();

        // Sector header parsing
        vectors["sector_header"] = GenerateSectorHeaderVectors();

        // Write output
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        string outputPath = args.Length > 0 ? args[0] : "decoder_test_vectors.json";
        string json = JsonSerializer.Serialize(vectors, options);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"Generated {vectors.Values.Sum(v => v.Count)} test vectors to {outputPath}");
    }

    private static List<DecoderTestVector> GenerateSyncDetectionVectors()
    {
        var vectors = new List<DecoderTestVector>();

        // Valid CD sync pattern
        byte[] validSync = new byte[]
        {
            0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00
        };
        vectors.Add(new DecoderTestVector
        {
            Name = "valid_sync_pattern",
            InputHex = Convert.ToHexString(validSync).ToLower(),
            ExpectedHex = "true",
            Description = "Valid CD sector sync pattern"
        });

        // Invalid sync - wrong start byte
        byte[] invalidSync1 = new byte[]
        {
            0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00
        };
        vectors.Add(new DecoderTestVector
        {
            Name = "invalid_sync_wrong_start",
            InputHex = Convert.ToHexString(invalidSync1).ToLower(),
            ExpectedHex = "false",
            Description = "Invalid sync - first byte should be 0x00"
        });

        // Invalid sync - wrong end byte
        byte[] invalidSync2 = new byte[]
        {
            0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
        };
        vectors.Add(new DecoderTestVector
        {
            Name = "invalid_sync_wrong_end",
            InputHex = Convert.ToHexString(invalidSync2).ToLower(),
            ExpectedHex = "false",
            Description = "Invalid sync - last byte should be 0x00"
        });

        // Invalid sync - only 9 FF bytes
        byte[] invalidSync3 = new byte[]
        {
            0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0x00
        };
        vectors.Add(new DecoderTestVector
        {
            Name = "invalid_sync_too_short",
            InputHex = Convert.ToHexString(invalidSync3).ToLower(),
            ExpectedHex = "false",
            Description = "Invalid sync - only 9 FF bytes instead of 10"
        });

        // Sync from actual sector data (first 12 bytes)
        byte[] sectorWithSync = new byte[]
        {
            0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00,
            0x00, 0x02, 0x00, 0x02 // Header bytes
        };
        vectors.Add(new DecoderTestVector
        {
            Name = "sync_from_sector",
            InputHex = Convert.ToHexString(sectorWithSync[..12]).ToLower(),
            ExpectedHex = "true",
            Description = "Sync pattern from actual sector start"
        });

        return vectors;
    }

    private static List<DecoderTestVector> GenerateSectorScrambleVectors()
    {
        var vectors = new List<DecoderTestVector>();

        // Test scrambling a simple sector
        // Create a minimal sector with sync + header + data
        byte[] testSector = new byte[2352];
        
        // Sync pattern
        Array.Copy(Sector.SyncMark, 0, testSector, 0, 12);
        
        // Header (minute=0, second=2, sector=0, mode=1)
        testSector[12] = 0x00;
        testSector[13] = 0x02;
        testSector[14] = 0x00;
        testSector[15] = 0x01;
        
        // Fill data with pattern
        for (int i = 16; i < 2352; i++)
        {
            testSector[i] = (byte)(i % 256);
        }

        // Scramble it
        byte[] scrambled = Sector.Scramble(testSector);
        
        vectors.Add(new DecoderTestVector
        {
            Name = "scramble_mode1_sector",
            InputHex = Convert.ToHexString(testSector).ToLower(),
            ExpectedHex = Convert.ToHexString(scrambled).ToLower(),
            Description = "Scramble Mode 1 sector with incremental pattern"
        });

        // Unscramble should reverse it
        byte[] unscrambled = Sector.Scramble(scrambled);
        
        vectors.Add(new DecoderTestVector
        {
            Name = "unscramble_mode1_sector",
            InputHex = Convert.ToHexString(scrambled).ToLower(),
            ExpectedHex = Convert.ToHexString(unscrambled).ToLower(),
            Description = "Unscramble should restore original sector"
        });

        return vectors;
    }

    private static List<DecoderTestVector> GenerateUserDataMode1Vectors()
    {
        var vectors = new List<DecoderTestVector>();

        // Create a Mode 1 sector
        byte[] mode1Sector = new byte[2352];
        
        // Sync
        Array.Copy(Sector.SyncMark, 0, mode1Sector, 0, 12);
        
        // Header (minute=0, second=2, sector=0, mode=1)
        mode1Sector[12] = 0x00;
        mode1Sector[13] = 0x02;
        mode1Sector[14] = 0x00;
        mode1Sector[15] = 0x01;
        
        // User data (2048 bytes)
        for (int i = 0; i < 2048; i++)
        {
            mode1Sector[16 + i] = (byte)(i % 256);
        }
        
        // EDC/ECC (zeros for this test)
        // Remaining 288 bytes are zero

        // Extract user data
        byte[] userData = Sector.GetUserData(mode1Sector);
        
        vectors.Add(new DecoderTestVector
        {
            Name = "extract_mode1_user_data",
            InputHex = Convert.ToHexString(mode1Sector).ToLower(),
            ExpectedHex = Convert.ToHexString(userData).ToLower(),
            Description = "Extract 2048 bytes user data from Mode 1 sector"
        });

        return vectors;
    }

    private static List<DecoderTestVector> GenerateUserDataMode2Vectors()
    {
        var vectors = new List<DecoderTestVector>();

        // Create a Mode 2 Form 1 sector (2336 bytes)
        byte[] mode2Form1Sector = new byte[2336];
        
        // Sync
        Array.Copy(Sector.SyncMark, 0, mode2Form1Sector, 0, 12);
        
        // Header (minute=0, second=2, sector=0, mode=2)
        mode2Form1Sector[12] = 0x00;
        mode2Form1Sector[13] = 0x02;
        mode2Form1Sector[14] = 0x00;
        mode2Form1Sector[15] = 0x02;
        
        // Sub-header (file=0, channel=0, sub-mode=0x20 for Form 1)
        mode2Form1Sector[16] = 0x00; // File number
        mode2Form1Sector[17] = 0x00; // Channel number
        mode2Form1Sector[18] = 0x20; // Sub-mode (Form 1)
        mode2Form1Sector[19] = 0x00; // Coding info
        
        // User data (2048 bytes)
        for (int i = 0; i < 2048; i++)
        {
            mode2Form1Sector[20 + i] = (byte)(i % 256);
        }
        
        // EDC (4 bytes) - zeros for this test
        // Remaining bytes are zero

        // Extract user data
        byte[] userData = Sector.GetUserDataFromMode2(mode2Form1Sector);
        
        vectors.Add(new DecoderTestVector
        {
            Name = "extract_mode2_form1_user_data",
            InputHex = Convert.ToHexString(mode2Form1Sector).ToLower(),
            ExpectedHex = userData != null ? Convert.ToHexString(userData).ToLower() : "null",
            Description = "Extract 2048 bytes user data from Mode 2 Form 1 sector"
        });

        return vectors;
    }

    private static List<DecoderTestVector> GenerateSectorHeaderVectors()
    {
        var vectors = new List<DecoderTestVector>();

        // Test sector header extraction
        byte[] sector = new byte[2352];
        
        // Sync
        Array.Copy(Sector.SyncMark, 0, sector, 0, 12);
        
        // Header (minute=1, second=30, sector=15, mode=1)
        // In BCD: 0x01, 0x30, 0x15
        sector[12] = 0x01;
        sector[13] = 0x30;
        sector[14] = 0x15;
        sector[15] = 0x01;

        // Extract header (bytes 12-15)
        byte[] header = sector[12..16];
        
        vectors.Add(new DecoderTestVector
        {
            Name = "sector_header_lba_1_30_15",
            InputHex = Convert.ToHexString(header).ToLower(),
            ExpectedHex = "01301501",
            Description = "Sector header at LBA 1:30:15 Mode 1"
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
