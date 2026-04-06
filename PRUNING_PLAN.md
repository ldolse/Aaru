# Aaru Optical Media Pruning Plan

**Branch:** `feature/optical-media-pruning`  
**Baseline:** Full Aaru repository as of 2026-04-05  
**Goal:** Create minimal Aaru subset for optical media format conversion to CDRDAO → CHD workflow

---

## Purpose

Enable Python translation of Aaru's optical media conversion code to support MAME dump verification pipeline:
```
ObscureFormat → Aaru → CDRDAO (.toc/.bin) → chdman → CHD
```

---

## User Requirements (Confirmed)

1. ✅ **Keep binary analysis** - Filesystems and Partitions projects needed for unique MAME dump identification
2. ✅ **MDF read-only** - Alcohol120% write support not needed (simplifies implementation)
3. ✅ **Keep DVD decryption library** - Retain `Aaru.Decryption` for now (may need CSS/CPRM for PlayStation)
4. ✅ **Prune Archives project** - AMG, Arc, Ha, Stfs, Symbian, Zoo not needed
5. ✅ **Audit Filters** - Some filters may be needed for data transformation; verify before removal

---

## Target Formats

### Critical (Must Keep)
| Format | Folder | Read | Write | Notes |
|--------|--------|------|-------|-------|
| CDRDAO | `Aaru.Images/CDRDAO/` | ✅ | ✅ | **Primary target** - chdman compatibility |
| CloneCD | `Aaru.Images/CloneCD/` | ✅ | ✅ | PlayStation DRM, subchannel data |

### High Priority
| Format | Folder | Read | Write | Notes |
|--------|--------|------|-------|-------|
| Alcohol120% | `Aaru.Images/Alcohol120/` | ✅ | ❌ | MDF read-only sufficient |
| AaruFormat | `Aaru.Images/AaruFormat/` | ✅ | ✅ | May become standard format |

### Keep (Read Support)
| Format | Folder | Read | Write | Notes |
|--------|--------|------|-------|-------|
| GDI | `Aaru.Images/GDI/` | ✅ | ❌ | Dreamcast prototypes |
| CDRWin | `Aaru.Images/CDRWin/` | ✅ | ✅ | CUE/BIN support |
| Redumper | `Aaru.Images/Redumper/` | ✅ | ❌ | Modern dump format |
| Nero | `Aaru.Images/Nero/` | ✅ | ❌ | Common legacy format |
| BlindWrite | `Aaru.Images/BlindWrite/` | ✅ | ❌ | B5/B6/B7 formats |
| PowerISO | `Aaru.Images/PowerISO/` | ✅ | ❌ | DAA format |
| UltraISO | `Aaru.Images/UltraISO/` | ✅ | ❌ | UIF format |
| DiscJuggler | `Aaru.Images/DiscJuggler/` | ✅ | ❌ | CDI format |
| TallyDumps | `Aaru.Images/TallyDumps/` | ✅ | ❌ | Tape (verify if optical exists) |
| TOSEC | `Aaru.Images/TOSEC/` | ✅ | ❌ | CUE/CHD metadata |

### Remove (Non-Optical)
- **Floppy:** AppleNIB, CopyQM, CPCDSK, DART, DIM, DiskDupe, HDCopy, IMD, TeleDisk, ADL, ODI, Lime, CopyIIGS, DiskManager, CopyIIP, FD, XDF, Floptical, Appledos, DiskCopy42, NDIF, UDIF
- **Tape:** CopyTape, TAR
- **Flux:** HxCStream, KryoFlux, SuperCardPro, FluxEngine, Greaseweazle
- **VM Disks:** Parallels, QCOW, QED, VDI, VHD, VHDX, VMware, Virtual98
- **Other:** CDi, DragonDOS, DOS, Mac, OS2, V2500, XVD

---

## Projects to Remove

### Safe to Remove Immediately
| Project | Reason | Dependencies |
|---------|--------|--------------|
| `Aaru.Gui` | GUI not needed | None depend on it |
| `Aaru.Tui` | Terminal UI not needed | None depend on it |
| `Aaru.Tests` | Unit tests not needed | None depend on it |
| `Aaru.Tests.Devices` | Device tests not needed | None depend on it |
| `Aaru.Archives` | Archive formats out of scope | Referenced by Core |

### Remove with Code Changes
| Project | Reason | Files to Modify |
|---------|--------|-----------------|
| `Aaru.Devices` | Hardware-only (ioctl, DeviceIoControl) | Core, Decryption, Aaru CLI, Statistics.cs |

### Projects to Keep
| Project | Purpose |
|---------|---------|
| `Aaru.CommonTypes` | Core interfaces (IMediaImage, IOpticalMediaImage, etc.) |
| `Aaru.Core` | Conversion logic, filesystem operations |
| `Aaru.Images` | Image format implementations (pruned) |
| `Aaru.Filesystems` | Binary analysis support |
| `Aaru.Partitions` | Partition scheme reading |
| `Aaru.Decryption` | DVD decryption (CSS, CPRM, etc.) |
| `Aaru.Filters` | Data transformation (audit needed) |
| `Aaru.Checksums` | Hashing, error correction |
| `Aaru.Compression` | Compression algorithms |
| `Aaru.Decoders` | CD/DVD sector structures |
| `Aaru.Dto` | Data transfer objects |
| `Aaru.Database` | Metadata storage |
| `Aaru.EntityFramework` | EF integration |
| `Aaru.Settings` | Configuration |
| `Aaru.Helpers` | Utility functions |
| `Aaru.Logging` | Logging infrastructure |
| `Aaru.Console` | Console abstraction |
| `Aaru.Localization` | Localized strings |
| `Aaru.Generators` | Source generators |
| `Aaru` | Main CLI application |

---

## Phase 2: Remove Safe Projects

**Goal:** Remove projects with no dependencies or safe external references

### 2.1 Remove from Solution
Edit `Aaru.slnx` to remove:
```
Aaru.Gui
Aaru.Tui
Aaru.Tests
Aaru.Tests.Devices
Aaru.Archives
```

### 2.2 Remove Core Reference to Archives
File: `Aaru.Core/Aaru.Core.csproj`
- Remove: `<ProjectReference Include="..\Aaru.Archives\Aaru.Archives.csproj" />`

File: `Aaru.Core/Archive/` - Delete entire directory if exists

### 2.3 Delete Project Folders
```bash
rm -rf Aaru.Gui/
rm -rf Aaru.Tui/
rm -rf Aaru.Tests/
rm -rf Aaru.Tests.Devices/
rm -rf Aaru.Archives/
```

---

## Phase 3: Remove Devices Project

**Goal:** Remove hardware-only device communication code

### 3.1 Remove Unused Imports
Files with `using Aaru.Devices;` to check:
- `Aaru.Core/Image/Convert/Optical.cs` - Remove unused import
- `Aaru.Core/Image/Merge/Optical.cs` - Remove unused import
- `Aaru.Core/Statistics.cs` - Stub device stats methods
- `Aaru.Decryption/DVD/Dump.cs` - Verify if hardware decryption used
- `Aaru/Commands/Device/*.cs` - Will be deleted in Phase 5

### 3.2 Remove Project References
Edit `.csproj` files:
- `Aaru.Core/Aaru.Core.csproj` - Remove Devices reference
- `Aaru.Decryption/Aaru.Decryption.csproj` - Remove Devices reference (if present)
- `Aaru/Aaru.csproj` - Remove Devices reference

### 3.3 Stub Statistics.cs
Method: `Statistics.AddDevice()` - Stub to no-op or remove

### 3.4 Delete Devices Project
```bash
rm -rf Aaru.Devices/
```

---

## Phase 4: Prune Image Format Folders

**Goal:** Keep only optical media format implementations

### 4.1 Update Plugin Register
File: `Aaru.Images/Register.cs`
- Remove registration calls for non-optical formats
- Keep optical format registrations only

### 4.2 Delete Non-Optical Format Folders
From `Aaru.Images/`, delete:
```
AppleNIB/
CopyQM/
CPCDSK/
DART/
DIM/
DiskDupe/
HDCopy/
IMD/
TeleDisk/
ADL/
ODI/
Lime/
CopyIIGS/
DiskManager/
CopyIIP/
FD/
XDF/
Floptical/
Appledos/
DiskCopy42/
NDIF/
UDIF/
CopyTape/
TAR/
HxCStream/
KryoFlux/
SuperCardPro/
FluxEngine/
Greaseweazle/
Parallels/
QCOW/
QED/
VDI/
VHD/
VHDX/
VMware/
Virtual98/
CDi/
DragonDOS/
DOS/
Mac/
OS2/
V2500/
XVD/
```

### 4.3 Update Project File
File: `Aaru.Images/Aaru.Images.csproj`
- Remove `<Compile>` entries for deleted folders
- Remove unused `<EmbeddedResource>` entries

---

## Phase 5: Strip CLI Commands

**Goal:** Remove commands not needed for conversion workflow

### 5.1 Commands to Keep
- `Commands/Image/Convert.cs` - **KEEP** (primary conversion)
- `Commands/Image/Info.cs` - **KEEP** (format inspection)
- `Commands/Image/Verify.cs` - **KEEP** (integrity checking)
- `Commands/Image/Merge.cs` - **KEEP** (multi-track handling)
- `Commands/Formats.cs` - **KEEP** (list supported formats)
- `Commands/Configure.cs` - **KEEP** (settings management)
- `Commands/Filesystem/` - **KEEP** (binary analysis)
- `Commands/Partition/` - **KEEP** (partition inspection)

### 5.2 Commands to Remove
Delete entire directories:
```
Commands/Archive/
Commands/Database/
Commands/Device/
Commands/Media/
Commands/Checksum/
```

### 5.3 Update Main CLI
File: `Aaru/Program.cs` or `Aaru/RootCommand.cs`
- Remove command registrations for deleted commands

---

## Phase 6: Audit Filters Project

**Goal:** Identify and keep only necessary data filters

### 6.1 Review Filter Usage
Check `Aaru.Core/Image/Convert/Optical.cs` for filter usage:
- Which filters are actually invoked during conversion?
- Are any filters required for subchannel handling?
- Are any filters required for sector header manipulation?

### 6.2 Common Filters to Evaluate
| Filter | Likely Needed? | Reason |
|--------|----------------|--------|
| Base64 | ❌ | Encoding transformation |
| Base85 | ❌ | Encoding transformation |
| Base91 | ❌ | Encoding transformation |
| XXEncode | ❌ | Encoding transformation |
| UUEncode | ❌ | Encoding transformation |
| YEnc | ❌ | Encoding transformation |
| Bzip2 | ❌ | Compression |
| Deflate | ❌ | Compression |
| GZip | ❌ | Compression |
| LZMA | ❌ | Compression |
| Brotli | ❌ | Compression |
| Flate | ❌ | Compression |
| Raw | ✅ | Passthrough (default) |
| XOR | ⚠️ | Verify if used for DRM |
| Scramble | ⚠️ | DVD sector descrambling |
| Sector | ⚠️ | Sector manipulation |

### 6.3 Prune Unused Filters
Delete unused filter classes from `Aaru.Filters/`
Update `Aaru.Filters/Register.cs`

---

## Phase 7: Cleanup and Build Verification

**Goal:** Ensure clean build after all pruning

### 7.1 Remove Unused Using Statements
Run through all remaining files:
- Remove `using` statements for removed namespaces
- Fix any compilation errors

### 7.2 Remove Dead Code
- Unused methods that referenced removed functionality
- Empty namespaces
- Unused resource files

### 7.3 Build Verification
```bash
dotnet build -c Release
```

Expected output: Clean build with no errors or warnings

### 7.4 Test Conversion (if test images available)
```bash
dotnet run -- image convert -i input.ccd -o output.toc -f cdrdao
```

---

## Phase 8: Documentation for Python Translation

**Goal:** Document key code paths for Python implementation

### 8.1 Critical Files to Translate
1. `Aaru.Core/Image/Convert/Optical.cs` - Main conversion logic
2. `Aaru.Images/CDRDAO/` - CDRDAO writer implementation
3. `Aaru.Images/CloneCD/` - CloneCD reader implementation
4. `Aaru.Images/Alcohol120/` - MDF reader implementation
5. `Aaru.Core/Subchannel/*.cs` - Subchannel handling (if exists)
6. `Aaru.CommonTypes/Interfaces/IOpticalMediaImage.cs` - Interface definition

### 8.2 Key Concepts to Document
- Track type mapping (audio, data, mode2, etc.)
- Subchannel P-W handling
- Sector header formats (Mode 1, Mode 2, etc.)
- TOC/INDEX parsing and generation
- Multi-session handling
- Error correction code handling

---

## Known Technical Constraints

### CHD Write Support
- Aaru's CHD implementation is **READ-ONLY**
- Must use external `chdman` tool for CHD creation
- Workflow: `AnyFormat → CDRDAO → chdman → CHD`

### CDRDAO Format Requirements
- `.toc` file: Track layout metadata
- `.bin` file: Raw sector data
- Subchannel data stored in separate `.sub` or embedded
- Must be 1:1 sector-accurate for chdman compatibility

### Subchannel Data
- Critical for PlayStation LSD-style DRM
- Aaru supports: P, Q, R, S, T, U, V, W channels
- CloneCD `.sub` files contain 96 bytes per sector
- May need CRC fixing for malformed dumps

---

## Files Modified During Pruning

### Solution File
- `Aaru.slnx` - Remove project references

### Project Files (.csproj)
- `Aaru.Core/Aaru.Core.csproj` - Remove Archives, Devices
- `Aaru.Images/Aaru.Images.csproj` - Remove deleted format compilations
- `Aaru/Aaru.csproj` - Remove Devices, Archives
- `Aaru.Decryption/Aaru.Decryption.csproj` - Remove Devices (if present)

### Core Logic Files
- `Aaru.Core/Statistics.cs` - Stub device methods
- `Aaru.Core/Image/Convert/Optical.cs` - Remove unused imports
- `Aaru.Core/Image/Merge/Optical.cs` - Remove unused imports

### CLI Files
- `Aaru/Program.cs` or `Aaru/RootCommand.cs` - Remove command registrations
- Delete: `Aaru/Commands/Archive/*`
- Delete: `Aaru/Commands/Database/*`
- Delete: `Aaru/Commands/Device/*`
- Delete: `Aaru/Commands/Media/*`

### Image Format Files
- `Aaru.Images/Register.cs` - Remove non-optical registrations
- Delete all non-optical format folders

---

## Build Commands

```bash
# Build all configurations
dotnet build

# Build Release
dotnet build -c Release

# Publish for current platform
dotnet publish -c Release

# Run CLI
dotnet run -- image convert -i input.ccd -o output.toc -f cdrdao

# List formats
dotnet run -- formats
```

---

## Session Notes

**Session Date:** 2026-04-05  
**Branch:** `feature/optical-media-pruning`

### User Decisions Made:
1. Keep Filesystems/Partitions for binary analysis
2. MDF read-only (no write needed)
3. Keep DVD decryption library for now
4. Archives project approved for removal
5. Filters project needs audit before pruning

### Context Preservation:
This document serves as the primary reference for continuing the pruning work across multiple sessions. All key decisions, file paths, and phase descriptions are captured here.

---

## TODO Tracking

- [x] Phase 1: Create baseline branch
- [ ] Phase 2: Remove safe projects (Gui, Tui, Tests, Archives)
- [ ] Phase 3: Remove Devices project
- [ ] Phase 4: Prune image formats
- [ ] Phase 5: Strip CLI commands
- [ ] Phase 6: Audit Filters
- [ ] Phase 7: Cleanup and verify build
- [ ] Phase 8: Python translation documentation

---

*Last updated: 2026-04-05*