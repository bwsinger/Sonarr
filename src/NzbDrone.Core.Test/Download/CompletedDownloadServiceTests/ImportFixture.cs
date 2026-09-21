using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.CompletedDownloadServiceTests
{
    [TestFixture]
    public class ImportFixture : CoreTest<CompletedDownloadService>
    {
        private TrackedDownload _trackedDownload;
        private Episode _episode1;
        private Episode _episode2;
        private Episode _episode3;

        [SetUp]
        public void Setup()
        {
            _episode1 = new Episode { Id = 1, SeasonNumber = 1, EpisodeNumber = 1 };
            _episode2 = new Episode { Id = 2, SeasonNumber = 1, EpisodeNumber = 2 };
            _episode3 = new Episode { Id = 2, SeasonNumber = 1, EpisodeNumber = 3 };

            var completed = Builder<DownloadClientItem>.CreateNew()
                                                    .With(h => h.Status = DownloadItemStatus.Completed)
                                                    .With(h => h.OutputPath = new OsPath(@"C:\DropFolder\MyDownload".AsOsAgnostic()))
                                                    .With(h => h.Title = "Drone.S01E01.HDTV")
                                                    .Build();

            var remoteEpisode = BuildRemoteEpisode();

            _trackedDownload = Builder<TrackedDownload>.CreateNew()
                    .With(c => c.State = TrackedDownloadState.Downloading)
                    .With(c => c.DownloadItem = completed)
                    .With(c => c.RemoteEpisode = remoteEpisode)
                    .Build();

            Mocker.GetMock<IDownloadClient>()
              .SetupGet(c => c.Definition)
              .Returns(new DownloadClientDefinition { Id = 1, Name = "testClient" });

            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(c => c.Get(It.IsAny<int>()))
                  .Returns(Mocker.GetMock<IDownloadClient>().Object);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.MostRecentForDownloadId(_trackedDownload.DownloadItem.DownloadId))
                  .Returns(new EpisodeHistory());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetSeries("Drone.S01E01.HDTV"))
                  .Returns(remoteEpisode.Series);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            Mocker.GetMock<IProvideImportItemService>()
                  .Setup(s => s.ProvideImportItem(It.IsAny<DownloadClientItem>(), It.IsAny<DownloadClientItem>()))
                  .Returns<DownloadClientItem, DownloadClientItem>((i, p) => i);

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>()))
                .Returns(new List<Episode>());
        }

        private RemoteEpisode BuildRemoteEpisode()
        {
            return new RemoteEpisode
            {
                Series = new Series(),
                Episodes = new List<Episode>
                {
                    _episode1
                }
            };
        }

        private void GivenABadlyNamedDownload()
        {
            _trackedDownload.DownloadItem.DownloadId = "1234";
            _trackedDownload.DownloadItem.Title = "Droned Pilot"; // Set a badly named download
            Mocker.GetMock<IHistoryService>()
               .Setup(s => s.MostRecentForDownloadId(It.Is<string>(i => i == "1234")))
               .Returns(new EpisodeHistory() { SourceTitle = "Droned S01E01" });

            Mocker.GetMock<IParsingService>()
               .Setup(s => s.GetSeries(It.IsAny<string>()))
               .Returns((Series)null);

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.GetSeries("Droned S01E01"))
                .Returns(BuildRemoteEpisode().Series);
        }

        private void GivenSeriesMatch()
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetSeries(It.IsAny<string>()))
                  .Returns(_trackedDownload.RemoteEpisode.Series);
        }

        [Test]
        public void should_not_mark_as_imported_if_all_files_were_rejected()
        {
            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(
                                   new ImportDecision(
                                       new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv", Episodes = { _episode1 } }, new ImportRejection(ImportRejectionReason.Unknown, "Rejected!")), "Test Failure"),

                               new ImportResult(
                                   new ImportDecision(
                                       new LocalEpisode { Path = @"C:\TestPath\Droned.S01E02.mkv", Episodes = { _episode2 } }, new ImportRejection(ImportRejectionReason.Unknown, "Rejected!")), "Test Failure")
                           });

            Subject.Import(_trackedDownload);

            Mocker.GetMock<IEventAggregator>()
                .Verify(v => v.PublishEvent<DownloadCompletedEvent>(It.IsAny<DownloadCompletedEvent>()), Times.Never());

            AssertNotImported();
        }

        [Test]
        public void should_not_mark_as_imported_if_no_episodes_were_parsed()
        {
            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(
                                   new ImportDecision(
                                       new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv", Episodes = { _episode1 } }, new ImportRejection(ImportRejectionReason.Unknown, "Rejected!")), "Test Failure"),

                               new ImportResult(
                                   new ImportDecision(
                                       new LocalEpisode { Path = @"C:\TestPath\Droned.S01E02.mkv", Episodes = { _episode2 } }, new ImportRejection(ImportRejectionReason.Unknown, "Rejected!")), "Test Failure")
                           });

            _trackedDownload.RemoteEpisode.Episodes.Clear();

            Subject.Import(_trackedDownload);

            AssertNotImported();
        }

        [Test]
        public void should_not_mark_as_imported_if_all_files_were_skipped()
        {
            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv", Episodes = { _episode1 } }), "Test Failure"),
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E02.mkv", Episodes = { _episode2 } }), "Test Failure")
                           });

            Subject.Import(_trackedDownload);

            AssertNotImported();
        }

        [Test]
        public void should_not_mark_as_imported_if_some_of_episodes_were_not_imported()
        {
            _trackedDownload.RemoteEpisode.Episodes = new List<Episode>
            {
                new Episode(),
                new Episode(),
                new Episode()
            };

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv" })),
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv" }), "Test Failure"),
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv" }), "Test Failure")
                           });

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            Subject.Import(_trackedDownload);

            AssertNotImported();
        }

        [Test]
        public void should_not_mark_as_imported_if_some_of_episodes_were_not_imported_including_history()
        {
            _trackedDownload.RemoteEpisode.Episodes = new List<Episode>
                                                      {
                                                          new Episode(),
                                                          new Episode(),
                                                          new Episode()
                                                      };

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv" })),
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv" }), "Test Failure"),
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv" }), "Test Failure")
                           });

            var history = Builder<EpisodeHistory>.CreateListOfSize(2)
                                                  .BuildList();

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(history);

            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(_trackedDownload, history))
                  .Returns(true);

            Subject.Import(_trackedDownload);

            AssertNotImported();
        }

        [Test]
        public void should_mark_as_imported_if_all_episodes_were_imported()
        {
            var episode1 = new Episode { Id = 1 };
            var episode2 = new Episode { Id = 2 };
            _trackedDownload.RemoteEpisode.Episodes = new List<Episode> { episode1, episode2 };

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(
                                   new ImportDecision(
                                       new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv", Episodes = new List<Episode> { episode1 } })),

                               new ImportResult(
                                   new ImportDecision(
                                       new LocalEpisode { Path = @"C:\TestPath\Droned.S01E02.mkv", Episodes = new List<Episode> { episode2 } }))
                           });

            Subject.Import(_trackedDownload);

            AssertImported();
        }

        [Test]
        public void should_mark_as_imported_if_all_episodes_were_imported_including_history()
        {
            var episode1 = new Episode { Id = 1 };
            var episode2 = new Episode { Id = 2 };
            _trackedDownload.RemoteEpisode.Episodes = new List<Episode> { episode1, episode2 };

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                .Returns(new List<ImportResult>
                {
                    new ImportResult(
                        new ImportDecision(
                            new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv", Episodes = new List<Episode> { episode1 } })),

                    new ImportResult(
                        new ImportDecision(
                            new LocalEpisode { Path = @"C:\TestPath\Droned.S01E02.mkv", Episodes = new List<Episode> { episode2 } }), "Test Failure")
                });

            var history = Builder<EpisodeHistory>.CreateListOfSize(2)
                                                  .BuildList();

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(history);

            Mocker.GetMock<ITrackedDownloadAlreadyImported>()
                  .Setup(s => s.IsImported(It.IsAny<TrackedDownload>(), It.IsAny<List<EpisodeHistory>>()))
                  .Returns(true);

            Subject.Import(_trackedDownload);

            AssertImported();
        }

        [Test]
        public void should_mark_as_imported_if_double_episode_file_is_imported()
        {
            var episode1 = new Episode { Id = 1 };
            var episode2 = new Episode { Id = 2 };
            _trackedDownload.RemoteEpisode.Episodes = new List<Episode> { episode1, episode2 };

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(
                                   new ImportDecision(
                                       new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01-E02.mkv", Episodes = new List<Episode> { episode1, episode2 } }))
                           });

            Subject.Import(_trackedDownload);

            AssertImported();
        }

        [Test]
        public void should_mark_as_imported_if_all_episodes_were_imported_but_extra_files_were_not()
        {
            GivenSeriesMatch();

            _trackedDownload.RemoteEpisode.Episodes = new List<Episode>
                                                      {
                                                          new Episode()
                                                      };

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv", Episodes = _trackedDownload.RemoteEpisode.Episodes })),
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv" }), "Test Failure")
                           });

            Subject.Import(_trackedDownload);

            AssertImported();
        }

        [Test]
        public void should_mark_as_imported_if_the_download_can_be_tracked_using_the_source_seriesid()
        {
            GivenABadlyNamedDownload();

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(new ImportDecision(new LocalEpisode { Path = @"C:\TestPath\Droned.S01E01.mkv", Episodes = _trackedDownload.RemoteEpisode.Episodes }))
                           });

            Mocker.GetMock<ISeriesService>()
                  .Setup(v => v.GetSeries(It.IsAny<int>()))
                  .Returns(BuildRemoteEpisode().Series);

            Subject.Import(_trackedDownload);

            AssertImported();
        }

        [Test]
        public void should_not_automatically_fail_usenet_download()
        {
            GivenBelowMinimumDownloads();
            _trackedDownload.Protocol = DownloadProtocol.Usenet;

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().NotBe(TrackedDownloadState.FailedPending);
            _trackedDownload.PreserveFilesOnFailure.Should().BeFalse();
        }

        private List<ImportResult> GivenBelowMinimumDownloads()
        {
            _trackedDownload.PreserveFilesOnFailure = false;
            _trackedDownload.Protocol = DownloadProtocol.Torrent;
            _trackedDownload.RemoteEpisode.Episodes = new List<Episode> { _episode1, _episode2 };
            var results = _trackedDownload.RemoteEpisode.Episodes.Select(episode => new ImportResult(
                new ImportDecision(new LocalEpisode { Path = $"/downloads/{episode.Id}.mkv", Episodes = { episode } },
                    new ImportRejection(ImportRejectionReason.BelowMinimumCustomFormatScore, "Below minimum")), "Below minimum")).ToList();

            Mocker.GetMock<IDownloadedEpisodesImportService>()
                .Setup(s => s.ProcessPath(It.IsAny<string>(), ImportMode.Auto, It.IsAny<Series>(), It.IsAny<DownloadClientItem>()))
                .Returns(results);
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                .Returns(_trackedDownload.RemoteEpisode.Episodes.Select(episode => new EpisodeHistory
                {
                    EpisodeId = episode.Id,
                    SeriesId = _trackedDownload.RemoteEpisode.Series.Id,
                    EventType = EpisodeHistoryEventType.Grabbed
                }).ToList());
            return results;
        }

        [Test]
        public void should_fail_all_below_minimum_pack_and_preserve_files()
        {
            GivenBelowMinimumDownloads();

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.FailedPending);
            _trackedDownload.PreserveFilesOnFailure.Should().BeTrue();
            _trackedDownload.DownloadItem.CanBeRemoved.Should().BeTrue();
        }

        [Test]
        public void should_not_fail_ungrabbed_download()
        {
            GivenBelowMinimumDownloads();
            Mocker.GetMock<IHistoryService>().Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                .Returns(new List<EpisodeHistory>());

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().NotBe(TrackedDownloadState.FailedPending);
            _trackedDownload.PreserveFilesOnFailure.Should().BeFalse();
        }

        [TestCase(ImportRejectionReason.UnableToParse)]
        [TestCase(ImportRejectionReason.NotCustomFormatUpgrade)]
        [TestCase(ImportRejectionReason.EpisodeAlreadyImported)]
        [TestCase(ImportRejectionReason.FileLocked)]
        public void should_not_fail_pack_with_other_rejections(ImportRejectionReason reason)
        {
            var results = GivenBelowMinimumDownloads();
            results[0] = new ImportResult(new ImportDecision(results[0].ImportDecision.LocalEpisode,
                new ImportRejection(reason, "Other reason")), "Other reason");

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().NotBe(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void should_not_fail_pack_with_successful_import()
        {
            var results = GivenBelowMinimumDownloads();
            results[0] = new ImportResult(new ImportDecision(results[0].ImportDecision.LocalEpisode));

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().NotBe(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void should_not_fail_pack_with_missing_expected_episode()
        {
            var results = GivenBelowMinimumDownloads();
            results.RemoveAt(0);

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().NotBe(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void should_not_fail_incomplete_download()
        {
            GivenBelowMinimumDownloads();
            _trackedDownload.DownloadItem.Status = DownloadItemStatus.Downloading;

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().NotBe(TrackedDownloadState.FailedPending);
        }

        [Test]
        public void should_not_fail_library_files()
        {
            var results = GivenBelowMinimumDownloads();
            results[0].ImportDecision.LocalEpisode.ExistingFile = true;

            Subject.Import(_trackedDownload);

            _trackedDownload.State.Should().NotBe(TrackedDownloadState.FailedPending);
        }

        private void AssertNotImported()
        {
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Never());

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportBlocked);
        }

        private void AssertImported()
        {
            Mocker.GetMock<IDownloadedEpisodesImportService>()
                .Verify(v => v.ProcessPath(_trackedDownload.DownloadItem.OutputPath.FullPath, ImportMode.Auto, _trackedDownload.RemoteEpisode.Series, _trackedDownload.DownloadItem), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Once());

            _trackedDownload.State.Should().Be(TrackedDownloadState.Imported);
        }
    }
}
