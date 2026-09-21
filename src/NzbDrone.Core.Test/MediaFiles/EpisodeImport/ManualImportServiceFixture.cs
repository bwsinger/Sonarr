using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport
{
    public class ManualImportServiceFixture : CoreTest<ManualImportService>
    {
        [Test]
        public void should_clear_automatic_parser_attribution_after_user_overrides()
        {
            var series = new Series { Id = 1, Path = "/library/Series" };
            var episodes = new List<Episode> { new Episode { Id = 2 } };
            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(1)).Returns(series);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodes(It.IsAny<List<int>>())).Returns(episodes);
            Mocker.GetMock<IAggregationService>()
                .Setup(s => s.Augment(It.IsAny<LocalEpisode>(), null))
                .Returns((LocalEpisode localEpisode, DownloadClientItem item) =>
                {
                    localEpisode.DevImportFix = "numbered-title";
                    return localEpisode;
                });
            Mocker.GetMock<IImportApprovedEpisodes>()
                .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto))
                .Returns(new List<ImportResult>());

            Subject.Execute(new ManualImportCommand
            {
                ImportMode = ImportMode.Auto,
                Files = new List<ManualImportFile>
                {
                    new ManualImportFile { Path = "/downloads/01. Episode.mkv", SeriesId = 1, EpisodeIds = new List<int> { 2 } }
                }
            });

            Mocker.GetMock<IImportApprovedEpisodes>().Verify(s => s.Import(
                It.Is<List<ImportDecision>>(decisions => decisions.Single().LocalEpisode.DevImportFix == null &&
                    decisions.Single().LocalEpisode.Episodes == episodes),
                true,
                null,
                ImportMode.Auto), Times.Once());
        }
    }
}
