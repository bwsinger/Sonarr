using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Test.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.HistoryTests
{
    public class HistoryServiceFixture : CoreTest<HistoryService>
    {
        private QualityProfile _profile;
        private QualityProfile _profileCustom;

        [SetUp]
        public void Setup()
        {
            _profile = new QualityProfile
                {
                    Cutoff = Quality.WEBDL720p.Id,
                    Items = QualityFixture.GetDefaultQualities(),
                };

            _profileCustom = new QualityProfile
                {
                    Cutoff = Quality.WEBDL720p.Id,
                    Items = QualityFixture.GetDefaultQualities(Quality.DVD),
                };
        }

        [Test]
        public void should_use_file_name_for_source_title_if_scene_name_is_null()
        {
            var series = Builder<Series>.CreateNew().Build();
            var episodes = Builder<Episode>.CreateListOfSize(1).Build().ToList();
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                                                  .With(f => f.SceneName = null)
                                                  .Build();

            var localEpisode = new LocalEpisode
                               {
                                   Series = series,
                                   Episodes = episodes,
                                   Path = @"C:\Test\Unsorted\Series.s01e01.mkv"
                               };

            var downloadClientItem = new DownloadClientItem
                                     {
                                         DownloadClientInfo = new DownloadClientItemClientInfo
                                         {
                                             Protocol = DownloadProtocol.Usenet,
                                             Id = 1,
                                             Name = "sab"
                                         },
                                         DownloadId = "abcd"
                                     };

            Subject.Handle(new EpisodeImportedEvent(localEpisode, episodeFile, new List<DeletedEpisodeFile>(), true, downloadClientItem));

            Mocker.GetMock<IHistoryRepository>()
                .Verify(v => v.Insert(It.Is<EpisodeHistory>(h => h.SourceTitle == Path.GetFileNameWithoutExtension(localEpisode.Path))));
        }

        [TestCase("numbered-title", true)]
        [TestCase("special-title", true)]
        [TestCase("restored-episode-pair", true)]
        [TestCase(null, true)]
        [TestCase("numbered-title", false)]
        public void should_record_patch_attribution_only_for_new_download_imports(string fix, bool newDownload)
        {
            var localEpisode = new LocalEpisode
            {
                Series = Builder<Series>.CreateNew().Build(),
                Episodes = Builder<Episode>.CreateListOfSize(2).BuildList(),
                Path = "/downloads/Series.S01E01E02.mkv",
                DevImportFix = fix
            };
            var episodeFile = Builder<EpisodeFile>.CreateNew().Build();
            var rows = new List<EpisodeHistory>();
            Mocker.GetMock<IHistoryRepository>()
                .Setup(s => s.Insert(It.IsAny<EpisodeHistory>()))
                .Callback<EpisodeHistory>(h => rows.Add(h));

            Subject.Handle(new EpisodeImportedEvent(localEpisode, episodeFile, new List<DeletedEpisodeFile>(), newDownload, new DownloadClientItem { DownloadId = "hash" }));

            Assert.That(rows.Count, Is.EqualTo(newDownload ? 2 : 0));
            foreach (var row in rows)
            {
                Assert.That(row.Data["DevMetricsVersion"], Is.EqualTo("1"));
                Assert.That(row.Data.ContainsKey("DevImportFix"), Is.EqualTo(fix != null));
                Assert.That(row.DownloadId, Is.EqualTo("hash"));
                if (fix != null)
                {
                    Assert.That(row.Data["DevImportFix"], Is.EqualTo(fix));
                }
            }

            Assert.That(rows.Select(h => h.EpisodeId).Distinct().Count(), Is.EqualTo(rows.Count));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void should_record_only_custom_score_recovery_failures(bool preserveFiles)
        {
            Subject.Handle(new DownloadFailedEvent
            {
                EpisodeIds = new List<int> { 1, 2 },
                DownloadId = "hash",
                TrackedDownload = new TrackedDownload
                {
                    PreserveFilesOnFailure = preserveFiles,
                    DownloadItem = new DownloadClientItem { DownloadClientInfo = new DownloadClientItemClientInfo() }
                }
            });

            Mocker.GetMock<IHistoryRepository>().Verify(s => s.Insert(It.Is<EpisodeHistory>(h =>
                h.Data.ContainsKey("DevRecovery") == preserveFiles &&
                h.Data.ContainsKey("DevMetricsVersion") == preserveFiles &&
                !h.Data.ContainsKey("DevImportFix") && h.DownloadId == "hash")), Times.Exactly(2));
        }
    }
}
