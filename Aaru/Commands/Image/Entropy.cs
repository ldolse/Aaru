// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Entropy.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Commands.
//
// --[ Description ] ----------------------------------------------------------
//
//     Implements the 'entropy' command.
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
using System.Threading;
using Aaru.CommonTypes;
using Aaru.CommonTypes.Enums;
using Aaru.CommonTypes.Interfaces;
using Aaru.Core;
using Aaru.Localization;
using Aaru.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Aaru.Commands.Image;

sealed class EntropyCommand : Command<EntropyCommand.Settings>
{
    const  string       MODULE_NAME = "Entropy command";
    static ProgressTask _progressTask1;
    static ProgressTask _progressTask2;

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        MainClass.PrintCopyright();

        Statistics.AddCommand("entropy");

        AaruLogging.Debug(MODULE_NAME, "--debug={0}",              settings.Debug);
        AaruLogging.Debug(MODULE_NAME, "--duplicated-sectors={0}", settings.DuplicatedSectors);
        AaruLogging.Debug(MODULE_NAME, "--input={0}",              Markup.Escape(settings.ImagePath ?? ""));
        AaruLogging.Debug(MODULE_NAME, "--separated-tracks={0}",   settings.SeparatedTracks);
        AaruLogging.Debug(MODULE_NAME, "--verbose={0}",            settings.Verbose);
        AaruLogging.Debug(MODULE_NAME, "--whole-disc={0}",         settings.WholeDisc);

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
            ctx.AddTask(UI.Identifying_image_format).IsIndeterminate();
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

        bool separatedTracks = settings.SeparatedTracks;
        bool wholeDisc       = settings.WholeDisc;

        var entropyCalculator = new Entropy(settings.Debug, inputFormat);

        AnsiConsole.Progress()
                   .AutoClear(true)
                   .HideCompleted(true)
                   .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                   .Start(ctx =>
                    {
                        entropyCalculator.InitProgressEvent += () => { _progressTask1 = ctx.AddTask("Progress"); };

                        entropyCalculator.InitProgress2Event += () => { _progressTask2 = ctx.AddTask("Progress"); };

                        entropyCalculator.UpdateProgressEvent += (text, current, maximum) =>
                        {
                            _progressTask1             ??= ctx.AddTask("Progress");
                            _progressTask1.Description =   text;
                            _progressTask1.Value       =   current;
                            _progressTask1.MaxValue    =   maximum;
                        };

                        entropyCalculator.UpdateProgress2Event += (text, current, maximum) =>
                        {
                            _progressTask2             ??= ctx.AddTask("Progress");
                            _progressTask2.Description =   text;
                            _progressTask2.Value       =   current;
                            _progressTask2.MaxValue    =   maximum;
                        };

                        entropyCalculator.EndProgressEvent += static () =>
                        {
                            _progressTask1?.StopTask();
                            _progressTask1 = null;
                        };

                        entropyCalculator.EndProgress2Event += static () =>
                        {
                            _progressTask2?.StopTask();
                            _progressTask2 = null;
                        };

                        if(settings.WholeDisc && inputFormat is IOpticalMediaImage opticalFormat)
                        {
                            if(opticalFormat.Sessions?.Count > 1)
                            {
                                AaruLogging.Error(UI
                                                     .Calculating_disc_entropy_of_multisession_images_is_not_yet_implemented);

                                wholeDisc = false;
                            }

                            if(opticalFormat.Tracks?.Count == 1) separatedTracks = false;
                        }

                        if(separatedTracks)
                        {
                            EntropyResults[] tracksEntropy =
                                entropyCalculator.CalculateTracksEntropy(settings.DuplicatedSectors);

                            foreach(EntropyResults trackEntropy in tracksEntropy)
                            {
                                AaruLogging.WriteLine(UI.Entropy_for_track_0_is_1,
                                                      trackEntropy.Track,
                                                      trackEntropy.Entropy);

                                if(trackEntropy.UniqueSectors != null)
                                {
                                    AaruLogging.WriteLine(UI.Track_0_has_1_unique_sectors_2,
                                                          trackEntropy.Track,
                                                          trackEntropy.UniqueSectors,
                                                          (double)trackEntropy.UniqueSectors / trackEntropy.Sectors);
                                }
                            }
                        }

                        if(!wholeDisc) return;

                        EntropyResults entropy = inputFormat.Info.MetadataMediaType == MetadataMediaType.LinearMedia
                                                     ? entropyCalculator.CalculateLinearMediaEntropy()
                                                     : entropyCalculator.CalculateMediaEntropy(settings
                                                        .DuplicatedSectors);

                        AaruLogging.WriteLine(UI.Entropy_for_disk_is_0, entropy.Entropy);

                        if(entropy.UniqueSectors != null)
                        {
                            AaruLogging.WriteLine(UI.Disk_has_0_unique_sectors_1,
                                                  entropy.UniqueSectors,
                                                  (double)entropy.UniqueSectors / entropy.Sectors);
                        }
                    });

        return (int)ErrorNumber.NoError;
    }

#region Nested type: Settings

    public class Settings : ImageFamily
    {
        [LocalizedDescription(nameof(UI.Calculates_how_many_sectors_are_duplicated))]
        [DefaultValue(true)]
        [CommandOption("-p|--duplicated-sectors")]
        public bool DuplicatedSectors { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_entropy_for_each_track_separately))]
        [DefaultValue(true)]
        [CommandOption("-t|--separated-tracks")]
        public bool SeparatedTracks { get; init; }
        [LocalizedDescription(nameof(UI.Calculates_entropy_for_the_whole_disc))]
        [DefaultValue(true)]
        [CommandOption("-w|--whole-disc")]
        public bool WholeDisc { get; init; }
        [LocalizedDescription(nameof(UI.Media_image_path))]
        [CommandArgument(0, "<image-path>")]
        public string ImagePath { get; init; }
    }

#endregion
}