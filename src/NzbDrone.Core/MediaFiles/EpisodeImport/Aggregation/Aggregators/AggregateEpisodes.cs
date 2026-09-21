using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    public class AggregateEpisodes : IAggregateLocalEpisode
    {
        public int Order => 1;

        private readonly IParsingService _parsingService;
        private readonly IEpisodeService _episodeService;

        public AggregateEpisodes(IParsingService parsingService, IEpisodeService episodeService)
        {
            _parsingService = parsingService;
            _episodeService = episodeService;
        }

        public LocalEpisode Aggregate(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            localEpisode.Episodes = GetEpisodes(localEpisode);

            return localEpisode;
        }

        private ParsedEpisodeInfo GetBestEpisodeInfo(LocalEpisode localEpisode)
        {
            var parsedEpisodeInfo = localEpisode.FileEpisodeInfo;
            var downloadClientEpisodeInfo = localEpisode.DownloadClientEpisodeInfo;
            var folderEpisodeInfo = localEpisode.FolderEpisodeInfo;

            if (!localEpisode.OtherVideoFiles && !SceneChecker.IsSceneTitle(Path.GetFileNameWithoutExtension(localEpisode.Path)))
            {
                if (downloadClientEpisodeInfo != null &&
                    !downloadClientEpisodeInfo.FullSeason &&
                    PreferOtherEpisodeInfo(parsedEpisodeInfo, downloadClientEpisodeInfo))
                {
                    parsedEpisodeInfo = localEpisode.DownloadClientEpisodeInfo;
                }
                else if (folderEpisodeInfo != null &&
                         !folderEpisodeInfo.FullSeason &&
                         PreferOtherEpisodeInfo(parsedEpisodeInfo, folderEpisodeInfo))
                {
                    parsedEpisodeInfo = localEpisode.FolderEpisodeInfo;
                }
            }

            if (parsedEpisodeInfo == null)
            {
                parsedEpisodeInfo = GetSpecialEpisodeInfo(localEpisode, parsedEpisodeInfo);
            }

            return parsedEpisodeInfo;
        }

        private ParsedEpisodeInfo GetSpecialEpisodeInfo(LocalEpisode localEpisode, ParsedEpisodeInfo parsedEpisodeInfo)
        {
            var title = Path.GetFileNameWithoutExtension(localEpisode.Path);
            var specialEpisodeInfo = _parsingService.ParseSpecialEpisodeTitle(parsedEpisodeInfo, title, localEpisode.Series);

            return specialEpisodeInfo;
        }

        private List<Episode> GetEpisodes(LocalEpisode localEpisode)
        {
            var numberedTitleEpisode = GetNumberedTitleEpisode(localEpisode);

            if (numberedTitleEpisode != null)
            {
                return new List<Episode> { numberedTitleEpisode };
            }

            return GetBaselineEpisodes(localEpisode);
        }

        private List<Episode> GetBaselineEpisodes(LocalEpisode localEpisode)
        {
            var bestEpisodeInfoForEpisodes = GetBestEpisodeInfo(localEpisode);
            var isMediaFile = MediaFileExtensions.Extensions.Contains(Path.GetExtension(localEpisode.Path));

            if (bestEpisodeInfoForEpisodes == null)
            {
                return new List<Episode>();
            }

            if (ValidateParsedEpisodeInfo.ValidateForSeriesType(bestEpisodeInfoForEpisodes, localEpisode.Series, isMediaFile))
            {
                var episodes = _parsingService.GetEpisodes(bestEpisodeInfoForEpisodes, localEpisode.Series, localEpisode.SceneSource);

                if (episodes.Empty() && bestEpisodeInfoForEpisodes.IsPossibleSpecialEpisode)
                {
                    var parsedSpecialEpisodeInfo = GetSpecialEpisodeInfo(localEpisode, bestEpisodeInfoForEpisodes);

                    if (parsedSpecialEpisodeInfo != null)
                    {
                        episodes = _parsingService.GetEpisodes(parsedSpecialEpisodeInfo, localEpisode.Series, localEpisode.SceneSource);
                    }
                }

                return episodes;
            }

            return new List<Episode>();
        }

        private Episode GetNumberedTitleEpisode(LocalEpisode localEpisode)
        {
            if (localEpisode.Series.SeriesType != SeriesTypes.Standard || localEpisode.Series.UseSceneNumbering ||
                (localEpisode.FileEpisodeInfo != null && !localEpisode.FileEpisodeInfo.FullSeason) ||
                !MediaFileExtensions.Extensions.Contains(Path.GetExtension(localEpisode.Path)))
            {
                return null;
            }

            var match = Regex.Match(Path.GetFileNameWithoutExtension(localEpisode.Path), @"\A(?<number>[0-9]{1,3})[ ._-]+(?<title>\S.*)\z");

            if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var number) || number == 0)
            {
                return null;
            }

            var contexts = new[] { localEpisode.FileEpisodeInfo, localEpisode.FolderEpisodeInfo, localEpisode.DownloadClientEpisodeInfo }
                .Where(info => info != null).ToList();

            if (contexts.Empty() || contexts.Any(info => info.SeasonNumber <= 0 || info.IsMultiSeason || info.IsDaily ||
                    info.IsAbsoluteNumbering || info.Special || info.IsSeasonExtra ||
                    (info.EpisodeNumbers.Any() && !info.EpisodeNumbers.Contains(number))) ||
                contexts.Select(info => info.SeasonNumber).Distinct().Count() != 1)
            {
                return null;
            }

            // Keep part numbers and articles: the existing title normalizers intentionally discard them.
            var title = NormalizeNumberedTitle(match.Groups["title"].Value);
            var matches = _episodeService.GetEpisodesBySeason(localEpisode.Series.Id, contexts[0].SeasonNumber)
                .Where(episode => !string.IsNullOrWhiteSpace(episode.Title) && NormalizeNumberedTitle(episode.Title) == title)
                .ToList();

            if (title.Length == 0 || matches.Count != 1 || matches[0].EpisodeNumber != number)
            {
                return null;
            }

            var parsedInfo = contexts[0].JsonClone();
            parsedInfo.FullSeason = false;
            parsedInfo.EpisodeNumbers = new[] { number };

            // Scene-title aliases can remap seasons even when UseSceneNumbering is disabled.
            var mappedEpisodes = _parsingService.GetEpisodes(parsedInfo, localEpisode.Series, localEpisode.SceneSource);

            if (mappedEpisodes.Count != 1 || mappedEpisodes[0].Id != matches[0].Id)
            {
                return null;
            }

            try
            {
                var baselineEpisodes = GetBaselineEpisodes(localEpisode);
                if (baselineEpisodes.Count != 1 || baselineEpisodes[0].Id != matches[0].Id)
                {
                    localEpisode.DevImportFix = "numbered-title";
                }
            }
            catch (Exception)
            {
                // Attribution must not break a validated import; an unknown baseline earns no credit.
            }

            localEpisode.FileEpisodeInfo = parsedInfo;

            return matches[0];
        }

        private static string NormalizeNumberedTitle(string title)
        {
            return new string(title.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        private bool PreferOtherEpisodeInfo(ParsedEpisodeInfo fileEpisodeInfo, ParsedEpisodeInfo otherEpisodeInfo)
        {
            if (fileEpisodeInfo == null)
            {
                return true;
            }

            // When the files episode info is not absolute prefer it over a parsed episode info that is absolute
            if (!fileEpisodeInfo.IsAbsoluteNumbering && otherEpisodeInfo.IsAbsoluteNumbering)
            {
                return false;
            }

            return true;
        }
    }
}
