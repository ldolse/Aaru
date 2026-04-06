namespace Aaru.CommonTypes.Enums;

/// <summary>
/// MMC subchannel data types that can be read from optical media
/// </summary>
public enum MmcSubchannel : byte
{
    /// <summary>No subchannel shall be returned</summary>
    None = 0x00,

    /// <summary>The raw P to W subchannel data shall be transferred</summary>
    Raw = 0x01,

    /// <summary>Q data shall be transferred</summary>
    Q16 = 0x02,

    /// <summary>De-interleaved and error-corrected R to W subchannel data shall be transferred</summary>
    Rw = 0x04
}