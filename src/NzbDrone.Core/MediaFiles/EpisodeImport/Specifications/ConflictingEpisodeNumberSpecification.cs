using System;
using System.IO;
using System.Text.RegularExpressions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    public class ConflictingEpisodeNumberSpecification : IImportDecisionEngineSpecification
    {
        private static readonly Regex NumberedTitle = new Regex(@"(?:^|[ ._-])S\d{1,2}E(?<episode>\d{1,3})[ ._-]+Episode[ ._-]+(?<titleEpisode>\d{1,3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.ExistingFile || downloadClientItem == null || localEpisode.Series.SeriesType != SeriesTypes.Standard)
            {
                return ImportSpecDecision.Accept();
            }

            var match = NumberedTitle.Match(Path.GetFileNameWithoutExtension(localEpisode.Path));
            if (match.Success && int.Parse(match.Groups["episode"].Value) != int.Parse(match.Groups["titleEpisode"].Value))
            {
                return ImportSpecDecision.Reject(ImportRejectionReason.ConflictingEpisodeNumber, "Filename episode number conflicts with its Episode N title; manually verify and import");
            }

            return ImportSpecDecision.Accept();
        }
    }
}
