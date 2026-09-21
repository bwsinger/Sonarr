using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    public class MatchesGrabSpecification : IImportDecisionEngineSpecification
    {
        private readonly Logger _logger;
        private readonly IParsingService _parsingService;

        public MatchesGrabSpecification(Logger logger, IParsingService parsingService)
        {
            _logger = logger;
            _parsingService = parsingService;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.ExistingFile)
            {
                return ImportSpecDecision.Accept();
            }

            var releaseInfo = localEpisode.Release;

            if (releaseInfo == null || releaseInfo.EpisodeIds.Empty())
            {
                return ImportSpecDecision.Accept();
            }

            var unexpected = localEpisode.Episodes.Where(e => releaseInfo.EpisodeIds.All(o => o != e.Id)).ToList();

            if (unexpected.Any())
            {
                if (MatchesRestoredEpisodePair(localEpisode, downloadClientItem))
                {
                    return ImportSpecDecision.Accept();
                }

                _logger.Debug("Unexpected episode(s) in file: {0}", FormatEpisode(unexpected));

                if (unexpected.Count == 1)
                {
                    return ImportSpecDecision.Reject(ImportRejectionReason.EpisodeNotFoundInRelease, "Episode {0} was not found in the grabbed release: {1}", FormatEpisode(unexpected), releaseInfo.Title);
                }

                return ImportSpecDecision.Reject(ImportRejectionReason.EpisodeNotFoundInRelease, "Episodes {0} were not found in the grabbed release: {1}", FormatEpisode(unexpected), releaseInfo.Title);
            }

            return ImportSpecDecision.Accept();
        }

        private bool MatchesRestoredEpisodePair(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (downloadClientItem?.DownloadClientInfo?.Protocol != DownloadProtocol.Torrent ||
                downloadClientItem.DownloadId.IsNullOrWhiteSpace() ||
                localEpisode.Series?.SeriesType != SeriesTypes.Standard || localEpisode.Series.UseSceneNumbering ||
                localEpisode.Release.EpisodeIds.Count != 1 || localEpisode.Episodes.Count != 1 ||
                downloadClientItem.Title.IsNullOrWhiteSpace() || localEpisode.Release.Title.IsNullOrWhiteSpace())
            {
                return false;
            }

            // A bare space can introduce a numeric episode title. Only restore a lost range
            // separator when the torrent supplies it and the complete release titles agree.
            var range = Regex.Match(downloadClientItem.Title, @"\bS(?<season>[0-9]{2})E(?<first>[0-9]{2})-(?<last>[0-9]{2})(?=[ ._]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!range.Success || NormalizeSeparators(downloadClientItem.Title) != NormalizeSeparators(localEpisode.Release.Title))
            {
                return false;
            }

            var season = int.Parse(range.Groups["season"].Value);
            var first = int.Parse(range.Groups["first"].Value);
            var last = int.Parse(range.Groups["last"].Value);
            var grabbed = Parser.Parser.ParseTitle(localEpisode.Release.Title);
            var torrent = Parser.Parser.ParseTitle(downloadClientItem.Title);
            var file = Parser.Parser.ParseTitle(Path.GetFileNameWithoutExtension(localEpisode.Path));

            // Limit recovery to adjacent pairs in ordinary seasons; do not expand arbitrary packs.
            if (season == 0 || first == 0 || last != first + 1 ||
                grabbed == null || grabbed.SeasonNumber != season || !grabbed.EpisodeNumbers.SequenceEqual(new[] { first }) ||
                torrent == null || torrent.SeasonNumber != season || !torrent.EpisodeNumbers.SequenceEqual(new[] { first, last }) ||
                file == null || file.SeasonNumber != season || !file.EpisodeNumbers.SequenceEqual(new[] { last }) ||
                NormalizeSeparators(file.SeriesTitle ?? string.Empty) != NormalizeSeparators(torrent.SeriesTitle ?? string.Empty) ||
                file.IsAbsoluteNumbering || file.IsDaily || file.Special || file.FullSeason)
            {
                return false;
            }

            var mapped = _parsingService.GetEpisodes(torrent, localEpisode.Series, localEpisode.SceneSource);
            var mappedFile = _parsingService.GetEpisodes(file, localEpisode.Series, localEpisode.SceneSource);
            return mapped.Count == 2 && mapped.All(e => e.SeriesId == localEpisode.Series.Id && e.SeasonNumber == season) &&
                   mapped.Count(e => e.EpisodeNumber == first && e.Id == localEpisode.Release.EpisodeIds[0]) == 1 &&
                   mapped.Count(e => e.EpisodeNumber == last && e.Id == localEpisode.Episodes[0].Id) == 1 &&
                   mappedFile.Count == 1 && mappedFile[0].Id == localEpisode.Episodes[0].Id;
        }

        private static string NormalizeSeparators(string title)
        {
            return Regex.Replace(title, @"[\W_]+", " ").Trim().ToUpperInvariant();
        }

        private string FormatEpisode(List<Episode> episodes)
        {
            return string.Join(", ", episodes.Select(e => $"{e.SeasonNumber}x{e.EpisodeNumber:00}"));
        }
    }
}
