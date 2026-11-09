using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class ConflictingEpisodeNumberSpecificationFixture : CoreTest<ConflictingEpisodeNumberSpecification>
    {
        private LocalEpisode Episode(string name)
        {
            return new LocalEpisode { Path = name, Series = new Series { SeriesType = SeriesTypes.Standard } };
        }

        [TestCase("Jentry Chau vs. the Underworld - S01E01 - Episode 10.mkv", false)]
        [TestCase("Jentry Chau vs. the Underworld - S01E01 - Episode 2.mkv", false)]
        [TestCase("Jentry Chau vs. the Underworld - S01E01 - Episode 1.mkv", true)]
        [TestCase("Show.s01e01.Episode.02.mkv", false)]
        [TestCase("Show - S01E10 - Episode 10.mkv", true)]
        [TestCase("Show - S01E01 - Episode 10 Years Later.mkv", true)]
        [TestCase("01. The Hedge Knight.mkv", true)]
        [TestCase("Show - S01E01-E02 - Episode 2.mkv", true)]
        public void should_require_review_only_for_explicit_conflicting_numbered_title(string name, bool accepted)
        {
            Subject.IsSatisfiedBy(Episode(name), new DownloadClientItem()).Accepted.Should().Be(accepted);
        }

        [Test]
        public void should_not_change_existing_library_or_untracked_files()
        {
            var episode = Episode("Show - S01E01 - Episode 10.mkv");
            Subject.IsSatisfiedBy(episode, null).Accepted.Should().BeTrue();
            episode.ExistingFile = true;
            Subject.IsSatisfiedBy(episode, new DownloadClientItem()).Accepted.Should().BeTrue();
        }

        [TestCase(SeriesTypes.Anime)]
        [TestCase(SeriesTypes.Daily)]
        public void should_not_interpret_other_numbering_schemes(SeriesTypes type)
        {
            var episode = Episode("Show - S01E01 - Episode 10.mkv");
            episode.Series.SeriesType = type;
            Subject.IsSatisfiedBy(episode, new DownloadClientItem()).Accepted.Should().BeTrue();
        }

        private LocalEpisode MultipartEpisode(string name = "Star Trek TNG S07E24-E25 - All Good Things 1080P x264 RB58.mp4")
        {
            var season = new List<Episode>
            {
                new Episode { Id = 24, SeriesId = 174, SeasonNumber = 7, EpisodeNumber = 24, Title = "Preemptive Strike" },
                new Episode { Id = 25, SeriesId = 174, SeasonNumber = 7, EpisodeNumber = 25, Title = "All Good Things... (1)" },
                new Episode { Id = 26, SeriesId = 174, SeasonNumber = 7, EpisodeNumber = 26, Title = "All Good Things... (2)" }
            };
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodesBySeason(174, 7)).Returns(season);
            var info = Parser.Parser.ParseTitle(name);
            return new LocalEpisode
            {
                Path = name,
                Series = new Series { Id = 174, SeriesType = SeriesTypes.Standard },
                FileEpisodeInfo = info,
                Episodes = season.Where(e => info.EpisodeNumbers.Contains(e.EpisodeNumber)).ToList()
            };
        }

        [TestCase("Star Trek TNG S07E24-E25 - All Good Things 1080P x264 RB58.mp4")]
        [TestCase("Star.Trek.TNG.S07E24-25.All.Good.Things.1080p.BluRay.x264-RB58.mkv")]
        [TestCase("Star Trek TNG S07E24-E25 - All Good Things.mp4")]
        [TestCase("Star.Trek.TNG.S07E24&25.All.Good.Things.mkv")]
        [TestCase("Star Trek TNG S07E24 & E25 - All Good Things.mkv")]
        [TestCase("Star.Trek.TNG.S07E25&24.All.Good.Things.mkv")]
        public void should_require_review_for_numbered_range_conflicting_with_unique_multipart_title(string name)
        {
            var episode = MultipartEpisode(name);
            var decision = Subject.IsSatisfiedBy(episode, new DownloadClientItem());

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.ConflictingEpisodeNumber);
            decision.Message.Should().Contain("S07E24-E25").And.Contain("S07E25-E26").And.Contain("manually");
            episode.Episodes.Select(e => e.Id).Should().Equal(24, 25);
            episode.DevImportFix.Should().BeNull();
        }

        [TestCase("Star Trek TNG S07E25-E26 - All Good Things 1080P x264 RB58.mp4")]
        [TestCase("Star Trek TNG S07E24 - Preemptive Strike 1080P x264 RB58.mp4")]
        [TestCase("Star Trek TNG S07E24-E25 - Unknown Title 1080P x264 RB58.mp4")]
        [TestCase("Star Trek TNG S07E24-E25 - All Good Things Return 1080P x264 RB58.mp4")]
        [TestCase("Star Trek TNG S07E24-E25 - All Good Things Part 3 1080P x264 RB58.mp4")]
        [TestCase("Star Trek TNG S07E24-E25 - All Good Things (1) 1080P x264 RB58.mp4")]
        [TestCase("Star Trek TNG S07E24-E25 - Good Things 1080P x264 RB58.mp4")]
        [TestCase("Star Trek TNG S07E24-E26 - All Good Things 1080P x264 RB58.mp4")]
        [TestCase("Star.Trek.TNG.S07E25&26.All.Good.Things.mkv")]
        [TestCase("Star.Trek.TNG.S07E26&25.All.Good.Things.mkv")]
        public void should_preserve_valid_or_unrecognized_titles(string name)
        {
            Subject.IsSatisfiedBy(MultipartEpisode(name), new DownloadClientItem()).Accepted.Should().BeTrue();
        }

        [TestCase("library")]
        [TestCase("manual")]
        [TestCase("scene")]
        [TestCase("anime")]
        [TestCase("daily")]
        [TestCase("special")]
        [TestCase("other-season")]
        [TestCase("multi-season")]
        [TestCase("absolute")]
        [TestCase("scene-alias")]
        public void should_preserve_unsupported_contexts_and_manual_overrides(string context)
        {
            var episode = MultipartEpisode();
            switch (context)
            {
                case "library": episode.ExistingFile = true; break;
                case "scene": episode.Series.UseSceneNumbering = true; break;
                case "anime": episode.Series.SeriesType = SeriesTypes.Anime; break;
                case "daily": episode.Series.SeriesType = SeriesTypes.Daily; break;
                case "special": episode.FileEpisodeInfo.Special = true; break;
                case "other-season": episode.FolderEpisodeInfo = new ParsedEpisodeInfo { SeasonNumber = 8, FullSeason = true }; break;
                case "multi-season": episode.FileEpisodeInfo.IsMultiSeason = true; break;
                case "absolute": episode.FileEpisodeInfo.AbsoluteEpisodeNumbers = new[] { 24, 25 }; break;
                case "scene-alias": episode.Episodes.ForEach(e => e.SeasonNumber = 8); break;
            }

            Subject.IsSatisfiedBy(episode, context == "manual" ? null : new DownloadClientItem()).Accepted.Should().BeTrue();
        }

        [TestCase("duplicate")]
        [TestCase("third-part")]
        [TestCase("plain-title")]
        [TestCase("missing-part")]
        [TestCase("nonconsecutive")]
        public void should_not_infer_pair_from_ambiguous_metadata(string context)
        {
            var episode = MultipartEpisode();
            var season = Mocker.GetMock<IEpisodeService>().Object.GetEpisodesBySeason(174, 7);
            switch (context)
            {
                case "duplicate": season.Add(new Episode { Title = "All Good Things... (1)" }); break;
                case "third-part": season.Add(new Episode { Title = "All Good Things... (3)" }); break;
                case "plain-title": season.Add(new Episode { Title = "All Good Things" }); break;
                case "missing-part": season.RemoveAt(2); break;
                case "nonconsecutive": season[2].EpisodeNumber = 28; break;
            }

            Subject.IsSatisfiedBy(episode, new DownloadClientItem()).Accepted.Should().BeTrue();
        }
    }
}
