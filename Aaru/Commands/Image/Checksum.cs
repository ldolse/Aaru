// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Checksum.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Commands.
//
// --[ Description ] ----------------------------------------------------------
//
//     Implements the 'checksum' command.
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
using System.ComponentModel;
using System.Threading;
using Aaru.CommonTypes;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Interfaces;
using Aaru.CommonTypes.Structs;
using Aaru.Core;
using Aaru.Localization;
using Aaru.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Aaru.Commands.Image;

sealed class ChecksumCommand : Command<ChecksumCommand.Settings>
{
    // How many sectors to read at once
    const uint SECTORS_TO_READ = 256;

    // How many bytes to read at once
    const int    BYTES_TO_READ = 65536;
    const string MODULE_NAME   = "Checksum command";

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        MainClass.PrintCopyright();

        Statistics.AddCommand("checksum");

        AaruLogging.Debug(MODULE_NAME, "--adler32={0}",          settings.Adler32);
        AaruLogging.Debug(MODULE_NAME, "--crc16={0}",            settings.Crc16);
        AaruLogging.Debug(MODULE_NAME, "--crc32={0}",            settings.Crc32);
        AaruLogging.Debug(MODULE_NAME, "--crc64={0}",            settings.Crc64);
        AaruLogging.Debug(MODULE_NAME, "--debug={0}",            settings.Debug);
        AaruLogging.Debug(MODULE_NAME, "--fletcher16={0}",       settings.Fletcher16);
        AaruLogging.Debug(MODULE_NAME, "--fletcher32={0}",       settings.Fletcher32);
        AaruLogging.Debug(MODULE_NAME, "--input={0}",            Markup.Escape(settings.ImagePath ?? ""));
        AaruLogging.Debug(MODULE_NAME, "--md5={0}",              settings.Md5);
        AaruLogging.Debug(MODULE_NAME, "--separated-tracks={0}", settings.SeparatedTracks);
        AaruLogging.Debug(MODULE_NAME, "--sha1={0}",             settings.Sha1);
        AaruLogging.Debug(MODULE_NAME, "--sha256={0}",           settings.Sha256);
        AaruLogging.Debug(MODULE_NAME, "--sha384={0}",           settings.Sha384);
        AaruLogging.Debug(MODULE_NAME, "--sha512={0}",           settings.Sha512);
        AaruLogging.Debug(MODULE_NAME, "--spamsum={0}",          settings.SpamSum);
        AaruLogging.Debug(MODULE_NAME, "--verbose={0}",          settings.Verbose);
        AaruLogging.Debug(MODULE_NAME, "--whole-disc={0}",       settings.WholeDisc);

        IFilter inputFilter = null;

        Core.Spectre.ProgressSingleSpinner(ctx =>
        {
            ctx.AddTask(UI.Identifying_file_filter).IsIndeterminate();
            inputFilter = PluginRegister.Singleton.GetFilter(settings.ImagePath);
        });

        if(inputFilter == null)
        {
            AaruLogging.Error(UI.Cannot_open_specified_file);

            return (int)ErrorNumber.CannotOpenFile;
        }

        IBaseImage inputFormat = null;

        Core.Spectre.ProgressSingleSpinner(ctx =>
        {
            ctx.AddTask($"[slateblue1]{UI.Identifying_image_format}[/]").IsIndeterminate();
            inputFormat = ImageFormat.Detect(inputFilter);
        });

        if(inputFormat == null)
        {
            AaruLogging.Error(UI.Unable_to_recognize_image_format_not_checksumming);

            return (int)ErrorNumber.UnrecognizedFormat;
        }

        ErrorNumber opened = ErrorNumber.NoData;

        Core.Spectre.ProgressSingleSpinner(ctx =>
        {
            ctx.AddTask($"[slateblue1]{UI.Invoke_Opening_image_file}[/]").IsIndeterminate();
            opened = inputFormat.Open(inputFilter);
        });

        if(opened != ErrorNumber.NoError)
        {
            AaruLogging.WriteLine(UI.Unable_to_open_image_format);
            AaruLogging.WriteLine(Localization.Core.Error_0, opened);

            return (int)opened;
        }

        Statistics.AddMediaFormat(inputFormat.Format);
        Statistics.AddMedia(inputFormat.Info.MediaType, false);
        Statistics.AddFilter(inputFilter.Name);
        var enabledChecksums = new EnableChecksum();

        if(settings.Adler32) enabledChecksums |= EnableChecksum.Adler32;

        if(settings.Crc16) enabledChecksums |= EnableChecksum.Crc16;

        if(settings.Crc32) enabledChecksums |= EnableChecksum.Crc32;

        if(settings.Crc64) enabledChecksums |= EnableChecksum.Crc64;

        if(settings.Md5) enabledChecksums |= EnableChecksum.Md5;

        if(settings.Sha1) enabledChecksums |= EnableChecksum.Sha1;

        if(settings.Sha256) enabledChecksums |= EnableChecksum.Sha256;

        if(settings.Sha384) enabledChecksums |= EnableChecksum.Sha384;

        if(settings.Sha512) enabledChecksums |= EnableChecksum.Sha512;

        if(settings.SpamSum) enabledChecksums |= EnableChecksum.SpamSum;

        if(settings.Fletcher16) enabledChecksums |= EnableChecksum.Fletcher16;

        if(settings.Fletcher32) enabledChecksums |= EnableChecksum.Fletcher32;

        Checksum mediaChecksum = null;

        ErrorNumber errno = ErrorNumber.NoError;

        switch(inputFormat)
        {
            case IOpticalMediaImage { Tracks: not null } opticalInput:
                try
                {
                    Checksum trackChecksum = null;

                    if(settings.WholeDisc) mediaChecksum = new Checksum(enabledChecksums);

                    List<Track> inputTracks = opticalInput.Tracks;

                    AnsiConsole.Progress()
                               .AutoClear(true)
                               .HideCompleted(true)
                               .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                               .Start(ctx =>
                                {
                                    ProgressTask discTask = ctx.AddTask(Localization.Core.Hashing_tracks);
                                    discTask.MaxValue = inputTracks.Count;

                                    foreach(Track currentTrack in inputTracks)
                                    {
                                        discTask.Description =
                                            string.Format(UI.Hashing_track_0_of_1,
                                                          discTask.Value + 1,
                                                          inputTracks.Count);

                                        ProgressTask trackTask = ctx.AddTask(UI.Hashing_sector);

                                        /*
                                        if(currentTrack.StartSector - previousTrackEnd != 0 && wholeDisc)
                                            for(ulong i = previousTrackEnd + 1; i < currentTrack.StartSector; i++)
                                            {
                                                AaruLogging.Write("\rHashing track-less sector {0}", i);

                                                byte[] hiddenSector = inputFormat.ReadSector(i);

                                                mediaChecksum?.Update(hiddenSector);
                                            }
                                        */

                                        AaruLogging.Debug(MODULE_NAME,
                                                          UI.Track_0_starts_at_sector_1_and_ends_at_sector_2,
                                                          currentTrack.Sequence,
                                                          currentTrack.StartSector,
                                                          currentTrack.EndSector);

                                        if(settings.SeparatedTracks) trackChecksum = new Checksum(enabledChecksums);

                                        ulong sectors = currentTrack.EndSector - currentTrack.StartSector + 1;

                                        trackTask.MaxValue = sectors;

                                        ulong doneSectors = 0;

                                        while(doneSectors < sectors)
                                        {
                                            byte[] sector;

                                            if(sectors - doneSectors >= SECTORS_TO_READ)
                                            {
                                                errno = opticalInput.ReadSectors(doneSectors,
                                                    SECTORS_TO_READ,
                                                    currentTrack.Sequence,
                                                    out sector,
                                                    out _);

                                                trackTask.Description =
                                                    string.Format(UI.Hashing_sectors_0_to_2_of_track_1,
                                                                  doneSectors,
                                                                  currentTrack.Sequence,
                                                                  doneSectors + SECTORS_TO_READ);

                                                if(errno != ErrorNumber.NoError)
                                                {
                                                    AaruLogging
                                                       .Error(string
                                                                 .Format(UI
                                                                            .Error_0_while_reading_1_sectors_from_sector_2,
                                                                         errno,
                                                                         SECTORS_TO_READ,
                                                                         doneSectors));

                                                    return;
                                                }

                                                doneSectors += SECTORS_TO_READ;
                                            }
                                            else
                                            {
                                                errno = opticalInput.ReadSectors(doneSectors,
                                                    (uint)(sectors - doneSectors),
                                                    currentTrack.Sequence,
                                                    out sector,
                                                    out _);

                                                trackTask.Description =
                                                    string.Format(UI.Hashing_sectors_0_to_2_of_track_1,
                                                                  doneSectors,
                                                                  currentTrack.Sequence,
                                                                  doneSectors + (sectors - doneSectors));

                                                if(errno != ErrorNumber.NoError)
                                                {
                                                    AaruLogging
                                                       .Error(string
                                                                 .Format(UI
                                                                            .Error_0_while_reading_1_sectors_from_sector_2,
                                                                         errno,
                                                                         sectors - doneSectors,
                                                                         doneSectors));

                                                    return;
                                                }

                                                doneSectors += sectors - doneSectors;
                                            }

                                            if(settings.WholeDisc) mediaChecksum?.Update(sector);

                                            if(settings.SeparatedTracks) trackChecksum?.Update(sector);

                                            trackTask.Value = doneSectors;
                                        }

                                        trackTask.StopTask();
                                        AaruLogging.WriteLine();

                                        if(!settings.SeparatedTracks) continue;

                                        if(trackChecksum == null) continue;

                                        foreach(CommonTypes.AaruMetadata.Checksum chk in trackChecksum.End())
                                        {
                                            AaruLogging.WriteLine(UI.Checksums_Track_0_has_1_2,
                                                                  currentTrack.Sequence,
                                                                  chk.Type,
                                                                  chk.Value);
                                        }

                                        discTask.Increment(1);
                                    }

                                    /*
                                    if(opticalInput.Info.Sectors - previousTrackEnd != 0 && wholeDisc)
                                        for(ulong i = previousTrackEnd + 1; i < opticalInput.Info.Sectors; i++)
                                        {
                                            AaruLogging.Write("\rHashing track-less sector {0}", i);

                                            byte[] hiddenSector = inputFormat.ReadSector(i);
                                            mediaChecksum?.Update(hiddenSector);
                                        }
                                    */

                                    if(!settings.WholeDisc) return;

                                    if(mediaChecksum == null) return;

                                    AaruLogging.WriteLine();

                                    foreach(CommonTypes.AaruMetadata.Checksum chk in mediaChecksum.End())
                                        AaruLogging.WriteLine(UI.Checksums_Disc_has_0_1, chk.Type, chk.Value);
                                });

                    if(errno != ErrorNumber.NoError) return (int)errno;
                }
                catch(Exception ex)
                {
                    if(settings.Debug)
                        AaruLogging.Debug(Localization.Core.Could_not_get_tracks_because_0, ex.Message);
                    else
                        AaruLogging.WriteLine(UI.Unable_to_get_separate_tracks_not_checksumming_them);
                }

                break;

            case ITapeImage { IsTape: true, Files.Count: > 0 } tapeImage:
            {
                Checksum trackChecksum = null;

                if(settings.WholeDisc) mediaChecksum = new Checksum(enabledChecksums);

                ulong previousFileEnd = 0;

                AnsiConsole.Progress()
                           .AutoClear(true)
                           .HideCompleted(true)
                           .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                           .Start(ctx =>
                            {
                                ProgressTask tapeTask = ctx.AddTask(Localization.Core.Hashing_files);
                                tapeTask.MaxValue = tapeImage.Files.Count;

                                foreach(TapeFile currentFile in tapeImage.Files)
                                {
                                    tapeTask.Description = string.Format(UI.Hashing_file_0_of_1,
                                                                         currentFile.File,
                                                                         tapeImage.Files.Count);

                                    if(currentFile.FirstBlock - previousFileEnd != 0 && settings.WholeDisc)
                                    {
                                        ProgressTask preFileTask = ctx.AddTask(UI.Hashing_sector);
                                        preFileTask.MaxValue = currentFile.FirstBlock - previousFileEnd;

                                        for(ulong i = previousFileEnd + 1; i < currentFile.FirstBlock; i++)
                                        {
                                            preFileTask.Description = string.Format(UI.Hashing_file_less_block_0, i);

                                            errno = tapeImage.ReadSector(i, false, out byte[] hiddenSector, out _);

                                            if(errno != ErrorNumber.NoError)
                                            {
                                                AaruLogging.Error(string.Format(UI.Error_0_while_reading_block_1,
                                                                                    errno,
                                                                                    i));

                                                return;
                                            }

                                            mediaChecksum?.Update(hiddenSector);
                                            preFileTask.Increment(1);
                                        }

                                        preFileTask.StopTask();
                                    }

                                    AaruLogging.Debug(MODULE_NAME,
                                                      UI.File_0_starts_at_block_1_and_ends_at_block_2,
                                                      currentFile.File,
                                                      currentFile.FirstBlock,
                                                      currentFile.LastBlock);

                                    if(settings.SeparatedTracks) trackChecksum = new Checksum(enabledChecksums);

                                    ulong sectors     = currentFile.LastBlock - currentFile.FirstBlock + 1;
                                    ulong doneSectors = 0;

                                    ProgressTask fileTask = ctx.AddTask(UI.Hashing_sector);
                                    fileTask.MaxValue = sectors;

                                    while(doneSectors < sectors)
                                    {
                                        byte[] sector;

                                        if(sectors - doneSectors >= SECTORS_TO_READ)
                                        {
                                            errno = tapeImage.ReadSectors(doneSectors + currentFile.FirstBlock,
                                                                          false,
                                                                          SECTORS_TO_READ,
                                                                          out sector,
                                                                          out _);

                                            if(errno != ErrorNumber.NoError)
                                            {
                                                AaruLogging
                                                   .Error(string
                                                             .Format(UI.Error_0_while_reading_1_sectors_from_sector_2,
                                                                     errno,
                                                                     SECTORS_TO_READ,
                                                                     doneSectors + currentFile.FirstBlock));

                                                return;
                                            }

                                            fileTask.Description = string.Format(UI.Hashing_blocks_0_to_2_of_file_1,
                                                doneSectors,
                                                currentFile.File,
                                                doneSectors + SECTORS_TO_READ);

                                            doneSectors += SECTORS_TO_READ;
                                        }
                                        else
                                        {
                                            errno = tapeImage.ReadSectors(doneSectors + currentFile.FirstBlock,
                                                                          false,
                                                                          (uint)(sectors - doneSectors),
                                                                          out sector,
                                                                          out _);

                                            if(errno != ErrorNumber.NoError)
                                            {
                                                AaruLogging
                                                   .Error(string
                                                             .Format(UI.Error_0_while_reading_1_sectors_from_sector_2,
                                                                     errno,
                                                                     sectors     - doneSectors,
                                                                     doneSectors + currentFile.FirstBlock));

                                                return;
                                            }

                                            fileTask.Description = string.Format(UI.Hashing_blocks_0_to_2_of_file_1,
                                                doneSectors,
                                                currentFile.File,
                                                doneSectors + (sectors - doneSectors));

                                            doneSectors += sectors - doneSectors;
                                        }

                                        fileTask.Value = doneSectors;

                                        if(settings.WholeDisc) mediaChecksum?.Update(sector);

                                        if(settings.SeparatedTracks) trackChecksum?.Update(sector);
                                    }

                                    fileTask.StopTask();
                                    AaruLogging.WriteLine();

                                    if(settings.SeparatedTracks)
                                    {
                                        if(trackChecksum != null)
                                        {
                                            foreach(CommonTypes.AaruMetadata.Checksum chk in trackChecksum.End())
                                            {
                                                AaruLogging.WriteLine(UI.Checksums_File_0_has_1_2,
                                                                      currentFile.File,
                                                                      chk.Type,
                                                                      chk.Value);
                                            }
                                        }
                                    }

                                    previousFileEnd = currentFile.LastBlock;

                                    tapeTask.Increment(1);
                                }

                                if(tapeImage.Info.Sectors - previousFileEnd == 0 || !settings.WholeDisc) return;

                                ProgressTask postFileTask = ctx.AddTask(UI.Hashing_sector);
                                postFileTask.MaxValue = tapeImage.Info.Sectors - previousFileEnd;

                                for(ulong i = previousFileEnd + 1; i < tapeImage.Info.Sectors; i++)
                                {
                                    postFileTask.Description = string.Format(UI.Hashing_file_less_block_0, i);

                                    errno = tapeImage.ReadSector(i, false, out byte[] hiddenSector, out _);

                                    if(errno != ErrorNumber.NoError)
                                    {
                                        AaruLogging.Error(string.Format(UI.Error_0_while_reading_block_1, errno, i));

                                        return;
                                    }

                                    mediaChecksum?.Update(hiddenSector);
                                    postFileTask.Increment(1);
                                }
                            });

                if(errno != ErrorNumber.NoError) return (int)errno;

                if(settings.WholeDisc && mediaChecksum != null)
                {
                    AaruLogging.WriteLine();

                    foreach(CommonTypes.AaruMetadata.Checksum chk in mediaChecksum.End())
                        AaruLogging.WriteLine(UI.Checksums_Tape_has_0_1, chk.Type, chk.Value);
                }

                break;
            }

            case IByteAddressableImage { Info.MetadataMediaType: MetadataMediaType.LinearMedia } byteAddressableImage:
            {
                mediaChecksum = new Checksum(enabledChecksums);

                AnsiConsole.Progress()
                           .AutoClear(true)
                           .HideCompleted(true)
                           .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                           .Start(ctx =>
                            {
                                ProgressTask imageTask = ctx.AddTask(UI.Hashing_image);
                                ulong        length    = byteAddressableImage.Info.Sectors;
                                imageTask.MaxValue = length;
                                ulong doneBytes = 0;
                                var   data      = new byte[BYTES_TO_READ];

                                while(doneBytes < length)
                                {
                                    int bytesRead;

                                    if(length - doneBytes >= BYTES_TO_READ)
                                    {
                                        errno = byteAddressableImage.ReadBytes(data, 0, BYTES_TO_READ, out bytesRead);

                                        if(errno != ErrorNumber.NoError)
                                        {
                                            AaruLogging.Error(string.Format(UI.Error_0_while_reading_1_bytes_from_2,
                                                                            errno,
                                                                            BYTES_TO_READ,
                                                                            doneBytes));

                                            return;
                                        }

                                        imageTask.Description =
                                            string.Format(UI.Hashing_bytes_0_to_1,
                                                          doneBytes,
                                                          doneBytes + BYTES_TO_READ);

                                        doneBytes += (ulong)bytesRead;

                                        if(bytesRead == 0) break;
                                    }
                                    else
                                    {
                                        errno = byteAddressableImage.ReadBytes(data,
                                                                               0,
                                                                               (int)(length - doneBytes),
                                                                               out bytesRead);

                                        if(errno != ErrorNumber.NoError)
                                        {
                                            AaruLogging.Error(string.Format(UI.Error_0_while_reading_1_bytes_from_2,
                                                                            errno,
                                                                            length - doneBytes,
                                                                            doneBytes));

                                            return;
                                        }

                                        imageTask.Description =
                                            string.Format($"[slateblue1]{UI.Hashing_bytes_0_to_1}[/]",
                                                          $"[lime]{doneBytes}[/]",
                                                          $"[violet]{doneBytes + (length - doneBytes)}[/]");

                                        doneBytes += length - doneBytes;
                                    }

                                    mediaChecksum.Update(data);
                                    imageTask.Value = doneBytes;
                                }
                            });

                if(errno != ErrorNumber.NoError) return (int)errno;

                AaruLogging.WriteLine();

                foreach(CommonTypes.AaruMetadata.Checksum chk in mediaChecksum.End())
                    AaruLogging.WriteLine(UI.Checksums_Media_has_0_1, chk.Type, chk.Value);

                break;
            }

            default:
            {
                var mediaImage = inputFormat as IMediaImage;
                mediaChecksum = new Checksum(enabledChecksums);

                AnsiConsole.Progress()
                           .AutoClear(true)
                           .HideCompleted(true)
                           .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                           .Start(ctx =>
                            {
                                ProgressTask diskTask = ctx.AddTask(Localization.Core.Hashing_sectors);
                                ulong        sectors  = mediaImage.Info.Sectors;
                                diskTask.MaxValue = sectors;
                                ulong doneSectors = 0;

                                while(doneSectors < sectors)
                                {
                                    byte[] sector;

                                    if(sectors - doneSectors >= SECTORS_TO_READ)
                                    {
                                        errno = mediaImage.ReadSectors(doneSectors,
                                                                       false,
                                                                       SECTORS_TO_READ,
                                                                       out sector,
                                                                       out _);

                                        if(errno != ErrorNumber.NoError)
                                        {
                                            AaruLogging
                                               .Error(string.Format(UI.Error_0_while_reading_1_sectors_from_sector_2,
                                                                    errno,
                                                                    SECTORS_TO_READ,
                                                                    doneSectors));

                                            return;
                                        }

                                        diskTask.Description =
                                            string.Format(UI.Hashing_sectors_0_to_1,
                                                          doneSectors,
                                                          doneSectors + SECTORS_TO_READ);

                                        doneSectors += SECTORS_TO_READ;
                                    }
                                    else
                                    {
                                        errno = mediaImage.ReadSectors(doneSectors,
                                                                       false,
                                                                       (uint)(sectors - doneSectors),
                                                                       out sector,
                                                                       out _);

                                        if(errno != ErrorNumber.NoError)
                                        {
                                            AaruLogging
                                               .Error(string.Format(UI.Error_0_while_reading_1_sectors_from_sector_2,
                                                                    errno,
                                                                    sectors - doneSectors,
                                                                    doneSectors));

                                            return;
                                        }

                                        diskTask.Description = string.Format(UI.Hashing_sectors_0_to_1,
                                                                             doneSectors,
                                                                             doneSectors + (sectors - doneSectors));

                                        doneSectors += sectors - doneSectors;
                                    }

                                    mediaChecksum.Update(sector);
                                    diskTask.Value = doneSectors;
                                }
                            });

                if(errno != ErrorNumber.NoError) return (int)errno;

                AaruLogging.WriteLine();

                foreach(CommonTypes.AaruMetadata.Checksum chk in mediaChecksum.End())
                    AaruLogging.WriteLine(UI.Checksums_Disk_has_0_1, chk.Type, chk.Value);

                break;
            }
        }

        return (int)ErrorNumber.NoError;
    }

#region Nested type: Settings

    public class Settings : ImageFamily
    {
        [LocalizedDescription(nameof(UI.Calculates_Adler_32))]
        [CommandOption("-a|--adler32")]
        [DefaultValue(false)]
        public bool Adler32 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_CRC16))]
        [CommandOption("--crc16")]
        [DefaultValue(true)]
        public bool Crc16 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_CRC32))]
        [CommandOption("-c|--crc32")]
        [DefaultValue(true)]
        public bool Crc32 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_CRC64_ECMA))]
        [CommandOption("--crc64")]
        [DefaultValue(true)]
        public bool Crc64 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_Fletcher_16))]
        [CommandOption("--fletcher16")]
        [DefaultValue(false)]
        public bool Fletcher16 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_Fletcher_32))]
        [CommandOption("--fletcher32")]
        [DefaultValue(false)]
        public bool Fletcher32 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_MD5))]
        [CommandOption("-m|--md5")]
        [DefaultValue(true)]
        public bool Md5 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_SHA1))]
        [CommandOption("-s|--sha1")]
        [DefaultValue(true)]
        public bool Sha1 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_SHA256))]
        [CommandOption("--sha256")]
        [DefaultValue(false)]
        public bool Sha256 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_SHA384))]
        [CommandOption("--sha384")]
        [DefaultValue(false)]
        public bool Sha384 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_SHA512))]
        [CommandOption("--sha512")]
        [DefaultValue(true)]
        public bool Sha512 { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_SpamSum_fuzzy_hash))]
        [CommandOption("-f|--spamsum")]
        [DefaultValue(true)]
        public bool SpamSum { get; init; }
        [LocalizedDescription(nameof(UI.Checksums_the_whole_disc))]
        [CommandOption("-w|--whole-disc")]
        [DefaultValue(true)]
        public bool WholeDisc { get; init; }
        [LocalizedDescription(nameof(UI.Checksums_each_track_separately))]
        [CommandOption("-t|--separated-tracks")]
        [DefaultValue(true)]
        public bool SeparatedTracks { get; init; }
        [LocalizedDescription(nameof(UI.Media_image_path))]
        [CommandArgument(0, "<image-path>")]
        public string ImagePath { get; init; }
    }

#endregion
}