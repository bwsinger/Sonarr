using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    public class ConflictingEpisodeNumberSpecification : IImportDecisionEngineSpecification
    {
        private static readonly Regex NumberedTitle = new Regex(@"(?:^|[ ._-])S\d{1,2}E(?<episode>\d{1,3})[ ._-]+Episode[ ._-]+(?<titleEpisode>\d{1,3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex EpisodeRangeTitle = new Regex(@"(?:^|[ ._-])S(?<season>\d{1,2})E(?<first>\d{1,3})-E?(?<last>\d{1,3})[ ._-]+(?<title>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex MultipartTitle = new Regex(@"^(?<title>.+?)\s*\((?<part>[0-9]+)\)\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex TechnicalSuffix = new Regex(@"[ ._-]+(?:\d{3,4}p|[248]k|BluRay|WEB[ ._-]?DL|WEBRip|HDTV|DVDRip|[xh][ .]?26[45]|HEVC|AVC)(?:[ ._-].*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private readonly IEpisodeService _episodeService;

        public ConflictingEpisodeNumberSpecification(IEpisodeService episodeService)
        {
            _episodeService = episodeService;
        }

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

            if (localEpisode.Series.UseSceneNumbering)
            {
                return ImportSpecDecision.Accept();
            }

            var range = EpisodeRangeTitle.Match(Path.GetFileNameWithoutExtension(localEpisode.Path));
            if (!range.Success)
            {
                return ImportSpecDecision.Accept();
            }

            var season = int.Parse(range.Groups["season"].Value);
            var first = int.Parse(range.Groups["first"].Value);
            var last = int.Parse(range.Groups["last"].Value);
            if (season == 0 || first == 0 || last != first + 1 || localEpisode.Episodes.Count != 2 ||
                new[] { localEpisode.FileEpisodeInfo, localEpisode.FolderEpisodeInfo, localEpisode.DownloadClientEpisodeInfo }
                    .Any(info => info != null && (info.SeasonNumber != season || info.IsMultiSeason || info.IsDaily || info.IsAbsoluteNumbering || info.Special || info.IsSeasonExtra)) ||
                localEpisode.Episodes.Any(e => e.SeriesId != localEpisode.Series.Id || e.SeasonNumber != season) ||
                !localEpisode.Episodes.Select(e => e.EpisodeNumber).OrderBy(n => n).SequenceEqual(new[] { first, last }))
            {
                return ImportSpecDecision.Accept();
            }

            var title = NormalizeTitle(TechnicalSuffix.Replace(range.Groups["title"].Value, string.Empty));
            if (title.Length == 0)
            {
                return ImportSpecDecision.Accept();
            }

            // Recognize only an explicit, unique metadata pair; titles cannot safely renumber a download.
            var candidates = _episodeService.GetEpisodesBySeason(localEpisode.Series.Id, season)
                .Where(e => !string.IsNullOrWhiteSpace(e.Title))
                .Where(e => NormalizeTitle(MultipartTitle.Match(e.Title).Groups["title"].Value) == title || NormalizeTitle(e.Title) == title)
                .OrderBy(e => e.EpisodeNumber).ToList();

            if (candidates.Count == 2 && candidates.All(e => e.SeriesId == localEpisode.Series.Id && e.SeasonNumber == season) &&
                MultipartTitle.Match(candidates[0].Title).Groups["part"].Value == "1" &&
                MultipartTitle.Match(candidates[1].Title).Groups["part"].Value == "2" &&
                candidates[1].EpisodeNumber == candidates[0].EpisodeNumber + 1 &&
                !candidates.Select(e => e.Id).OrderBy(id => id).SequenceEqual(localEpisode.Episodes.Select(e => e.Id).OrderBy(id => id)))
            {
                return ImportSpecDecision.Reject(
                    ImportRejectionReason.ConflictingEpisodeNumber,
                    "Filename range S{0:00}E{1:00}-E{2:00} conflicts with its multipart title (S{0:00}E{3:00}-E{4:00}); manually verify and import",
                    season,
                    first,
                    last,
                    candidates[0].EpisodeNumber,
                    candidates[1].EpisodeNumber);
            }

            return ImportSpecDecision.Accept();
        }

        private static string NormalizeTitle(string title)
        {
            return new string(title.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }
}
