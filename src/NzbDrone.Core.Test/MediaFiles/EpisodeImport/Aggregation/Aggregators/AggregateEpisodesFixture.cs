using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    [TestFixture]
    public class AugmentEpisodesFixture : CoreTest<AggregateEpisodes>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew().Build();

            var augmenters = new List<Mock<IAggregateLocalEpisode>>
                             {
                                 new Mock<IAggregateLocalEpisode>()
                             };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetEpisodes(It.IsAny<ParsedEpisodeInfo>(), _series, It.IsAny<bool>(), null))
                  .Returns(Builder<Episode>.CreateListOfSize(1).BuildList());

            Mocker.SetConstant(augmenters.Select(c => c.Object));
        }

        [Test]
        public void should_not_use_folder_for_full_season()
        {
            var fileEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01E01");
            var folderEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01");
            var localEpisode = new LocalEpisode
                               {
                                   FileEpisodeInfo = fileEpisodeInfo,
                                   FolderEpisodeInfo = folderEpisodeInfo,
                                   Path = @"C:\Test\Unsorted TV\Series.Title.S01\Series.Title.S01E01.mkv".AsOsAgnostic(),
                                   Series = _series
                               };

            Subject.Aggregate(localEpisode, null);

            Mocker.GetMock<IParsingService>()
                  .Verify(v => v.GetEpisodes(fileEpisodeInfo, _series, localEpisode.SceneSource, null), Times.Once());
        }

        [Test]
        public void should_not_use_folder_when_it_contains_more_than_one_valid_video_file()
        {
            var fileEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01E01");
            var folderEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01");
            var localEpisode = new LocalEpisode
            {
                FileEpisodeInfo = fileEpisodeInfo,
                FolderEpisodeInfo = folderEpisodeInfo,
                Path = @"C:\Test\Unsorted TV\Series.Title.S01\Series.Title.S01E01.mkv".AsOsAgnostic(),
                Series = _series,
                OtherVideoFiles = true
            };

            Subject.Aggregate(localEpisode, null);

            Mocker.GetMock<IParsingService>()
                  .Verify(v => v.GetEpisodes(fileEpisodeInfo, _series, localEpisode.SceneSource, null), Times.Once());
        }

        [Test]
        public void should_not_use_folder_name_if_file_name_is_scene_name()
        {
            var fileEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01E01");
            var folderEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01E01");
            var localEpisode = new LocalEpisode
            {
                FileEpisodeInfo = fileEpisodeInfo,
                FolderEpisodeInfo = folderEpisodeInfo,
                Path = @"C:\Test\Unsorted TV\Series.Title.S01E01\Series.Title.S01E01.720p.HDTV-Sonarr.mkv".AsOsAgnostic(),
                Series = _series
            };

            Subject.Aggregate(localEpisode, null);

            Mocker.GetMock<IParsingService>()
                  .Verify(v => v.GetEpisodes(fileEpisodeInfo, _series, localEpisode.SceneSource, null), Times.Once());
        }

        [Test]
        public void should_use_folder_when_only_one_video_file()
        {
            var fileEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01E01");
            var folderEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01E01");
            var localEpisode = new LocalEpisode
            {
                FileEpisodeInfo = fileEpisodeInfo,
                FolderEpisodeInfo = folderEpisodeInfo,
                Path = @"C:\Test\Unsorted TV\Series.Title.S01E01\Series.Title.S01E01.mkv".AsOsAgnostic(),
                Series = _series
            };

            Subject.Aggregate(localEpisode, null);

            Mocker.GetMock<IParsingService>()
                  .Verify(v => v.GetEpisodes(folderEpisodeInfo, _series, localEpisode.SceneSource, null), Times.Once());
        }

        [Test]
        public void should_use_file_when_folder_is_absolute_and_file_is_not()
        {
            var fileEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01E01");
            var folderEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.01");
            var localEpisode = new LocalEpisode
                               {
                                   FileEpisodeInfo = fileEpisodeInfo,
                                   FolderEpisodeInfo = folderEpisodeInfo,
                                   Path = @"C:\Test\Unsorted TV\Series.Title.101\Series.Title.S01E01.mkv".AsOsAgnostic(),
                                   Series = _series
                               };

            Subject.Aggregate(localEpisode, null);

            Mocker.GetMock<IParsingService>()
                  .Verify(v => v.GetEpisodes(fileEpisodeInfo, _series, localEpisode.SceneSource, null), Times.Once());
        }

        [Test]
        public void should_use_special_info_when_not_null()
        {
            var fileEpisodeInfo = Parser.Parser.ParseTitle("S00E01");
            var specialEpisodeInfo = fileEpisodeInfo.JsonClone();

            var localEpisode = new LocalEpisode
                               {
                                   FileEpisodeInfo = fileEpisodeInfo,
                                   Path = @"C:\Test\TV\Series\Specials\S00E01.mkv".AsOsAgnostic(),
                                   Series = _series
                               };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetEpisodes(fileEpisodeInfo, _series, It.IsAny<bool>(), null))
                  .Returns(new List<Episode>());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.ParseSpecialEpisodeTitle(fileEpisodeInfo, It.IsAny<string>(), _series))
                  .Returns(specialEpisodeInfo);

            Subject.Aggregate(localEpisode, null);

            Mocker.GetMock<IParsingService>()
                  .Verify(v => v.GetEpisodes(specialEpisodeInfo, _series, localEpisode.SceneSource, null), Times.Once());
        }

        private LocalEpisode NumberedTitleEpisode(string fileName = "01. The Hedge Knight.mkv")
        {
            _series.SeriesType = SeriesTypes.Standard;
            _series.UseSceneNumbering = false;

            var episodes = new List<Episode>
            {
                new Episode { Id = 1, SeasonNumber = 1, EpisodeNumber = 1, Title = "The Hedge Knight" },
                new Episode { Id = 2, SeasonNumber = 1, EpisodeNumber = 2, Title = "Hard Salt Beef" }
            };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                  .Returns(episodes);
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetEpisodes(It.Is<ParsedEpisodeInfo>(info => !info.FullSeason), _series, It.IsAny<bool>(), null))
                  .Returns<ParsedEpisodeInfo, Series, bool, SearchCriteriaBase>(
                      (info, series, sceneSource, criteria) => episodes.Where(e => info.EpisodeNumbers.Contains(e.EpisodeNumber)).ToList());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetEpisodes(It.Is<ParsedEpisodeInfo>(info => info.FullSeason), _series, It.IsAny<bool>(), null))
                  .Returns(new List<Episode>());

            var path = (@"C:\Downloads\A.Knight.of.the.Seven.Kingdoms.S01.1080p.WEB-DL\" + fileName).AsOsAgnostic();

            return new LocalEpisode
            {
                Series = _series,
                Path = path,
                FileEpisodeInfo = Parser.Parser.ParsePath(path),
                FolderEpisodeInfo = Parser.Parser.ParseTitle("A.Knight.of.the.Seven.Kingdoms.S01.1080p.WEB-DL"),
                DownloadClientEpisodeInfo = Parser.Parser.ParseTitle("A.Knight.of.the.Seven.Kingdoms.S01.1080p.WEB-DL"),
                OtherVideoFiles = true,
                SceneSource = true
            };
        }

        [TestCase("01. The Hedge Knight.mkv", 1)]
        [TestCase("02. Hard Salt Beef.mkv", 2)]
        [TestCase("01 - the_hedge_knight.mkv", 1)]
        public void should_match_number_and_unique_title_in_known_season(string fileName, int number)
        {
            var localEpisode = NumberedTitleEpisode(fileName);
            var originalInfo = localEpisode.FileEpisodeInfo;

            Subject.Aggregate(localEpisode, null);

            Assert.That(localEpisode.Episodes.Select(e => e.EpisodeNumber), Is.EqualTo(new[] { number }));
            Assert.That(localEpisode.DevImportFix, Is.EqualTo("numbered-title"));
            Assert.That(localEpisode.FileEpisodeInfo.FullSeason, Is.False);
            Assert.That(localEpisode.FileEpisodeInfo.EpisodeNumbers, Is.EqualTo(new[] { number }));
            Assert.That(localEpisode.FileEpisodeInfo.Quality, Is.EqualTo(originalInfo.Quality));
            Assert.That(originalInfo.FullSeason, Is.True, "Do not mutate shared folder/download parsing results");
            Mocker.GetMock<IParsingService>().Verify(s => s.GetEpisodes(localEpisode.FileEpisodeInfo, _series, localEpisode.SceneSource, null), Times.Once());
        }

        [Test]
        public void should_match_when_file_parse_is_missing()
        {
            var localEpisode = NumberedTitleEpisode();
            localEpisode.FileEpisodeInfo = null;

            Subject.Aggregate(localEpisode, null);

            Assert.That(localEpisode.Episodes.Single().Id, Is.EqualTo(1));
            Assert.That(localEpisode.FileEpisodeInfo.EpisodeNumbers, Is.EqualTo(new[] { 1 }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void should_not_claim_numbered_title_when_upstream_context_maps_same_episode(bool clientContext)
        {
            var localEpisode = NumberedTitleEpisode();
            localEpisode.OtherVideoFiles = false;
            var context = Parser.Parser.ParseTitle("A.Knight.of.the.Seven.Kingdoms.S01E01.1080p.WEB-DL");
            if (clientContext)
            {
                localEpisode.DownloadClientEpisodeInfo = context;
            }
            else
            {
                localEpisode.FolderEpisodeInfo = context;
            }

            Subject.Aggregate(localEpisode, null);

            Assert.That(localEpisode.Episodes.Single().Id, Is.EqualTo(1));
            Assert.That(localEpisode.DevImportFix, Is.Null);
        }

        [Test]
        public void should_preserve_numbered_title_import_without_credit_when_baseline_evaluation_fails()
        {
            var localEpisode = NumberedTitleEpisode();
            localEpisode.OtherVideoFiles = false;
            localEpisode.DownloadClientEpisodeInfo = Parser.Parser.ParseTitle("A.Knight.of.the.Seven.Kingdoms.S01E01.1080p.WEB-DL");
            Mocker.GetMock<IParsingService>()
                .Setup(s => s.GetEpisodes(localEpisode.DownloadClientEpisodeInfo, _series, localEpisode.SceneSource, null))
                .Throws(new InvalidOperationException("Baseline unavailable"));

            Subject.Aggregate(localEpisode, null);

            Assert.That(localEpisode.Episodes.Single().Id, Is.EqualTo(1));
            Assert.That(localEpisode.FileEpisodeInfo.EpisodeNumbers, Is.EqualTo(new[] { 1 }));
            Assert.That(localEpisode.DevImportFix, Is.Null);
        }

        [TestCase("02. The Hedge Knight.mkv")]
        [TestCase("01. Unknown Title.mkv")]
        [TestCase("01. Hedge Knight.mkv")]
        [TestCase("01. The Hedge Knight Part 2.mkv")]
        [TestCase("01. The Hedge Knight (2).mkv")]
        [TestCase("00. The Hedge Knight.mkv")]
        [TestCase("The Hedge Knight.mkv")]
        [TestCase("01. The Hedge Knight.srt")]
        public void should_not_guess_from_number_or_title_alone(string fileName)
        {
            var localEpisode = NumberedTitleEpisode(fileName);
            var originalInfo = localEpisode.FileEpisodeInfo;

            Subject.Aggregate(localEpisode, null);

            Assert.That(localEpisode.FileEpisodeInfo, Is.SameAs(originalInfo));
        }

        [TestCase("conflicting_season")]
        [TestCase("conflicting_episode")]
        [TestCase("multi_season")]
        [TestCase("special")]
        [TestCase("anime")]
        [TestCase("daily")]
        [TestCase("scene_numbering")]
        [TestCase("no_season")]
        [TestCase("already_parsed")]
        public void should_not_override_ambiguous_or_unsupported_context(string reason)
        {
            var localEpisode = NumberedTitleEpisode();

            switch (reason)
            {
                case "conflicting_season": localEpisode.DownloadClientEpisodeInfo.SeasonNumber = 2; break;
                case "conflicting_episode": localEpisode.DownloadClientEpisodeInfo.EpisodeNumbers = new[] { 2 }; break;
                case "multi_season": localEpisode.DownloadClientEpisodeInfo.IsMultiSeason = true; break;
                case "special": localEpisode.FolderEpisodeInfo.SeasonNumber = 0; break;
                case "anime": _series.SeriesType = SeriesTypes.Anime; break;
                case "daily": _series.SeriesType = SeriesTypes.Daily; break;
                case "scene_numbering": _series.UseSceneNumbering = true; break;
                case "no_season":
                    localEpisode.FileEpisodeInfo = null;
                    localEpisode.FolderEpisodeInfo = null;
                    localEpisode.DownloadClientEpisodeInfo = null;
                    break;
                case "already_parsed": localEpisode.FileEpisodeInfo = Parser.Parser.ParseTitle("Series.Title.S01E02"); break;
            }

            var originalInfo = localEpisode.FileEpisodeInfo;
            Subject.Aggregate(localEpisode, null);

            Assert.That(localEpisode.FileEpisodeInfo, Is.SameAs(originalInfo));
        }

        [Test]
        public void should_not_match_duplicate_normalized_titles()
        {
            var localEpisode = NumberedTitleEpisode();
            var originalInfo = localEpisode.FileEpisodeInfo;
            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                  .Returns(new List<Episode>
                  {
                      new Episode { EpisodeNumber = 1, Title = "The Hedge Knight" },
                      new Episode { EpisodeNumber = 2, Title = "The-Hedge-Knight" }
                  });

            Subject.Aggregate(localEpisode, null);

            Assert.That(localEpisode.FileEpisodeInfo, Is.SameAs(originalInfo));
        }

        [Test]
        public void should_not_bypass_scene_alias_season_mapping_when_scene_numbering_is_disabled()
        {
            var localEpisode = NumberedTitleEpisode();
            var originalInfo = localEpisode.FileEpisodeInfo;
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetEpisodes(It.Is<ParsedEpisodeInfo>(info => !info.FullSeason), _series, true, null))
                  .Returns(new List<Episode>
                  {
                      new Episode { Id = 3, SeasonNumber = 2, EpisodeNumber = 1, Title = "The Hedge Knight" }
                  });

            Subject.Aggregate(localEpisode, null);

            Assert.That(_series.UseSceneNumbering, Is.False);
            Assert.That(localEpisode.FileEpisodeInfo, Is.SameAs(originalInfo));
            Assert.That(localEpisode.FileEpisodeInfo.FullSeason, Is.True);
            Mocker.GetMock<IParsingService>()
                  .Verify(s => s.GetEpisodes(It.Is<ParsedEpisodeInfo>(info => !info.FullSeason && info.EpisodeNumbers.Single() == 1), _series, true, null), Times.Once());
        }
    }
}
