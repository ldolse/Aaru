// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Decode.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Commands.
//
// --[ Description ] ----------------------------------------------------------
//
//     Implements the 'decode' command.
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

using System.ComponentModel;
using System.Globalization;
using System.Threading;
using Aaru.CommonTypes;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Interfaces;
using Aaru.Core;
using Aaru.Decoders.ATA;
using Aaru.Decoders.CD;
using Aaru.Decoders.SCSI;
using Aaru.Localization;
using Aaru.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Aaru.Commands.Image;

sealed class DecodeCommand : Command<DecodeCommand.Settings>
{
    const string MODULE_NAME = "Decode command";

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        MainClass.PrintCopyright();

        Statistics.AddCommand("decode");

        AaruLogging.Debug(MODULE_NAME, "--debug={0}",       settings.Debug);
        AaruLogging.Debug(MODULE_NAME, "--disk-tags={0}",   settings.DiskTags);
        AaruLogging.Debug(MODULE_NAME, "--input={0}",       Markup.Escape(settings.ImagePath ?? ""));
        AaruLogging.Debug(MODULE_NAME, "--length={0}",      Markup.Escape(settings.Length    ?? ""));
        AaruLogging.Debug(MODULE_NAME, "--sector-tags={0}", settings.SectorTags);
        AaruLogging.Debug(MODULE_NAME, "--start={0}",       settings.StartSector);
        AaruLogging.Debug(MODULE_NAME, "--verbose={0}",     settings.Verbose);

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

        IMediaImage inputFormat = null;
        IBaseImage  baseImage   = null;

        Core.Spectre.ProgressSingleSpinner(ctx =>
        {
            ctx.AddTask(UI.Identifying_image_format).IsIndeterminate();
            baseImage   = ImageFormat.Detect(inputFilter);
            inputFormat = baseImage as IMediaImage;
        });

        if(baseImage == null)
        {
            AaruLogging.Error(UI.Unable_to_recognize_image_format_not_decoding);

            return (int)ErrorNumber.UnrecognizedFormat;
        }

        if(inputFormat == null)
        {
            AaruLogging.WriteLine(UI.Command_not_supported_for_this_image_type);

            return (int)ErrorNumber.InvalidArgument;
        }

        ErrorNumber opened = ErrorNumber.NoData;

        Core.Spectre.ProgressSingleSpinner(ctx =>
        {
            ctx.AddTask(UI.Invoke_Opening_image_file).IsIndeterminate();
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

        if(settings.DiskTags)
        {
            if(inputFormat.Info.ReadableMediaTags.Count == 0)
                AaruLogging.WriteLine(UI.There_are_no_media_tags_in_chosen_disc_image);
            else
            {
                foreach(MediaTagType tag in inputFormat.Info.ReadableMediaTags)
                {
                    ErrorNumber errno;

                    switch(tag)
                    {
                        case MediaTagType.SCSI_INQUIRY:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.SCSI_INQUIRY, out byte[] inquiry);

                            if(inquiry == null)
                                AaruLogging.WriteLine(UI.Error_0_reading_SCSI_INQUIRY_response_from_disc_image, errno);
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.SCSI_INQUIRY_command_response}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(Inquiry.Prettify(inquiry));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        case MediaTagType.ATA_IDENTIFY:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.ATA_IDENTIFY, out byte[] identify);

                            if(errno != ErrorNumber.NoError)
                            {
                                AaruLogging.WriteLine(UI.Error_0_reading_ATA_IDENTIFY_DEVICE_response_from_disc_image,
                                                      errno);
                            }
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.ATA_IDENTIFY_DEVICE_command_response}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(Identify.Prettify(identify));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        case MediaTagType.ATAPI_IDENTIFY:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.ATAPI_IDENTIFY, out byte[] identify);

                            if(identify == null)
                            {
                                AaruLogging
                                   .WriteLine(UI.Error_0_reading_ATA_IDENTIFY_PACKET_DEVICE_response_from_disc_image,
                                              errno);
                            }
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.ATA_IDENTIFY_PACKET_DEVICE_command_response}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(Identify.Prettify(identify));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        case MediaTagType.CD_ATIP:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.CD_ATIP, out byte[] atip);

                            if(errno != ErrorNumber.NoError)
                                AaruLogging.WriteLine(UI.Error_0_reading_CD_ATIP_from_disc_image, errno);
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.CD_ATIP}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(ATIP.Prettify(atip));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        case MediaTagType.CD_FullTOC:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.CD_FullTOC, out byte[] fullToc);

                            if(errno != ErrorNumber.NoError)
                                AaruLogging.WriteLine(UI.Error_0_reading_CD_full_TOC_from_disc_image, errno);
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.CD_full_TOC}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(FullTOC.Prettify(fullToc));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        case MediaTagType.CD_PMA:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.CD_PMA, out byte[] pma);

                            if(errno != ErrorNumber.NoError)
                                AaruLogging.WriteLine(UI.Error_0_reading_CD_PMA_from_disc_image, errno);
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.CD_PMA}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(PMA.Prettify(pma));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        case MediaTagType.CD_SessionInfo:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.CD_SessionInfo, out byte[] sessionInfo);

                            if(errno != ErrorNumber.NoError)
                                AaruLogging.WriteLine(UI.Error_0_reading_CD_session_information_from_disc_image, errno);
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.CD_session_information}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(Session.Prettify(sessionInfo));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        case MediaTagType.CD_TEXT:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.CD_TEXT, out byte[] cdText);

                            if(errno != ErrorNumber.NoError)
                                AaruLogging.WriteLine(UI.Error_reading_CD_TEXT_from_disc_image);
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.CD_TEXT}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(CDTextOnLeadIn.Prettify(cdText));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        case MediaTagType.CD_TOC:
                        {
                            errno = inputFormat.ReadMediaTag(MediaTagType.CD_TOC, out byte[] toc);

                            if(toc == null || errno != ErrorNumber.NoError)
                                AaruLogging.WriteLine(UI.Error_reading_CD_TOC_from_disc_image);
                            else
                            {
                                AaruLogging.WriteLine($"[bold]{UI.CD_TOC}[/]");

                                AaruLogging
                                   .WriteLine("================================================================================");

                                AaruLogging.WriteLine(TOC.Prettify(toc));

                                AaruLogging
                                   .WriteLine("================================================================================");
                            }

                            break;
                        }
                        default:
                            AaruLogging.WriteLine(UI.Decoder_for_media_tag_type_0_not_yet_implemented_sorry, tag);

                            break;
                    }
                }
            }
        }

        if(!settings.SectorTags) return (int)ErrorNumber.NoError;

        if(settings.Length.ToLower(CultureInfo.CurrentUICulture) == UI.Parameter_response_all_sectors) {}
        else
        {
            if(!ulong.TryParse(settings.Length, out ulong _))
            {
                AaruLogging.WriteLine(UI.Value_0_is_not_a_valid_number_for_length, settings.Length);
                AaruLogging.WriteLine(UI.Not_decoding_sectors_tags);

                return 3;
            }
        }

        if(inputFormat.Info.ReadableSectorTags.Count == 0)
            AaruLogging.WriteLine(UI.There_are_no_sector_tags_in_chosen_disc_image);
        else
        {
            foreach(SectorTagType tag in inputFormat.Info.ReadableSectorTags)
            {
                switch(tag)
                {
#pragma warning disable PH2077 // TODO: Implement some!
                    default:
                        AaruLogging.WriteLine(UI.Decoder_for_sector_tag_type_0_not_yet_implemented_sorry, tag);

                        break;
#pragma warning restore PH2077
                }
            }
        }

        // TODO: Not implemented

        return (int)ErrorNumber.NoError;
    }

#region Nested type: Settings

    public class Settings : ImageFamily
    {
        [LocalizedDescription(nameof(UI.Decode_media_tags))]
        [DefaultValue(true)]
        [CommandOption("-f|--disk-tags")]
        public bool DiskTags { get; init; }
        [LocalizedDescription(nameof(UI.How_many_sectors_to_decode_or_all))]
        [DefaultValue("all")]
        [CommandOption("-l|--length")]
        public string Length { get; init; }
        [LocalizedDescription(nameof(UI.Decode_sector_tags))]
        [DefaultValue(true)]
        [CommandOption("-p|--sector-tags")]
        public bool SectorTags { get; init; }
        [LocalizedDescription(nameof(UI.Sector_to_start_decoding_from))]
        [DefaultValue(0)]
        [CommandOption("-s|--start")]
        public ulong StartSector { get; init; }
        [LocalizedDescription(nameof(UI.Media_image_path))]
        [CommandArgument(0, "<image-path>")]
        public string ImagePath { get; init; }
    }

#endregion
}