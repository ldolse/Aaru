// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Program.cs
// Author(s)      : Translation Agent
//
// Component      : Test Vector Generator
//
// --[ Description ] ----------------------------------------------------------
//
//     Generates test vectors for verifying Python checksum implementations
//     against the C# reference implementation.
//
// --[ Usage ] ----------------------------------------------------------------
//
//     dotnet run --project Aaru.Tests.ChecksumVectors
//
//     Outputs: checksum_test_vectors.json
//
// ****************************************************************************/

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aaru.Checksums;

namespace Aaru.Tests.ChecksumVectors;

public static class Program
{
    public static void Main(string[] args)
    {
        var vectors = new Dictionary<string, List<ChecksumTestVector>>();

        // Adler-32
        vectors["adler32"] = GenerateAdler32Vectors();

        // CRC-32
        vectors["crc32"] = GenerateCrc32Vectors();

        // CRC-16-CCITT
        vectors["crc16_ccitt"] = GenerateCrc16CcittVectors();

        // CRC-16-IBM
        vectors["crc16_ibm"] = GenerateCrc16IbmVectors();

        // CRC-64
        vectors["crc64"] = GenerateCrc64Vectors();

        // Fletcher-32
        vectors["fletcher32"] = GenerateFletcher32Vectors();

        // MD5
        vectors["md5"] = GenerateMd5Vectors();

        // SHA-1
        vectors["sha1"] = GenerateSha1Vectors();

        // SHA-256
        vectors["sha256"] = GenerateSha256Vectors();

        // SHA-384
        vectors["sha384"] = GenerateSha384Vectors();

        // SHA-512
        vectors["sha512"] = GenerateSha512Vectors();

        // SpamSum
        vectors["spamsum"] = GenerateSpamSumVectors();

        // Write output
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        string outputPath = args.Length > 0 ? args[0] : "checksum_test_vectors.json";
        string json = JsonSerializer.Serialize(vectors, options);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"Generated {vectors.Values.Sum(v => v.Count)} test vectors to {outputPath}");
    }

    private static List<ChecksumTestVector> GenerateAdler32Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        void AddVector(string name, byte[] data)
        {
            var ctx = new Adler32Context();
            ctx.Update(data);
            byte[] result = ctx.Final();
            uint value = BitConverter.ToUInt32(result.Reverse().ToArray(), 0);
            vectors.Add(new ChecksumTestVectorHex(name, data, value));
        }

        // Empty buffer
        vectors.Add(new ChecksumTestVectorHex("empty", [], 0x00000001));

        // Single byte - 'A' (0x41)
        AddVector("single_byte_A", [0x41]);

        // Single byte - 0x00
        AddVector("single_byte_zero", [0x00]);

        // Simple string - "ABC"
        AddVector("string_ABC", [0x41, 0x42, 0x43]);

        // "hello" in ASCII
        AddVector("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F]);

        // 256 bytes of pattern 0x00-0xFF
        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        AddVector("pattern_0xff", fullPattern);

        // All zeros (1KB)
        var zeros1k = new byte[1024];
        AddVector("zeros_1kb", zeros1k);

        // All 0xFF (1KB)
        var ff1k = new byte[1024];
        Array.Fill(ff1k, (byte)0xFF);
        AddVector("ff_1kb", ff1k);

        // Incremental update test
        var ctx = new Adler32Context();
        ctx.Update([0x41, 0x42, 0x43]);
        ctx.Update([0x44, 0x45, 0x46]);
        byte[] incrementalResult = ctx.Final();
        uint incrementalValue = BitConverter.ToUInt32(incrementalResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("incremental_ABCDEF", [0x41, 0x42, 0x43, 0x44, 0x45, 0x46], incrementalValue));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateCrc32Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        void AddVector(string name, byte[] data)
        {
            var ctx = new Crc32Context();
            ctx.Update(data);
            byte[] result = ctx.Final();
            uint value = BitConverter.ToUInt32(result.Reverse().ToArray(), 0);
            vectors.Add(new ChecksumTestVectorHex(name, data, value));
        }

        // Empty buffer
        vectors.Add(new ChecksumTestVectorHex("empty", [], 0x00000000));

        // Single byte - 0x00
        AddVector("single_byte_zero", [0x00]);

        // Single byte - 0xFF
        AddVector("single_byte_ff", [0xFF]);

        // Simple string - "A"
        AddVector("single_byte_A", [0x41]);

        // Simple string - "ABC"
        AddVector("string_ABC", [0x41, 0x42, 0x43]);

        // "hello" in ASCII
        AddVector("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F]);

        // 256 bytes of pattern 0x00-0xFF
        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        AddVector("pattern_0xff", fullPattern);

        // All zeros (1KB)
        var zeros1k = new byte[1024];
        AddVector("zeros_1kb", zeros1k);

        // All 0xFF (1KB)
        var ff1k = new byte[1024];
        Array.Fill(ff1k, (byte)0xFF);
        AddVector("ff_1kb", ff1k);

        // "The quick brown fox jumps over the lazy dog" in ASCII
        AddVector("quick_brown_fox", Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog"));

        // Incremental update test
        var ctx = new Crc32Context();
        ctx.Update([0x41, 0x42, 0x43]);
        ctx.Update([0x44, 0x45, 0x46]);
        byte[] incrementalResult = ctx.Final();
        uint incrementalValue = BitConverter.ToUInt32(incrementalResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("incremental_ABCDEF", [0x41, 0x42, 0x43, 0x44, 0x45, 0x46], incrementalValue));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateCrc16CcittVectors()
    {
        var vectors = new List<ChecksumTestVector>();

        // Empty buffer
        var ctxEmpty = new CRC16CcittContext();
        byte[] emptyResult = ctxEmpty.Final();
        ushort emptyValue = BitConverter.ToUInt16(emptyResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("empty", [], emptyValue));

        // Single byte - 0x00
        var ctx0 = new CRC16CcittContext();
        ctx0.Update([0x00]);
        byte[] result0 = ctx0.Final();
        ushort value0 = BitConverter.ToUInt16(result0.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("single_byte_zero", [0x00], value0));

        // Single byte - 0x41 ('A')
        var ctxA = new CRC16CcittContext();
        ctxA.Update([0x41]);
        byte[] resultA = ctxA.Final();
        ushort valueA = BitConverter.ToUInt16(resultA.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("single_byte_A", [0x41], valueA));

        // Single byte - 0xFF
        var ctxFF = new CRC16CcittContext();
        ctxFF.Update([0xFF]);
        byte[] resultFF = ctxFF.Final();
        ushort valueFF = BitConverter.ToUInt16(resultFF.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("single_byte_ff", [0xFF], valueFF));

        // Simple string - "A"
        var ctxSA = new CRC16CcittContext();
        ctxSA.Update([0x41]);
        byte[] resultSA = ctxSA.Final();
        ushort valueSA = BitConverter.ToUInt16(resultSA.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("string_A", [0x41], valueSA));

        // Simple string - "ABC"
        var ctxABC = new CRC16CcittContext();
        ctxABC.Update([0x41, 0x42, 0x43]);
        byte[] resultABC = ctxABC.Final();
        ushort valueABC = BitConverter.ToUInt16(resultABC.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("string_ABC", [0x41, 0x42, 0x43], valueABC));

        // Simple string - "AB" (2 bytes)
        var ctxAB = new CRC16CcittContext();
        ctxAB.Update([0x41, 0x42]);
        byte[] resultAB = ctxAB.Final();
        ushort valueAB = BitConverter.ToUInt16(resultAB.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("string_AB", [0x41, 0x42], valueAB));

        // "hello" in ASCII
        var ctxHello = new CRC16CcittContext();
        ctxHello.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F]);
        byte[] resultHello = ctxHello.Final();
        ushort valueHello = BitConverter.ToUInt16(resultHello.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F], valueHello));

        // 256 bytes of pattern 0x00-0xFF
        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var ctxPattern = new CRC16CcittContext();
        ctxPattern.Update(fullPattern);
        byte[] resultPattern = ctxPattern.Final();
        ushort valuePattern = BitConverter.ToUInt16(resultPattern.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("pattern_0xff", fullPattern, valuePattern));

        // All zeros (1KB)
        var zeros1k = new byte[1024];
        var ctxZeros = new CRC16CcittContext();
        ctxZeros.Update(zeros1k);
        byte[] resultZeros = ctxZeros.Final();
        ushort valueZeros = BitConverter.ToUInt16(resultZeros.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("zeros_1kb", zeros1k, valueZeros));

        // All 0xFF (1KB)
        var ff1k = new byte[1024];
        Array.Fill(ff1k, (byte)0xFF);
        var ctxFF1k = new CRC16CcittContext();
        ctxFF1k.Update(ff1k);
        byte[] resultFF1k = ctxFF1k.Final();
        ushort valueFF1k = BitConverter.ToUInt16(resultFF1k.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("ff_1kb", ff1k, valueFF1k));

        // Common CD-ROM sync pattern: 0x00 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0x00
        var syncPattern = new byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
        var ctxSync = new CRC16CcittContext();
        ctxSync.Update(syncPattern);
        byte[] resultSync = ctxSync.Final();
        ushort valueSync = BitConverter.ToUInt16(resultSync.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("cd_sync_pattern", syncPattern, valueSync));

        // Incremental update test
        var ctx = new CRC16CcittContext();
        ctx.Update([0x41, 0x42, 0x43]);
        ctx.Update([0x44, 0x45, 0x46]);
        byte[] incrResult = ctx.Final();
        ushort incrValue = BitConverter.ToUInt16(incrResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("incremental_ABCDEF", [0x41, 0x42, 0x43, 0x44, 0x45, 0x46], incrValue));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateCrc16IbmVectors()
    {
        var vectors = new List<ChecksumTestVector>();

        void AddVector(string name, byte[] data)
        {
            var ctx = new CRC16IbmContext();
            ctx.Update(data);
            byte[] result = ctx.Final();
            ushort value = BitConverter.ToUInt16(result.Reverse().ToArray(), 0);
            vectors.Add(new ChecksumTestVectorHex(name, data, value));
        }

        // Empty buffer
        var ctxEmpty = new CRC16IbmContext();
        byte[] emptyResult = ctxEmpty.Final();
        ushort emptyValue = BitConverter.ToUInt16(emptyResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("empty", [], emptyValue));

        AddVector("single_byte_zero", [0x00]);
        AddVector("single_byte_A", [0x41]);
        AddVector("single_byte_ff", [0xFF]);
        AddVector("string_ABC", [0x41, 0x42, 0x43]);
        AddVector("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F]);

        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        AddVector("pattern_0xff", fullPattern);

        var zeros1k = new byte[1024];
        AddVector("zeros_1kb", zeros1k);

        var ff1k = new byte[1024];
        Array.Fill(ff1k, (byte)0xFF);
        AddVector("ff_1kb", ff1k);

        var ctx = new CRC16IbmContext();
        ctx.Update([0x41, 0x42, 0x43]);
        ctx.Update([0x44, 0x45, 0x46]);
        byte[] incrResult = ctx.Final();
        ushort incrValue = BitConverter.ToUInt16(incrResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("incremental_ABCDEF", [0x41, 0x42, 0x43, 0x44, 0x45, 0x46], incrValue));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateCrc64Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        void AddVector(string name, byte[] data)
        {
            var ctx = new Crc64Context();
            ctx.Update(data);
            byte[] result = ctx.Final();
            ulong value = BitConverter.ToUInt64(result.Reverse().ToArray(), 0);
            vectors.Add(new ChecksumTestVectorHex(name, data, value));
        }

        var ctxEmpty = new Crc64Context();
        byte[] emptyResult = ctxEmpty.Final();
        ulong emptyValue = BitConverter.ToUInt64(emptyResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("empty", [], emptyValue));

        AddVector("single_byte_zero", [0x00]);
        AddVector("single_byte_A", [0x41]);
        AddVector("string_ABC", [0x41, 0x42, 0x43]);
        AddVector("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F]);

        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        AddVector("pattern_0xff", fullPattern);

        var zeros1k = new byte[1024];
        AddVector("zeros_1kb", zeros1k);

        var ff1k = new byte[1024];
        Array.Fill(ff1k, (byte)0xFF);
        AddVector("ff_1kb", ff1k);

        var ctx = new Crc64Context();
        ctx.Update([0x41, 0x42, 0x43]);
        ctx.Update([0x44, 0x45, 0x46]);
        byte[] incrResult = ctx.Final();
        ulong incrValue = BitConverter.ToUInt64(incrResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("incremental_ABCDEF", [0x41, 0x42, 0x43, 0x44, 0x45, 0x46], incrValue));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateFletcher32Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        void AddVector(string name, byte[] data)
        {
            var ctx = new Fletcher32Context();
            ctx.Update(data);
            byte[] result = ctx.Final();
            uint value = BitConverter.ToUInt32(result.Reverse().ToArray(), 0);
            vectors.Add(new ChecksumTestVectorHex(name, data, value));
        }

        var ctxEmpty = new Fletcher32Context();
        byte[] emptyResult = ctxEmpty.Final();
        uint emptyValue = BitConverter.ToUInt32(emptyResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("empty", [], emptyValue));

        AddVector("single_word_1", [0x00, 0x01]);
        AddVector("two_words_1_2", [0x00, 0x01, 0x00, 0x02]);

        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        AddVector("pattern_0xff", fullPattern);

        var zeros1k = new byte[1024];
        AddVector("zeros_1kb", zeros1k);

        var ff1k = new byte[1024];
        Array.Fill(ff1k, (byte)0xFF);
        AddVector("ff_1kb", ff1k);

        AddVector("string_ABCD", [0x41, 0x42, 0x43, 0x44]);

        var ctx = new Fletcher32Context();
        ctx.Update([0x41, 0x42]);
        ctx.Update([0x43, 0x44]);
        byte[] incrResult = ctx.Final();
        uint incrValue = BitConverter.ToUInt32(incrResult.Reverse().ToArray(), 0);
        vectors.Add(new ChecksumTestVectorHex("incremental_AB_CD", [0x41, 0x42, 0x43, 0x44], incrValue));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateMd5Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        // Empty buffer
        vectors.Add(new ChecksumTestVectorString("empty", [], "d41d8cd98f00b204e9800998ecf8427e"));

        // Single byte - 'a'
        vectors.Add(new ChecksumTestVectorString("single_byte_a", [0x61], "0cc175b9c0f1b6a831c399e269772661"));

        // "abc"
        vectors.Add(new ChecksumTestVectorString("string_abc", [0x61, 0x62, 0x63], "900150983cd24fb0d6963f7d28e17f72"));

        // "hello"
        vectors.Add(new ChecksumTestVectorString("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F], "5d41402abc4b2a76b9719d911017c592"));

        // 256 bytes of pattern 0x00-0xFF
        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var md5Ctx = new Md5Context();
        md5Ctx.Update(fullPattern);
        vectors.Add(new ChecksumTestVectorString("pattern_0xff", fullPattern, md5Ctx.End()));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateSha1Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        // Empty buffer
        vectors.Add(new ChecksumTestVectorString("empty", [], "da39a3ee5e6b4b0d3255bfef95601890afd80709"));

        // Single byte - 'a' (corrected: was cd, should be dd)
        vectors.Add(new ChecksumTestVectorString("single_byte_a", [0x61], "86f7e437faa5a7fce15d1ddcb9eaeaea377667b8"));

        // "abc"
        vectors.Add(new ChecksumTestVectorString("string_abc", [0x61, 0x62, 0x63], "a9993e364706816aba3e25717850c26c9cd0d89d"));

        // "hello"
        vectors.Add(new ChecksumTestVectorString("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F], "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d"));

        // 256 bytes of pattern 0x00-0xFF - compute dynamically
        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var ctx = new Sha1Context();
        ctx.Update(fullPattern);
        vectors.Add(new ChecksumTestVectorString("pattern_0xff", fullPattern, ctx.End()));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateSha256Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        // Empty buffer - compute dynamically
        var ctxEmpty = new Sha256Context();
        vectors.Add(new ChecksumTestVectorString("empty", [], ctxEmpty.End()));

        // Single byte - 'a' - compute dynamically
        var ctx1 = new Sha256Context();
        ctx1.Update([0x61]);
        vectors.Add(new ChecksumTestVectorString("single_byte_a", [0x61], ctx1.End()));

        // "abc" - compute dynamically
        var ctx2 = new Sha256Context();
        ctx2.Update([0x61, 0x62, 0x63]);
        vectors.Add(new ChecksumTestVectorString("string_abc", [0x61, 0x62, 0x63], ctx2.End()));

        // "hello" - compute dynamically
        var ctx3 = new Sha256Context();
        ctx3.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F]);
        vectors.Add(new ChecksumTestVectorString("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F], ctx3.End()));

        // 256 bytes of pattern 0x00-0xFF - compute dynamically
        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var ctx = new Sha256Context();
        ctx.Update(fullPattern);
        vectors.Add(new ChecksumTestVectorString("pattern_0xff", fullPattern, ctx.End()));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateSha384Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        // Empty buffer - compute dynamically
        var ctxEmpty = new Sha384Context();
        vectors.Add(new ChecksumTestVectorString("empty", [], ctxEmpty.End()));

        // Single byte - 'a' - compute dynamically
        var ctx1 = new Sha384Context();
        ctx1.Update([0x61]);
        vectors.Add(new ChecksumTestVectorString("single_byte_a", [0x61], ctx1.End()));

        // "abc" - compute dynamically
        var ctx2 = new Sha384Context();
        ctx2.Update([0x61, 0x62, 0x63]);
        vectors.Add(new ChecksumTestVectorString("string_abc", [0x61, 0x62, 0x63], ctx2.End()));

        // "hello" - compute dynamically
        var ctx3 = new Sha384Context();
        ctx3.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F]);
        vectors.Add(new ChecksumTestVectorString("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F], ctx3.End()));

        // 256 bytes of pattern 0x00-0xFF - compute dynamically
        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var ctx4 = new Sha384Context();
        ctx4.Update(fullPattern);
        vectors.Add(new ChecksumTestVectorString("pattern_0xff", fullPattern, ctx4.End()));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateSha512Vectors()
    {
        var vectors = new List<ChecksumTestVector>();

        // Empty buffer
        vectors.Add(new ChecksumTestVectorString("empty", [], "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e"));

        // Single byte - 'a' - compute dynamically
        var ctx1 = new Sha512Context();
        ctx1.Update([0x61]);
        vectors.Add(new ChecksumTestVectorString("single_byte_a", [0x61], ctx1.End()));

        // "abc" - compute dynamically
        var ctx2 = new Sha512Context();
        ctx2.Update([0x61, 0x62, 0x63]);
        vectors.Add(new ChecksumTestVectorString("string_abc", [0x61, 0x62, 0x63], ctx2.End()));

        // "hello" - compute dynamically
        var ctx3 = new Sha512Context();
        ctx3.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F]);
        vectors.Add(new ChecksumTestVectorString("string_hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F], ctx3.End()));

        // 256 bytes of pattern 0x00-0xFF - compute dynamically
        var fullPattern = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var ctx4 = new Sha512Context();
        ctx4.Update(fullPattern);
        vectors.Add(new ChecksumTestVectorString("pattern_0xff", fullPattern, ctx4.End()));

        return vectors;
    }

    private static List<ChecksumTestVector> GenerateSpamSumVectors()
    {
        var vectors = new List<ChecksumTestVector>();

        // Empty buffer
        vectors.Add(new ChecksumTestVectorString("empty", [], "3::"));

        // Single byte - 'a' - compute dynamically
        var ctx1 = new SpamSumContext();
        ctx1.Update([0x61]);
        vectors.Add(new ChecksumTestVectorString("single_byte_a", [0x61], ctx1.End()));

        // "abc" - compute dynamically
        var ctx2 = new SpamSumContext();
        ctx2.Update([0x61, 0x62, 0x63]);
        vectors.Add(new ChecksumTestVectorString("string_abc", [0x61, 0x62, 0x63], ctx2.End()));

        // "hello world" - compute dynamically
        var ctx3 = new SpamSumContext();
        ctx3.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F, 0x72, 0x6C, 0x64]);
        vectors.Add(new ChecksumTestVectorString("string_hello_world", [0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F, 0x72, 0x6C, 0x64], ctx3.End()));

        // Incremental "hello" variants for debugging
        var ctxHello = new SpamSumContext();
        ctxHello.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F]);
        vectors.Add(new ChecksumTestVectorString("hello", [0x68, 0x65, 0x6C, 0x6C, 0x6F], ctxHello.End()));

        var ctxHelloSpace = new SpamSumContext();
        ctxHelloSpace.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20]);
        vectors.Add(new ChecksumTestVectorString("hello_space", [0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20], ctxHelloSpace.End()));

        var ctxHelloW = new SpamSumContext();
        ctxHelloW.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77]);
        vectors.Add(new ChecksumTestVectorString("hello_w", [0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77], ctxHelloW.End()));

        var ctxHelloWo = new SpamSumContext();
        ctxHelloWo.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F]);
        vectors.Add(new ChecksumTestVectorString("hello_wo", [0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F], ctxHelloWo.End()));

        var ctxHelloWor = new SpamSumContext();
        ctxHelloWor.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F, 0x72]);
        vectors.Add(new ChecksumTestVectorString("hello_wor", [0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F, 0x72], ctxHelloWor.End()));

        var ctxHelloWorl = new SpamSumContext();
        ctxHelloWorl.Update([0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F, 0x72, 0x6C]);
        vectors.Add(new ChecksumTestVectorString("hello_worl", [0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x77, 0x6F, 0x72, 0x6C], ctxHelloWorl.End()));

        // Larger text - repeated pattern (compute dynamically)
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++) sb.Append("test data ");
        var repeated = Encoding.ASCII.GetBytes(sb.ToString());
        var ctx4 = new SpamSumContext();
        ctx4.Update(repeated);
        vectors.Add(new ChecksumTestVectorString("repeated_test_data", repeated, ctx4.End()));

        // Binary pattern - 512 bytes
        var binaryPattern = Enumerable.Range(0, 512).Select(i => (byte)(i % 256)).ToArray();
        var ctx5 = new SpamSumContext();
        ctx5.Update(binaryPattern);
        vectors.Add(new ChecksumTestVectorString("binary_pattern_512", binaryPattern, ctx5.End()));

        return vectors;
    }
}

public class ChecksumTestVector
{
    public string Name { get; }
    public string Input { get; }
    public string Expected { get; }

    [JsonConstructor]
    public ChecksumTestVector(string name, string input, string expected)
    {
        Name = name;
        Input = input;
        Expected = expected;
    }
}

public class ChecksumTestVectorHex : ChecksumTestVector
{
    public ChecksumTestVectorHex(string name, byte[] input, ulong expected) 
        : base(name, BitConverter.ToString(input).Replace("-", "").ToLowerInvariant(), expected.ToString("x16"))
    {
    }

    public ChecksumTestVectorHex(string name, byte[] input, uint expected) 
        : base(name, BitConverter.ToString(input).Replace("-", "").ToLowerInvariant(), expected.ToString("x8"))
    {
    }

    public ChecksumTestVectorHex(string name, byte[] input, ushort expected) 
        : base(name, BitConverter.ToString(input).Replace("-", "").ToLowerInvariant(), expected.ToString("x4"))
    {
    }
}

public class ChecksumTestVectorString : ChecksumTestVector
{
    public ChecksumTestVectorString(string name, byte[] input, string expected) 
        : base(name, BitConverter.ToString(input).Replace("-", "").ToLowerInvariant(), expected)
    {
    }
}
