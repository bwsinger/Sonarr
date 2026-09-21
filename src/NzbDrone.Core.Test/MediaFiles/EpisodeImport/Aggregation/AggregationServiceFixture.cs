using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation.Aggregators;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Aggregation
{
    [TestFixture]
    public class AggregationServiceFixture : CoreTest<AggregationService>
    {
        private LocalEpisode _localEpisode;
        private Episode _special;

        [SetUp]
        public void Setup()
        {
            _localEpisode = new LocalEpisode
            {
                Path = "/downloads/Sherlock The Abominable Bride (2016) 1080p BluRay H264 DolbyD 5.1 + nickarad.mp4",
                Series = new Series { Id = 126, Title = "Sherlock", SeriesType = SeriesTypes.Standard },
                SceneSource = true
            };
            _special = new Episode { Id = 5492, SeriesId = 126, SeasonNumber = 0, EpisodeNumber = 9, Title = "The Abominable Bride" };

            var parsingService = Mocker.Resolve<ParsingService>();
            Mocker.SetConstant<IParsingService>(parsingService);
            Mocker.SetConstant<IEnumerable<IAggregateLocalEpisode>>(new[]
            {
                new AggregateEpisodes(parsingService, Mocker.GetMock<IEpisodeService>().Object)
            });

            var specialTitle = Path.GetFileNameWithoutExtension(_localEpisode.Path);
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.FindEpisodeByTitle(126, 0, specialTitle))
                .Returns(_special);
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.FindEpisode(126, 0, 9))
                .Returns(_special);
        }

        [Test]
        public void should_map_known_special_when_all_numbered_parse_results_are_null()
        {
            Parser.Parser.ParsePath(_localEpisode.Path).Should().BeNull();

            Subject.Augment(_localEpisode, null);

            _localEpisode.DevImportFix.Should().Be("special-title");
            _localEpisode.FileEpisodeInfo.Special.Should().BeTrue();
            _localEpisode.FileEpisodeInfo.SeasonNumber.Should().Be(0);
            _localEpisode.Episodes.Should().ContainSingle().Which.Should().BeSameAs(_special);
        }

        [Test]
        public void should_reject_unknown_title()
        {
            _localEpisode.Path = "/downloads/Sherlock Unknown Adventure.mp4";

            Action augment = () => Subject.Augment(_localEpisode, null);

            augment.Should().Throw<AugmentingFailedException>();
            _localEpisode.Episodes.Should().BeEmpty();
        }

        [Test]
        public void should_reject_unparsed_file_without_known_series()
        {
            _localEpisode.Series = null;

            Action augment = () => Subject.Augment(_localEpisode, null);

            augment.Should().Throw<AugmentingFailedException>();
        }

        [Test]
        public void should_not_assign_special_when_episode_mapping_fails()
        {
            Mocker.GetMock<IEpisodeService>().Setup(s => s.FindEpisode(126, 0, 9)).Returns((Episode)null);

            Subject.Augment(_localEpisode, null);

            _localEpisode.Episodes.Should().BeEmpty();
        }

        [Test]
        public void should_preserve_explicit_episode_numbering()
        {
            _localEpisode.FileEpisodeInfo = Parser.Parser.ParseTitle("Sherlock.S01E01");
            var episode = new Episode { SeriesId = 126, SeasonNumber = 1, EpisodeNumber = 1 };
            Mocker.GetMock<IEpisodeService>().Setup(s => s.FindEpisode(126, 1, 1)).Returns(episode);

            Subject.Augment(_localEpisode, null);

            _localEpisode.DevImportFix.Should().BeNull();
            _localEpisode.Episodes.Should().ContainSingle().Which.Should().BeSameAs(episode);
            Mocker.GetMock<IEpisodeService>()
                .Verify(s => s.FindEpisodeByTitle(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never());
        }
    }
}
