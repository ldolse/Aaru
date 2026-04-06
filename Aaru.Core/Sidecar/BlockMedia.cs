// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : BlockMedia.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Core algorithms.
//
// --[ Description ] ----------------------------------------------------------
//
//     Contains logic to create sidecar from a block media dump.
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2026 Natalia Portillo
// ****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aaru.CommonTypes;
using Aaru.CommonTypes.AaruMetadata;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Interfaces;
using Aaru.CommonTypes.Structs.Devices.ATA;
using Aaru.Decoders.PCMCIA;
using Aaru.Filters;
using Aaru.Helpers;
using Aaru.Images;
using Aaru.Logging;
using Sentry;
using Directory = System.IO.Directory;
using File = System.IO.File;
using MediaType = Aaru.CommonTypes.Metadata.MediaType;
using Partition = Aaru.CommonTypes.Partition;
using Tuple = Aaru.Decoders.PCMCIA.Tuple;

namespace Aaru.Core;

public sealed partial class Sidecar
{
    /// <summary>Creates a metadata sidecar for a block media (e.g. floppy, hard disk, flash card, usb stick)</summary>
    /// <param name="image">Image</param>
    /// <param name="filterId">Filter uuid</param>
    /// <param name="imagePath">Image path</param>
    /// <param name="fi">Image file information</param>
    /// <param name="plugins">Image plugins</param>
    /// <param name="imgChecksums">List of image checksums</param>
    /// <param name="sidecar">Metadata sidecar</param>
    /// <param name="encoding">Encoding to be used for filesystem plugins</param>
    void BlockMedia(IMediaImage image, Guid filterId, string imagePath, FileInfo fi, PluginRegister plugins,
                    List<CommonTypes.AaruMetadata.Checksum> imgChecksums, ref Metadata sidecar, Encoding encoding)
    {
        if(_aborted) return;

        sidecar.BlockMedias =
        [
            new BlockMedia
            {
                Checksums = imgChecksums,
                Image = new CommonTypes.AaruMetadata.Image
                {
                    Format = image.Format,
                    Offset = 0,
                    Value  = Path.GetFileName(imagePath)
                },
                Size = (ulong)fi.Length,
                Sequence = new Sequence
                {
                    Title = image.Info.MediaTitle
                }
            }
        ];

        if(image.Info.MediaSequence != 0 && image.Info.LastMediaSequence != 0)
        {
            sidecar.BlockMedias[0].Sequence.MediaSequence = (uint)image.Info.MediaSequence;
            sidecar.BlockMedias[0].Sequence.TotalMedia    = (uint)image.Info.LastMediaSequence;
        }
        else
        {
            sidecar.BlockMedias[0].Sequence.MediaSequence = 1;
            sidecar.BlockMedias[0].Sequence.TotalMedia    = 1;
        }

        UpdateStatus(Localization.Core.Hashing_media_tags);
        ErrorNumber errno;
        byte[]      buffer;

        foreach(MediaTagType tagType in image.Info.ReadableMediaTags)
        {
            if(_aborted) return;

            switch(tagType)
            {
                case MediaTagType.ATAPI_IDENTIFY:
                    errno = image.ReadMediaTag(MediaTagType.ATAPI_IDENTIFY, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].ATA = new ATA
                    {
                        Identify = new Dump
                        {
                            Checksums = Checksum.GetChecksums(buffer),
                            Size      = (ulong)buffer.Length
                        }
                    };

                    break;
                case MediaTagType.ATA_IDENTIFY:
                    errno = image.ReadMediaTag(MediaTagType.ATA_IDENTIFY, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].ATA = new ATA
                    {
                        Identify = new Dump
                        {
                            Checksums = Checksum.GetChecksums(buffer),
                            Size      = (ulong)buffer.Length
                        }
                    };

                    break;
                case MediaTagType.PCMCIA_CIS:
                    errno = image.ReadMediaTag(MediaTagType.PCMCIA_CIS, out byte[] cis);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].Pcmcia = new Pcmcia
                    {
                        Cis = new Dump
                        {
                            Checksums = Checksum.GetChecksums(cis),
                            Size      = (ulong)cis.Length
                        }
                    };

                    Tuple[] tuples = CIS.GetTuples(cis);

                    if(tuples != null)
                    {
                        foreach(Tuple tuple in tuples)
                        {
                            switch(tuple.Code)
                            {
                                case TupleCodes.CISTPL_MANFID:
                                    ManufacturerIdentificationTuple manfid =
                                        CIS.DecodeManufacturerIdentificationTuple(tuple);

                                    if(manfid != null)
                                    {
                                        sidecar.BlockMedias[0].Pcmcia.ManufacturerCode = manfid.ManufacturerID;

                                        sidecar.BlockMedias[0].Pcmcia.CardCode = manfid.CardID;
                                    }

                                    break;
                                case TupleCodes.CISTPL_VERS_1:
                                    Level1VersionTuple vers = CIS.DecodeLevel1VersionTuple(tuple);

                                    if(vers != null)
                                    {
                                        sidecar.BlockMedias[0].Pcmcia.Manufacturer = vers.Manufacturer;
                                        sidecar.BlockMedias[0].Pcmcia.ProductName  = vers.Product;

                                        sidecar.BlockMedias[0].Pcmcia.Compliance =
                                            $"{vers.MajorVersion}.{vers.MinorVersion}";

                                        sidecar.BlockMedias[0].Pcmcia.AdditionalInformation =
                                        [
                                            ..vers.AdditionalInformation
                                        ];
                                    }

                                    break;
                            }
                        }
                    }

                    break;
                case MediaTagType.SCSI_INQUIRY:
                    errno = image.ReadMediaTag(MediaTagType.SCSI_INQUIRY, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].SCSI = new SCSI
                    {
                        Inquiry = new Dump
                        {
                            Checksums = Checksum.GetChecksums(buffer),
                            Size      = (ulong)buffer.Length
                        }
                    };

                    break;
                case MediaTagType.SD_CID:
                    errno = image.ReadMediaTag(MediaTagType.SD_CID, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].SecureDigital ??= new SecureDigital();

                    sidecar.BlockMedias[0].SecureDigital.CID = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.SD_CSD:
                    errno = image.ReadMediaTag(MediaTagType.SD_CSD, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].SecureDigital ??= new SecureDigital();

                    sidecar.BlockMedias[0].SecureDigital.CSD = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.SD_SCR:
                    errno = image.ReadMediaTag(MediaTagType.SD_SCR, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].SecureDigital ??= new SecureDigital();

                    sidecar.BlockMedias[0].SecureDigital.SCR = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.SD_OCR:
                    errno = image.ReadMediaTag(MediaTagType.SD_OCR, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].SecureDigital ??= new SecureDigital();

                    sidecar.BlockMedias[0].SecureDigital.OCR = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.MMC_CID:
                    errno = image.ReadMediaTag(MediaTagType.MMC_CID, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].MultiMediaCard ??= new MultiMediaCard();

                    sidecar.BlockMedias[0].MultiMediaCard.CID = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.MMC_CSD:
                    errno = image.ReadMediaTag(MediaTagType.MMC_CSD, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].MultiMediaCard ??= new MultiMediaCard();

                    sidecar.BlockMedias[0].MultiMediaCard.CSD = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.MMC_OCR:
                    errno = image.ReadMediaTag(MediaTagType.MMC_OCR, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].MultiMediaCard ??= new MultiMediaCard();

                    sidecar.BlockMedias[0].MultiMediaCard.OCR = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.MMC_ExtendedCSD:
                    errno = image.ReadMediaTag(MediaTagType.MMC_ExtendedCSD, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].MultiMediaCard ??= new MultiMediaCard();

                    sidecar.BlockMedias[0].MultiMediaCard.ExtendedCSD = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.USB_Descriptors:
                    errno = image.ReadMediaTag(MediaTagType.USB_Descriptors, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].Usb ??= new Usb();

                    sidecar.BlockMedias[0].Usb.Descriptors = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.SCSI_MODESENSE_6:
                    errno = image.ReadMediaTag(MediaTagType.SCSI_MODESENSE_6, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].SCSI ??= new SCSI();

                    sidecar.BlockMedias[0].SCSI.ModeSense = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
                case MediaTagType.SCSI_MODESENSE_10:
                    errno = image.ReadMediaTag(MediaTagType.SCSI_MODESENSE_10, out buffer);

                    if(errno != ErrorNumber.NoError) break;

                    sidecar.BlockMedias[0].SCSI ??= new SCSI();

                    sidecar.BlockMedias[0].SCSI.ModeSense10 = new Dump
                    {
                        Checksums = Checksum.GetChecksums(buffer),
                        Size      = (ulong)buffer.Length
                    };

                    break;
            }
        }

        // If there is only one track, and it's the same as the image file (e.g. ".iso" files), don't re-checksum.
        if(image.Id == new Guid("12345678-AAAA-BBBB-CCCC-123456789000") &&
           filterId == new Guid("12345678-AAAA-BBBB-CCCC-123456789000"))
            sidecar.BlockMedias[0].ContentChecksums = sidecar.BlockMedias[0].Checksums;
        else
        {
            UpdateStatus(Localization.Core.Hashing_sectors);

            Checksum contentChkWorker =
                _enableSpamsum ? new Checksum() : new Checksum(EnableChecksum.All & ~EnableChecksum.SpamSum);

            // For fast debugging, skip checksum
            //goto skipImageChecksum;

            const uint sectorsToRead = 64;
            ulong      sectors       = image.Info.Sectors;
            ulong      doneSectors   = 0;

            InitProgress2();

            while(doneSectors < sectors)
            {
                if(_aborted)
                {
                    EndProgress2();

                    return;
                }

                byte[] sector;

                if(sectors - doneSectors >= sectorsToRead)
                {
                    errno = image.ReadSectors(doneSectors, false, sectorsToRead, out sector, out _);

                    if(errno != ErrorNumber.NoError)
                    {
                        UpdateStatus(string.Format(Localization.Core.Error_0_reading_sector_1, errno, doneSectors));
                        EndProgress2();

                        return;
                    }

                    UpdateProgress2(Localization.Core.Hashing_sector_0_of_1, (long)doneSectors, (long)sectors);
                    doneSectors += sectorsToRead;
                }
                else
                {
                    errno = image.ReadSectors(doneSectors, false, (uint)(sectors - doneSectors), out sector, out _);

                    if(errno != ErrorNumber.NoError)
                    {
                        UpdateStatus(string.Format(Localization.Core.Error_0_reading_sector_1, errno, doneSectors));
                        EndProgress2();

                        return;
                    }

                    UpdateProgress2(Localization.Core.Hashing_sector_0_of_1, (long)doneSectors, (long)sectors);
                    doneSectors += sectors - doneSectors;
                }

                contentChkWorker.Update(sector);
            }

            // For fast debugging, skip checksum
            //skipImageChecksum:

            sidecar.BlockMedias[0].ContentChecksums = contentChkWorker.End();

            EndProgress2();
        }

        (string type, string subType) diskType = MediaType.MediaTypeToString(image.Info.MediaType);
        sidecar.BlockMedias[0].MediaType    = diskType.type;
        sidecar.BlockMedias[0].MediaSubType = diskType.subType;
        Statistics.AddMedia(image.Info.MediaType, false);

        sidecar.BlockMedias[0].Dimensions = Dimensions.FromMediaType(image.Info.MediaType);

        sidecar.BlockMedias[0].LogicalBlocks    = image.Info.Sectors;
        sidecar.BlockMedias[0].LogicalBlockSize = image.Info.SectorSize;

        // TODO: Detect it
        sidecar.BlockMedias[0].PhysicalBlockSize = image.Info.SectorSize;

        if(image is ITapeImage { IsTape: true } tapeImage)
        {
            List<TapePartition> tapePartitions = [];

            foreach(CommonTypes.Structs.TapePartition tapePartition in tapeImage.TapePartitions)
            {
                var thisPartition = new TapePartition
                {
                    Image      = sidecar.BlockMedias[0].Image,
                    Sequence   = tapePartition.Number,
                    StartBlock = tapePartition.FirstBlock,
                    EndBlock   = tapePartition.LastBlock
                };

                if(tapeImage.TapePartitions.Count == 1)
                    thisPartition.Checksums = sidecar.BlockMedias[0].ContentChecksums;
                else
                {
                    UpdateStatus(string.Format(Localization.Core.Hashing_partition_0, tapePartition.Number));

                    if(_aborted) return;

                    Checksum tapePartitionChk = _enableSpamsum
                                                    ? new Checksum()
                                                    : new Checksum(EnableChecksum.All & ~EnableChecksum.SpamSum);

                    // For fast debugging, skip checksum
                    //goto skipImageChecksum;

                    const uint sectorsToRead = 64;
                    ulong      sectors       = tapePartition.LastBlock - tapePartition.FirstBlock + 1;
                    ulong      doneSectors   = 0;

                    InitProgress2();

                    while(doneSectors < sectors)
                    {
                        if(_aborted)
                        {
                            EndProgress2();

                            return;
                        }

                        byte[] sector;

                        if(sectors - doneSectors >= sectorsToRead)
                        {
                            errno = image.ReadSectors(tapePartition.FirstBlock + doneSectors,
                                                      false,
                                                      sectorsToRead,
                                                      out sector,
                                                      out _);

                            if(errno != ErrorNumber.NoError)
                            {
                                AaruLogging.Error(string.Format(Localization.Core.Error_0_reading_sector_1,
                                                                errno,
                                                                tapePartition.FirstBlock + doneSectors));

                                EndProgress2();

                                return;
                            }

                            UpdateProgress2(Localization.Core.Hashing_blocks_0_of_1, (long)doneSectors, (long)sectors);
                            doneSectors += sectorsToRead;
                        }
                        else
                        {
                            errno = image.ReadSectors(tapePartition.FirstBlock + doneSectors,
                                                      false,
                                                      (uint)(sectors - doneSectors),
                                                      out sector,
                                                      out _);

                            if(errno != ErrorNumber.NoError)
                            {
                                AaruLogging.Error(string.Format(Localization.Core.Error_0_reading_sector_1,
                                                                errno,
                                                                tapePartition.FirstBlock + doneSectors));

                                EndProgress2();

                                return;
                            }

                            UpdateProgress2(Localization.Core.Hashing_blocks_0_of_1, (long)doneSectors, (long)sectors);
                            doneSectors += sectors - doneSectors;
                        }

                        thisPartition.Size += (ulong)sector.LongLength;

                        tapePartitionChk.Update(sector);
                    }

                    // For fast debugging, skip checksum
                    //skipImageChecksum:

                    thisPartition.Checksums = tapePartitionChk.End();

                    EndProgress2();
                }

                List<TapeFile> filesInPartition = [];

                foreach(CommonTypes.Structs.TapeFile tapeFile in
                        tapeImage.Files.Where(f => f.Partition == tapePartition.Number))
                {
                    var thisFile = new TapeFile
                    {
                        Sequence   = tapeFile.File,
                        StartBlock = tapeFile.FirstBlock,
                        EndBlock   = tapeFile.LastBlock,
                        Image      = sidecar.BlockMedias[0].Image,
                        Size       = 0,
                        BlockSize  = 0
                    };

                    if(tapeImage.Files.Count(f => f.Partition == tapePartition.Number) == 1)
                    {
                        thisFile.Checksums = thisPartition.Checksums;
                        thisFile.Size      = thisPartition.Size;
                    }
                    else
                    {
                        UpdateStatus(string.Format(Localization.Core.Hashing_file_0, tapeFile.File));

                        if(_aborted) return;

                        Checksum tapeFileChk = _enableSpamsum
                                                   ? new Checksum()
                                                   : new Checksum(EnableChecksum.All & ~EnableChecksum.SpamSum);

                        // For fast debugging, skip checksum
                        //goto skipImageChecksum;

                        const uint sectorsToRead = 64;
                        ulong      sectors       = tapeFile.LastBlock - tapeFile.FirstBlock + 1;
                        ulong      doneSectors   = 0;

                        InitProgress2();

                        while(doneSectors < sectors)
                        {
                            if(_aborted)
                            {
                                EndProgress2();

                                return;
                            }

                            byte[] sector;

                            if(sectors - doneSectors >= sectorsToRead)
                            {
                                errno = image.ReadSectors(tapeFile.FirstBlock + doneSectors,
                                                          false,
                                                          sectorsToRead,
                                                          out sector,
                                                          out _);

                                if(errno != ErrorNumber.NoError)
                                {
                                    AaruLogging.Error(string.Format(Localization.Core.Error_0_reading_sector_1,
                                                                    errno,
                                                                    tapeFile.FirstBlock + doneSectors));

                                    EndProgress2();

                                    return;
                                }

                                UpdateProgress2(Localization.Core.Hashing_blocks_0_of_1,
                                                (long)doneSectors,
                                                (long)sectors);

                                doneSectors += sectorsToRead;
                            }
                            else
                            {
                                errno = image.ReadSectors(tapeFile.FirstBlock + doneSectors,
                                                          false,
                                                          (uint)(sectors - doneSectors),
                                                          out sector,
                                                          out _);

                                if(errno != ErrorNumber.NoError)
                                {
                                    AaruLogging.Error(string.Format(Localization.Core.Error_0_reading_sector_1,
                                                                    errno,
                                                                    tapeFile.FirstBlock + doneSectors));

                                    EndProgress2();

                                    return;
                                }

                                UpdateProgress2(Localization.Core.Hashing_blocks_0_of_1,
                                                (long)doneSectors,
                                                (long)sectors);

                                doneSectors += sectors - doneSectors;
                            }

                            if((ulong)sector.LongLength > thisFile.BlockSize)
                                thisFile.BlockSize = (ulong)sector.LongLength;

                            thisFile.Size += (ulong)sector.LongLength;

                            tapeFileChk.Update(sector);
                        }

                        // For fast debugging, skip checksum
                        //skipImageChecksum:

                        thisFile.Checksums = tapeFileChk.End();

                        EndProgress2();
                    }

                    filesInPartition.Add(thisFile);
                }

                thisPartition.Files = filesInPartition;
                tapePartitions.Add(thisPartition);
            }

            sidecar.BlockMedias[0].TapeInformation = tapePartitions;
        }

        UpdateStatus(Localization.Core.Checking_filesystems);

        if(_aborted) return;

        List<Partition> partitions = Partitions.GetAll(image);
        Partitions.AddSchemesToStats(partitions);

        sidecar.BlockMedias[0].FileSystemInformation = [];

        if(partitions.Count > 0)
        {
            foreach(Partition partition in partitions)
            {
                if(_aborted) return;

                var fsInfo = new CommonTypes.AaruMetadata.Partition
                {
                    Description = partition.Description,
                    EndSector   = partition.End,
                    Name        = partition.Name,
                    Sequence    = (uint)partition.Sequence,
                    StartSector = partition.Start,
                    Type        = partition.Type
                };

                List<FileSystem> lstFs = [];

                foreach(IFilesystem fs in plugins.Filesystems.Values)
                {
                    try
                    {
                        if(_aborted) return;

                        if(fs is null) continue;

                        if(!fs.Identify(image, partition)) continue;

                        if(fs is IReadOnlyFilesystem rofs &&
                           rofs.Mount(image, partition, encoding, null, null) == ErrorNumber.NoError)
                        {
                            UpdateStatus(string.Format(Localization.Core.Mounting_0, rofs.Metadata.Type));

                            rofs.Metadata.Contents = Files(rofs);

                            lstFs.Add(rofs.Metadata);
                            Statistics.AddFilesystem(rofs.Metadata.Type);

                            rofs.Unmount();
                        }
                        else
                        {
                            fs.GetInformation(image, partition, encoding, out _, out FileSystem fsMetadata);

                            lstFs.Add(fsMetadata);
                            Statistics.AddFilesystem(fsMetadata.Type);
                        }
                    }
                    catch(Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                    }
                }

                if(lstFs.Count > 0) fsInfo.FileSystems = lstFs;

                sidecar.BlockMedias[0].FileSystemInformation.Add(fsInfo);
            }
        }
        else
        {
            if(_aborted) return;

            var fsInfo = new CommonTypes.AaruMetadata.Partition
            {
                StartSector = 0,
                EndSector   = image.Info.Sectors - 1
            };

            var wholePart = new Partition
            {
                Name   = Localization.Core.Whole_device,
                Length = image.Info.Sectors,
                Size   = image.Info.Sectors * image.Info.SectorSize
            };

            List<FileSystem> lstFs = [];

            foreach(IFilesystem fs in plugins.Filesystems.Values)
            {
                try
                {
                    if(_aborted) return;

                    if(fs is null) continue;

                    if(!fs.Identify(image, wholePart)) continue;

                    if(fs is IReadOnlyFilesystem rofs &&
                       rofs.Mount(image, wholePart, encoding, null, null) == ErrorNumber.NoError)
                    {
                        UpdateStatus(string.Format(Localization.Core.Mounting_0, rofs.Metadata.Type));

                        rofs.Metadata.Contents = Files(rofs);

                        lstFs.Add(rofs.Metadata);
                        Statistics.AddFilesystem(rofs.Metadata.Type);

                        rofs.Unmount();
                    }
                    else
                    {
                        fs.GetInformation(image, wholePart, encoding, out _, out FileSystem fsMetadata);

                        lstFs.Add(fsMetadata);
                        Statistics.AddFilesystem(fsMetadata.Type);
                    }
                }
                catch(Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                }
            }

            if(lstFs.Count > 0) fsInfo.FileSystems = lstFs;

            sidecar.BlockMedias[0].FileSystemInformation.Add(fsInfo);
        }

        UpdateStatus(Localization.Core.Saving_metadata);

        if(image.Info.Cylinders > 0 && image.Info is { Heads: > 0, SectorsPerTrack: > 0 })
        {
            sidecar.BlockMedias[0].Cylinders       = image.Info.Cylinders;
            sidecar.BlockMedias[0].Heads           = (ushort)image.Info.Heads;
            sidecar.BlockMedias[0].SectorsPerTrack = image.Info.SectorsPerTrack;
        }

        if(image.Info.ReadableMediaTags.Contains(MediaTagType.ATA_IDENTIFY))
        {
            Identify.IdentifyDevice? ataId = null;
            errno = image.ReadMediaTag(MediaTagType.ATA_IDENTIFY, out buffer);

            if(errno == ErrorNumber.NoError) ataId = Identify.Decode(buffer);

            switch(ataId)
            {
                case { CurrentCylinders: > 0, CurrentHeads: > 0, CurrentSectorsPerTrack: > 0 }:
                    sidecar.BlockMedias[0].Cylinders       = ataId.Value.CurrentCylinders;
                    sidecar.BlockMedias[0].Heads           = ataId.Value.CurrentHeads;
                    sidecar.BlockMedias[0].SectorsPerTrack = ataId.Value.CurrentSectorsPerTrack;

                    break;
                case { Cylinders: > 0, Heads: > 0, SectorsPerTrack: > 0 }:
                    sidecar.BlockMedias[0].Cylinders       = ataId.Value.Cylinders;
                    sidecar.BlockMedias[0].Heads           = ataId.Value.Heads;
                    sidecar.BlockMedias[0].SectorsPerTrack = ataId.Value.SectorsPerTrack;

                    break;
            }
        }

        sidecar.BlockMedias[0].DumpHardware = image.DumpHardware;

        // TODO: This is more of a hack, redo it planned for >4.0
        string trkFormat = null;

        switch(image.Info.MediaType)
        {
            case CommonTypes.MediaType.Apple32SS:
            case CommonTypes.MediaType.Apple32DS:
                trkFormat = "Apple GCR (DOS 3.2)";

                break;
            case CommonTypes.MediaType.Apple33SS:
            case CommonTypes.MediaType.Apple33DS:
                trkFormat = "Apple GCR (DOS 3.3)";

                break;
            case CommonTypes.MediaType.AppleSonySS:
            case CommonTypes.MediaType.AppleSonyDS:
                trkFormat = "Apple GCR (Sony)";

                break;
            case CommonTypes.MediaType.AppleFileWare:
                trkFormat = "Apple GCR (Twiggy)";

                break;
            case CommonTypes.MediaType.DOS_525_SS_DD_9:
            case CommonTypes.MediaType.DOS_525_DS_DD_8:
            case CommonTypes.MediaType.DOS_525_DS_DD_9:
            case CommonTypes.MediaType.DOS_525_HD:
            case CommonTypes.MediaType.DOS_35_SS_DD_8:
            case CommonTypes.MediaType.DOS_35_SS_DD_9:
            case CommonTypes.MediaType.DOS_35_DS_DD_8:
            case CommonTypes.MediaType.DOS_35_DS_DD_9:
            case CommonTypes.MediaType.DOS_35_HD:
            case CommonTypes.MediaType.DOS_35_ED:
            case CommonTypes.MediaType.DMF:
            case CommonTypes.MediaType.DMF_82:
            case CommonTypes.MediaType.XDF_525:
            case CommonTypes.MediaType.XDF_35:
            case CommonTypes.MediaType.IBM53FD_256:
            case CommonTypes.MediaType.IBM53FD_512:
            case CommonTypes.MediaType.IBM53FD_1024:
            case CommonTypes.MediaType.RX02:
            case CommonTypes.MediaType.RX03:
            case CommonTypes.MediaType.RX50:
            case CommonTypes.MediaType.ACORN_525_SS_DD_40:
            case CommonTypes.MediaType.ACORN_525_SS_DD_80:
            case CommonTypes.MediaType.ACORN_525_DS_DD:
            case CommonTypes.MediaType.ACORN_35_DS_DD:
            case CommonTypes.MediaType.ACORN_35_DS_HD:
            case CommonTypes.MediaType.ATARI_525_ED:
            case CommonTypes.MediaType.ATARI_525_DD:
            case CommonTypes.MediaType.ATARI_35_SS_DD:
            case CommonTypes.MediaType.ATARI_35_DS_DD:
            case CommonTypes.MediaType.ATARI_35_SS_DD_11:
            case CommonTypes.MediaType.ATARI_35_DS_DD_11:
            case CommonTypes.MediaType.DOS_525_SS_DD_8:
            case CommonTypes.MediaType.NEC_8_DD:
            case CommonTypes.MediaType.NEC_525_SS:
            case CommonTypes.MediaType.NEC_525_DS:
            case CommonTypes.MediaType.NEC_525_HD:
            case CommonTypes.MediaType.NEC_35_HD_8:
            case CommonTypes.MediaType.NEC_35_HD_15:
            case CommonTypes.MediaType.NEC_35_TD:
            case CommonTypes.MediaType.FDFORMAT_525_DD:
            case CommonTypes.MediaType.FDFORMAT_525_HD:
            case CommonTypes.MediaType.FDFORMAT_35_DD:
            case CommonTypes.MediaType.FDFORMAT_35_HD:
            case CommonTypes.MediaType.Apricot_35:
            case CommonTypes.MediaType.CompactFloppy:
            case CommonTypes.MediaType.MetaFloppy_Mod_I:
            case CommonTypes.MediaType.MetaFloppy_Mod_II:
                trkFormat = "IBM MFM";

                break;
            case CommonTypes.MediaType.ATARI_525_SD:
            case CommonTypes.MediaType.NEC_8_SD:
            case CommonTypes.MediaType.ACORN_525_SS_SD_40:
            case CommonTypes.MediaType.ACORN_525_SS_SD_80:
            case CommonTypes.MediaType.RX01:
            case CommonTypes.MediaType.IBM23FD:
            case CommonTypes.MediaType.IBM33FD_128:
            case CommonTypes.MediaType.IBM33FD_256:
            case CommonTypes.MediaType.IBM33FD_512:
            case CommonTypes.MediaType.IBM43FD_128:
            case CommonTypes.MediaType.IBM43FD_256:
                trkFormat = "IBM FM";

                break;
            case CommonTypes.MediaType.CBM_35_DD:
                trkFormat = "Commodore MFM";

                break;
            case CommonTypes.MediaType.CBM_AMIGA_35_HD:
            case CommonTypes.MediaType.CBM_AMIGA_35_DD:
                trkFormat = "Amiga MFM";

                break;
            case CommonTypes.MediaType.CBM_1540:
            case CommonTypes.MediaType.CBM_1540_Ext:
            case CommonTypes.MediaType.CBM_1571:
                trkFormat = "Commodore GCR";

                break;
            case CommonTypes.MediaType.SHARP_525_9:
            case CommonTypes.MediaType.SHARP_35_9:
                break;
            case CommonTypes.MediaType.ECMA_99_15:
            case CommonTypes.MediaType.ECMA_99_26:
            case CommonTypes.MediaType.ECMA_99_8:
                trkFormat = "ISO MFM";

                break;
            case CommonTypes.MediaType.ECMA_54:
            case CommonTypes.MediaType.ECMA_59:
            case CommonTypes.MediaType.ECMA_66:
            case CommonTypes.MediaType.ECMA_69_8:
            case CommonTypes.MediaType.ECMA_69_15:
            case CommonTypes.MediaType.ECMA_69_26:
            case CommonTypes.MediaType.ECMA_70:
            case CommonTypes.MediaType.ECMA_78:
            case CommonTypes.MediaType.ECMA_78_2:
                trkFormat = "ISO FM";

                break;
            default:
                trkFormat = "Unknown";

                break;
        }

        // TODO: Implement support for getting CHS from SCSI mode pages
    }
}