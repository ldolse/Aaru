using Aaru.Checksums;
using System.Text.Json;

// Quick test
var tests = new[] {
    ("empty", Array.Empty<byte>()),
    ("a", new byte[] { 0x61 }),
    ("abc", new byte[] { 0x61, 0x62, 0x63 }),
    ("hello", new byte[] { 0x68, 0x65, 0x6c, 0x6c, 0x6f }),
};

foreach (var (name, data) in tests)
{
    var ctx = new SpamSumContext();
    ctx.Update(data);
    Console.WriteLine($"{name}: {ctx.End()}");
}
