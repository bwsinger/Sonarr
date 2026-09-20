using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
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
    }
}
