using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class MatchesGrabSpecificationFixture : CoreTest<MatchesGrabSpecification>
    {
        private Episode _episode1;
        private Episode _episode2;
        private Episode _episode3;
        private LocalEpisode _localEpisode;
        private DownloadClientItem _downloadClientItem;

        [SetUp]
        public void Setup()
        {
            _episode1 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 1)
                .Build();

            _episode2 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 2)
                .Build();

            _episode3 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 3)
                .Build();

            _localEpisode = Builder<LocalEpisode>.CreateNew()
                                                 .With(l => l.Path = @"C:\Test\Unsorted\Series.Title.S01E01.720p.HDTV-Sonarr\S01E05.mkv".AsOsAgnostic())
                                                 .With(l => l.Episodes = new List<Episode> { _episode1 })
                                                 .With(l => l.Release = null)
                                                 .Build();

            _downloadClientItem = Builder<DownloadClientItem>.CreateNew().Build();
        }

        private void GivenHistoryForEpisodes(params Episode[] episodes)
        {
            if (episodes.Empty())
            {
                return;
            }

            var grabbedHistories = Builder<EpisodeHistory>.CreateListOfSize(episodes.Length)
                .All()
                .With(h => h.EventType == EpisodeHistoryEventType.Grabbed)
                .BuildList();

            for (var i = 0; i < grabbedHistories.Count; i++)
            {
                grabbedHistories[i].EpisodeId = episodes[i].Id;
            }

            _localEpisode.Release = new GrabbedReleaseInfo(grabbedHistories);
        }

        [Test]
        public void should_be_accepted_for_existing_file()
        {
            _localEpisode.ExistingFile = true;

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_accepted_if_no_download_client_item()
        {
            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_accepted_if_no_grabbed_release_info()
        {
            GivenHistoryForEpisodes();

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_accepted_if_file_episode_matches_single_grabbed_release_info()
        {
            GivenHistoryForEpisodes(_episode1);

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_accepted_if_file_episode_is_in_multi_episode_grabbed_release_info()
        {
            GivenHistoryForEpisodes(_episode1, _episode2);

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_rejected_if_file_episode_does_not_match_single_grabbed_release_info()
        {
            GivenHistoryForEpisodes(_episode2);

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_be_rejected_if_file_episode_is_not_in_multi_episode_grabbed_release_info()
        {
            GivenHistoryForEpisodes(_episode2, _episode3);

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeFalse();
        }

        private void GivenPunctuationStrippedPair(string title = "Stuart Fails To Save The Universe", int season = 1, int first = 7)
        {
            var series = new Series { Id = 160, Title = title, SeriesType = SeriesTypes.Standard };
            _episode1.SeriesId = series.Id;
            _episode1.SeasonNumber = season;
            _episode1.EpisodeNumber = first;
            _episode2.SeriesId = series.Id;
            _episode2.SeasonNumber = season;
            _episode2.EpisodeNumber = first + 1;
            _localEpisode.Series = series;
            _localEpisode.ExistingFile = false;
            _localEpisode.Episodes = new List<Episode> { _episode2 };
            _localEpisode.Path = $"/downloads/{title}.S{season:00}E{first + 1:00}.Episode.Title.2160p.WEB-DL-G66.mkv";
            var source = title.StartsWith("Star Trek") ? "ATV" : "HMAX";
            _downloadClientItem.Title = $"{title} S{season:00}E{first:00}-{first + 1:00} 2160p {source} WEB-DL ITA ENG DDP5 1 DV HDR H 265-G66";
            _downloadClientItem.DownloadClientInfo = new DownloadClientItemClientInfo { Protocol = DownloadProtocol.Torrent };
            GivenHistoryForEpisodes(_episode1);
            _localEpisode.Release.Title = _downloadClientItem.Title.Replace("-", " ");
            Mocker.GetMock<IParsingService>()
                .Setup(s => s.GetEpisodes(It.IsAny<ParsedEpisodeInfo>(), series, It.IsAny<bool>(), null))
                .Returns((ParsedEpisodeInfo info, Series _, bool scene, NzbDrone.Core.IndexerSearch.Definitions.SearchCriteriaBase criteria) =>
                    new[] { _episode1, _episode2 }.Where(e => info.EpisodeNumbers.Contains(e.EpisodeNumber)).ToList());
        }

        [TestCase("Stuart Fails To Save The Universe", 1, 7, "Stuart.Fails.to.Save.the.Universe.S01E08.Spoiler.Were.as.Confused.as.You.Are.2160p.HMAX.WEB-DL.ITA.ENG.DDP5.1.DV.HDR.H.265-G66.mkv")]
        [TestCase("Star Trek Strange New Worlds", 4, 4, "Star.Trek.Strange.New.Worlds.S04E05.Level-Five.Transporter.Accident.2160p.ATV.WEB-DL.ITA.ENG.DDP5.1.DV.HDR.H.265-G66.mkv")]
        [TestCase("Star Trek Strange New Worlds", 4, 6, "Star.Trek.Strange.New.Worlds.S04E07.Like.Chronitons.Through.the.Hourglass.2160p.ATV.WEB-DL.ITA.ENG.DDP5.1.DV.HDR.H.265-G66.mkv")]
        public void should_confirm_lost_range_separator_from_torrent_and_file(string title, int season, int first, string filename)
        {
            GivenPunctuationStrippedPair(title, season, first);
            _localEpisode.Path = "/downloads/" + filename;

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
            _localEpisode.Release.EpisodeIds.Should().Equal(_episode1.Id);
        }

        [TestCase("different_title")]
        [TestCase("different_quality")]
        [TestCase("no_range_separator")]
        [TestCase("usenet")]
        [TestCase("no_download_id")]
        [TestCase("scene_numbering")]
        [TestCase("anime")]
        [TestCase("wrong_file")]
        [TestCase("different_file_series")]
        [TestCase("unparseable_file")]
        [TestCase("wrong_series")]
        [TestCase("alias_season_remap")]
        [TestCase("wrong_grabbed_id")]
        [TestCase("not_adjacent")]
        public void should_not_expand_grabbed_episodes_without_matching_evidence(string mismatch)
        {
            GivenPunctuationStrippedPair();
            switch (mismatch)
            {
                case "different_title": _downloadClientItem.Title = _downloadClientItem.Title.Replace("Stuart", "Sherlock"); break;
                case "different_quality": _downloadClientItem.Title = _downloadClientItem.Title.Replace("2160p", "1080p"); break;
                case "no_range_separator": _downloadClientItem.Title = _localEpisode.Release.Title; break;
                case "usenet": _downloadClientItem.DownloadClientInfo.Protocol = DownloadProtocol.Usenet; break;
                case "no_download_id": _downloadClientItem.DownloadId = null; break;
                case "scene_numbering": _localEpisode.Series.UseSceneNumbering = true; break;
                case "anime": _localEpisode.Series.SeriesType = SeriesTypes.Anime; break;
                case "different_file_series": _localEpisode.Path = "/downloads/Sherlock.S01E08.mkv"; break;
                case "wrong_file": _localEpisode.Path = "/downloads/Stuart.S01E09.mkv"; break;
                case "unparseable_file": _localEpisode.Path = "/downloads/08.mkv"; break;
                case "wrong_series": _episode2.SeriesId++; break;
                case "alias_season_remap": _episode2.SeasonNumber++; break;
                case "wrong_grabbed_id": _localEpisode.Release.EpisodeIds = new List<int> { 999 }; break;
                case "not_adjacent":
                    _downloadClientItem.Title = _downloadClientItem.Title.Replace("07-08", "07-09");
                    _localEpisode.Release.Title = _localEpisode.Release.Title.Replace("07 08", "07 09");
                    break;
            }

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_not_treat_bare_numeric_episode_title_as_a_range()
        {
            Parser.Parser.ParseTitle("Doctor Who S03E07 42 1080p WEB-DL.mkv").EpisodeNumbers.Should().Equal(7);
        }
    }
}
