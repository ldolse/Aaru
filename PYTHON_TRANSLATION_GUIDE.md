# Python Translation Guide for Aaru Optical Media Conversion

**Purpose:** Enable Python implementation of Aaru's optical media conversion for MAME dump verification pipeline:
```
ObscureFormat → Python → CDRDAO (.toc/.bin) → chdman → CHD
```

**Target Use Case:** Map MAME dumps against Redump data, handle PlayStation DRM/subchannel data

---

## Critical Files to Translate

### 1. Core Interfaces (`Aaru.CommonTypes/Interfaces/`)

| File | Purpose | Priority |
|------|---------|----------|
| `IOpticalMediaImage.cs` | Optical disc image interface | **Critical** |
| `IWritableOpticalImage.cs` | Writable optical image interface | **Critical** |
| `IMediaImage.cs` | Base media image interface | High |
| `IFilter.cs` | Input filter interface | Medium |
| `IPluginRegister.cs` | Plugin registration | Low |

### 2. Image Format Readers (`Aaru.Images/`)

| Format | Folder | Priority | Notes |
|--------|--------|----------|-------|
| CloneCD | `CloneCD/` | **Critical** | PlayStation DRM, subchannel support |
| CDRDAO | `CDRDAO/` | **Critical** | chdman compatibility target |
| Alcohol120% | `Alcohol120/` | High | MDF read-only |
| CDRWin | `CDRWin/` | Medium | CUE/BIN support |
| GDI | `GDI/` | Medium | Dreamcast prototypes |
| Nero | `Nero/` | Low | Common legacy format |
| Redumper | `Redumper/` | Low | Modern dump format |

### 3. Core Conversion Logic (`Aaru.Core/Image/Convert/`)

| File | Purpose | Priority |
|------|---------|----------|
| `Optical.cs` | Main optical conversion logic | **Critical** |
| `Sectors.cs` | Sector handling | High |
| `Tags.cs` | Metadata tag handling | Medium |
| `Metadata.cs` | Metadata processing | Medium |

### 4. Subchannel Handling (`Aaru.Core/Media/`)

| File | Purpose | Priority |
|------|---------|----------|
| `CompactDisc.cs` | CD subchannel generation | **Critical** |
| `MmcSubchannel.cs` | MMC subchannel enum | High |

### 5. Decoders (`Aaru.Decoders/`)

| File | Purpose | Priority |
|------|---------|----------|
| `CD/` | CD sector structures | **Critical** |
| `DVD/` | DVD sector structures | Medium |

---

## Key Concepts to Translate

### Track Type Mapping

Aaru uses `TrackType` enum to represent optical disc track types:

```csharp
enum TrackType : byte
{
    Audio,           // CD-DA audio track
    Data,            // Mode 1 data track
    DataMode2,       // Mode 2 data track
    CDI,             // CD-i track
    Xbox,            // Xbox media track
    HFS,             // Mac HFS track
    HFSX,            // Mac HFS+ track
    // ... etc
}
```

**Python Translation:** Map to string constants or enum.IntEnum

### Subchannel Data Handling

CloneCD `.sub` files contain 96 bytes per sector:
- Bytes 0-11: P channel (96 bits = 12 bytes)
- Bytes 12-23: Q channel (96 bits = 12 bytes)
- Bytes 24-35: R channel (96 bits = 12 bytes)
- Bytes 36-47: S channel (96 bits = 12 bytes)
- Bytes 48-59: T channel (96 bits = 12 bytes)
- Bytes 60-71: U channel (96 bits = 12 bytes)
- Bytes 72-83: V channel (96 bits = 12 bytes)
- Bytes 84-95: W channel (96 bits = 12 bytes)

**Critical for PlayStation LSD-style DRM**

### Sector Header Formats

#### Mode 1 Sector (2352 bytes)
```
Sync (12) | Header (4) | Data (2048) | EDC (4) | Reserved (8) | ECC (276)
```

#### Mode 2 Form 1 Sector (2352 bytes)
```
Sync (12) | Header (4) | Subheader (8) | Data (2048) | EDC (4) | Reserved (8) | ECC (276)
```

#### Mode 2 Form 2 Sector (2352 bytes)
```
Sync (12) | Header (4) | Subheader (8) | Data (2324) | EDC (4)
```

### CDRDAO TOC Format

Example `.toc` file structure:
```
CD_ROM_XA
// Track 1 (data)
TRACK MODE1
// Index 0 (pregap)
INDEX 00:00:00
// File reference
FILE "track01.bin" 0 2352
```

**Key requirements for chdman compatibility:**
- Must be 1:1 sector-accurate
- Proper INDEX timing (MM:SS:FF format, 75 frames/second)
- Correct track mode specification

---

## Python Implementation Strategy

### Phase 1: Core Interfaces
1. Define `IOpticalMediaImage` interface
2. Implement base `MediaImage` class
3. Create track/sector data structures

### Phase 2: Format Readers
1. CloneCD reader (`.ccd`, `.img`, `.sub`)
2. CDRDAO writer (`.toc`, `.bin`)
3. Alcohol120% reader (`.mdf`, `.mds`)

### Phase 3: Conversion Logic
1. Track type mapping
2. Sector data extraction
3. Subchannel data handling
4. Multi-session support

### Phase 4: chdman Integration
1. Generate CDRDAO-compatible output
2. Invoke `chdman createdi` externally
3. Verify CHD output

---

## Critical Code Paths

### 1. CloneCD Reading
```
CloneCD.Read() → Identify format
              → Read .ccd descriptor
              → Open .img data file
              → Open .sub subchannel file (if exists)
              → Return IOpticalMediaImage
```

### 2. CDRDAO Writing
```
CDRDAO.Write() → Create .toc file
              → Write track layout
              → Write sector data to .bin
              → Embed subchannel data (if present)
```

### 3. Optical Conversion
```
ConvertImage() → Open input format
              → Create output format
              → For each track:
                 - Read sectors
                 - Process subchannel data
                 - Write sectors
              → Close input/output
```

---

## External Dependencies

### chdman (Required)
- **Tool:** MAME CHD Manager
- **Command:** `chdman createdi -i input.toc -o output.chd`
- **Alternative:** `chdman createcd -i input.toc -o output.chd`

### Python Libraries (Recommended)
- `construct` - Binary structure parsing
- `enum34` - Enum support (Python 3.4+)
- `typing` - Type hints

---

## Testing Strategy

### Test Images
Priority formats for testing:
1. CloneCD (`.ccd` + `.img` + `.sub`)
2. CDRDAO (`.toc` + `.bin`)
3. Alcohol120% (`.mdf` + `.mds`)

### Validation
1. Compare output with Aaru C# reference
2. Verify CHD creation with `chdman verify`
3. Test PlayStation DRM subchannel preservation

---

## Known Limitations

### CHD Write Support
- Aaru CHD implementation is **READ-ONLY**
- Must use external `chdman` tool
- Workflow: `AnyFormat → CDRDAO → chdman → CHD`

### Subchannel Data
- Critical for PlayStation LSD-style DRM
- CloneCD `.sub` files: 96 bytes per sector
- May need CRC fixing for malformed dumps

### DVD Decryption
- CSS/CPRM decryption requires SCSI commands
- Not implemented in this Python translation
- Use pre-decrypted dumps for DVD content

---

## Reference Implementation Notes

### Aaru Pruned Version
- **Branch:** `feature/optical-media-pruning`
- **Commits:** Phases 2-7 completed
- **Size:** ~300K lines removed from original

### Key Simplifications
1. Removed all floppy/tape/flux formats
2. Removed device dumping commands
3. Removed GUI/TUI interfaces
4. Removed Mac-specific filters
5. Kept only essential CLI commands (convert/info/verify/merge)

---

*Created: 2026-04-06*
*Based on Aaru pruning branch: `feature/optical-media-pruning`*