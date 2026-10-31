using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.TorrentRss;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Aggregation;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Test.Download.TrackedDownloads
{
    [TestFixture]
    public class TrackedDownloadServiceFixture : CoreTest<TrackedDownloadService>
    {
        private void GivenDownloadHistory()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns(new List<EpisodeHistory>()
                {
                 new EpisodeHistory()
                {
                     DownloadId = "35238",
                     SourceTitle = "TV Series S01",
                     SeriesId = 5,
                     EpisodeId = 4
                }
                });
        }

        [Test]
        public void should_track_downloads_using_the_source_title_if_it_cannot_be_found_using_the_download_title()
        {
            GivenDownloadHistory();

            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series() { Id = 5 },
                Episodes = new List<Episode> { new Episode { Id = 4 } },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
                {
                    SeriesTitle = "TV Series",
                    SeasonNumber = 1
                },
                MappedSeasonNumber = 1
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.Is<ParsedEpisodeInfo>(i => i.SeasonNumber == 1 && i.SeriesTitle == "TV Series"), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
                  .Returns(remoteEpisode);

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "The torrent release folder",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = client.Protocol,
                    Id = client.Id,
                    Name = client.Name
                }
            };

            var trackedDownload = Subject.TrackDownload(client, item);

            trackedDownload.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Series.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Series.Id.Should().Be(5);
            trackedDownload.RemoteEpisode.Episodes.First().Id.Should().Be(4);
            trackedDownload.RemoteEpisode.ParsedEpisodeInfo.SeasonNumber.Should().Be(1);
            trackedDownload.RemoteEpisode.MappedSeasonNumber.Should().Be(1);
        }

        [Test]
        public void should_set_indexer()
        {
            var episodeHistory = new EpisodeHistory()
            {
                DownloadId = "35238",
                SourceTitle = "TV Series S01",
                SeriesId = 5,
                EpisodeId = 4,
                EventType = EpisodeHistoryEventType.Grabbed,
            };
            episodeHistory.Data.Add("indexer", "MyIndexer (Prowlarr)");
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns(new List<EpisodeHistory>()
                {
                    episodeHistory
                });

            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
                Name = "MyIndexer (Prowlarr)",
                Settings = new TorrentRssIndexerSettings { MultiLanguages = new List<int> { Language.Original.Id, Language.French.Id } }
            };
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Get(indexerDefinition.Id))
                .Returns(indexerDefinition);
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns(new List<IndexerDefinition>() { indexerDefinition });

            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series() { Id = 5 },
                Episodes = new List<Episode> { new Episode { Id = 4 } },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
                {
                    SeriesTitle = "TV Series",
                    SeasonNumber = 1
                },
                MappedSeasonNumber = 1
            };

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                .Returns(remoteEpisode);

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "TV.Series.S01.MULTi.1080p.WEB.H265-RlsGroup",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = client.Protocol,
                    Id = client.Id,
                    Name = client.Name
                }
            };

            var trackedDownload = Subject.TrackDownload(client, item);

            trackedDownload.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Release.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Release.Indexer.Should().Be("MyIndexer (Prowlarr)");
        }

        [Test]
        public void should_parse_as_special_when_source_title_parsing_fails()
        {
            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series() { Id = 5 },
                Episodes = new List<Episode> { new Episode { Id = 4 } },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
                {
                    SeriesTitle = "TV Series",
                    SeasonNumber = 0,
                    EpisodeNumbers = new[] { 1 }
                },
                MappedSeasonNumber = 0
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns(new List<EpisodeHistory>()
                {
                 new EpisodeHistory()
                {
                     DownloadId = "35238",
                     SourceTitle = "TV Series Special",
                     SeriesId = 5,
                     EpisodeId = 4
                }
                });

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.Is<ParsedEpisodeInfo>(i => i.SeasonNumber == 0 && i.SeriesTitle == "TV Series"), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
                  .Returns(remoteEpisode);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.ParseSpecialEpisodeTitle(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                  .Returns(remoteEpisode.ParsedEpisodeInfo);

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "The torrent release folder",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = client.Protocol,
                    Id = client.Id,
                    Name = client.Name
                }
            };

            var trackedDownload = Subject.TrackDownload(client, item);

            trackedDownload.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Series.Should().NotBeNull();
            trackedDownload.RemoteEpisode.Series.Id.Should().Be(5);
            trackedDownload.RemoteEpisode.Episodes.First().Id.Should().Be(4);
            trackedDownload.RemoteEpisode.ParsedEpisodeInfo.SeasonNumber.Should().Be(0);
            trackedDownload.RemoteEpisode.MappedSeasonNumber.Should().Be(0);
        }

        [Test]
        public void should_unmap_tracked_download_if_episode_deleted()
        {
            GivenDownloadHistory();

            var remoteEpisode = new RemoteEpisode
                                {
                                    Series = new Series() { Id = 5 },
                                    Episodes = new List<Episode> { new Episode { Id = 4 } },
                                    ParsedEpisodeInfo = new ParsedEpisodeInfo()
                                                        {
                                                            SeriesTitle = "TV Series",
                                                            SeasonNumber = 1,
                                                            EpisodeNumbers = new[] { 1 }
                                                        },
                                    MappedSeasonNumber = 0
                                };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                  .Returns(remoteEpisode);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "TV Series - S01E01",
                DownloadId = "12345",
                DownloadClientInfo = new DownloadClientItemClientInfo
                                     {
                                         Id = 1,
                                         Type = "Blackhole",
                                         Name = "Blackhole Client",
                                         Protocol = DownloadProtocol.Torrent
                                     }
            };

            Subject.TrackDownload(client, item);
            Subject.GetTrackedDownloads().Should().HaveCount(1);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                  .Returns(default(RemoteEpisode));

            Subject.Handle(new EpisodeInfoRefreshedEvent(remoteEpisode.Series, new List<Episode>(), new List<Episode>(), remoteEpisode.Episodes));

            var trackedDownloads = Subject.GetTrackedDownloads();
            trackedDownloads.Should().HaveCount(1);
            trackedDownloads.First().RemoteEpisode.Should().BeNull();
        }

        [Test]
        public void should_not_throw_when_processing_deleted_episodes()
        {
            GivenDownloadHistory();

            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series() { Id = 5 },
                Episodes = new List<Episode> { new Episode { Id = 4 } },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
                {
                    SeriesTitle = "TV Series",
                    SeasonNumber = 1,
                    EpisodeNumbers = new[] { 1 }
                },
                MappedSeasonNumber = 0
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                  .Returns(default(RemoteEpisode));

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "TV Series - S01E01",
                DownloadId = "12345",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Id = 1,
                    Type = "Blackhole",
                    Name = "Blackhole Client",
                    Protocol = DownloadProtocol.Torrent
                }
            };

            Subject.TrackDownload(client, item);
            Subject.GetTrackedDownloads().Should().HaveCount(1);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                  .Returns(default(RemoteEpisode));

            Subject.Handle(new EpisodeInfoRefreshedEvent(remoteEpisode.Series, new List<Episode>(), new List<Episode>(), remoteEpisode.Episodes));

            var trackedDownloads = Subject.GetTrackedDownloads();
            trackedDownloads.Should().HaveCount(1);
            trackedDownloads.First().RemoteEpisode.Should().BeNull();
        }

        [Test]
        public void should_not_throw_when_processing_deleted_series()
        {
            GivenDownloadHistory();

            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series() { Id = 5 },
                Episodes = new List<Episode> { new Episode { Id = 4 } },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
                {
                    SeriesTitle = "TV Series",
                    SeasonNumber = 1,
                    EpisodeNumbers = new[] { 1 }
                },
                MappedSeasonNumber = 0
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                  .Returns(default(RemoteEpisode));

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "TV Series - S01E01",
                DownloadId = "12345",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Id = 1,
                    Type = "Blackhole",
                    Name = "Blackhole Client",
                    Protocol = DownloadProtocol.Torrent
                }
            };

            Subject.TrackDownload(client, item);
            Subject.GetTrackedDownloads().Should().HaveCount(1);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                  .Returns(default(RemoteEpisode));

            Subject.Handle(new SeriesDeletedEvent(new List<Series> { remoteEpisode.Series }, true, true));

            var trackedDownloads = Subject.GetTrackedDownloads();
            trackedDownloads.Should().HaveCount(1);
            trackedDownloads.First().RemoteEpisode.Should().BeNull();
        }

        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, true, 1, true, false)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, true, 1, true, true)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, false, 1, false, false)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, false, 1, false, true)]
        [TestCase(DownloadHistoryEventType.DownloadGrabbed, EpisodeHistoryEventType.DownloadFailed, true, 1, false, false)]
        [TestCase(DownloadHistoryEventType.DownloadGrabbed, EpisodeHistoryEventType.DownloadFailed, true, 1, false, true)]
        [TestCase(DownloadHistoryEventType.DownloadImported, EpisodeHistoryEventType.DownloadFailed, true, 1, false, false)]
        [TestCase(DownloadHistoryEventType.DownloadImported, EpisodeHistoryEventType.DownloadFailed, true, 1, false, true)]
        [TestCase(DownloadHistoryEventType.DownloadIgnored, EpisodeHistoryEventType.DownloadFailed, true, 1, false, false)]
        [TestCase(DownloadHistoryEventType.DownloadIgnored, EpisodeHistoryEventType.DownloadFailed, true, 1, false, true)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.Grabbed, true, 1, false, false)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.Grabbed, true, 1, false, true)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFolderImported, true, 1, false, false)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFolderImported, true, 1, false, true)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, true, 2, false, false)]
        [TestCase(DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, true, 2, false, true)]
        public void should_restore_cleanup_only_for_latest_custom_failure(DownloadHistoryEventType downloadEvent, EpisodeHistoryEventType historyEvent, bool tagged, int historyClient, bool expected, bool serialized, string recovery = "minimum-format-score")
        {
            var olderFailure = new EpisodeHistory
            {
                Id = 1,
                EventType = EpisodeHistoryEventType.DownloadFailed,
                SourceTitle = "unparseable",
                Date = System.DateTime.UtcNow
            };
            olderFailure.Data.Add("DevRecovery", "minimum-format-score");
            var latest = new EpisodeHistory
            {
                Id = 2,
                EventType = historyEvent,
                SourceTitle = "unparseable",
                Date = olderFailure.Date
            };
            if (tagged)
            {
                latest.Data.Add("DevRecovery", recovery);
            }

            if (serialized)
            {
                var converter = new EmbeddedDocumentConverter<Dictionary<string, string>>();
                var parameter = new SQLiteParameter();
                converter.SetValue(parameter, olderFailure.Data);
                olderFailure.Data = converter.Parse(parameter.Value);
                converter.SetValue(parameter, latest.Data);
                latest.Data = converter.Parse(parameter.Value);
                olderFailure.Data.Should().ContainKey("devRecovery");
            }

            Mocker.GetMock<IHistoryService>().Setup(s => s.FindByDownloadId("hash"))
                .Returns(new List<EpisodeHistory> { olderFailure, latest });
            Mocker.GetMock<IDownloadHistoryService>().Setup(s => s.GetLatestDownloadHistoryItem("hash"))
                .Returns(new DownloadHistory { EventType = downloadEvent, DownloadClientId = historyClient });
            var client = new DownloadClientDefinition { Id = 1, Protocol = DownloadProtocol.Torrent };
            var item = new DownloadClientItem
            {
                DownloadId = "hash",
                Title = "unparseable",
                CanBeRemoved = false,
                DownloadClientInfo = new DownloadClientItemClientInfo { Name = "qBittorrent" }
            };

            var tracked = Subject.TrackDownload(client, item);
            tracked.PreserveFilesOnFailure.Should().Be(expected);
            tracked.HasZeroFilledVideo.Should().Be(expected && recovery == "zero-filled-video");
            tracked.DownloadItem.CanBeRemoved.Should().BeFalse();

            // A new durable grab also supersedes a cached failure, without publishing another failure.
            Mocker.GetMock<IDownloadHistoryService>().Setup(s => s.GetLatestDownloadHistoryItem("hash"))
                .Returns(new DownloadHistory { EventType = DownloadHistoryEventType.DownloadGrabbed, DownloadClientId = 1 });
            if (expected)
            {
                Subject.TrackDownload(client, item).PreserveFilesOnFailure.Should().BeFalse();
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void should_retry_durable_history_error_without_caching_state(bool terminalReadFails, bool cachedFailure)
        {
            var failure = new EpisodeHistory { EventType = EpisodeHistoryEventType.DownloadFailed, SourceTitle = "unparseable" };
            failure.Data.Add("DevRecovery", "minimum-format-score");
            var durable = new DownloadHistory { EventType = DownloadHistoryEventType.DownloadFailed, DownloadClientId = 1 };
            Mocker.GetMock<IDownloadHistoryService>().Setup(s => s.GetLatestDownloadHistoryItem("hash")).Returns(durable);
            Mocker.GetMock<IHistoryService>().Setup(s => s.FindByDownloadId("hash")).Returns(new List<EpisodeHistory> { failure });
            var client = new DownloadClientDefinition { Id = 1, Protocol = DownloadProtocol.Torrent };
            var item = new DownloadClientItem { DownloadId = "hash", Title = "unparseable", Status = DownloadItemStatus.Completed, DownloadClientInfo = new DownloadClientItemClientInfo { Name = "qBittorrent" } };
            var previous = cachedFailure ? Subject.TrackDownload(client, item) : null;
            if (terminalReadFails)
            {
                Mocker.GetMock<IDownloadHistoryService>().SetupSequence(s => s.GetLatestDownloadHistoryItem("hash"))
                    .Throws(new System.Exception("temporary download history error")).Returns(durable);
            }
            else
            {
                Mocker.GetMock<IHistoryService>().SetupSequence(s => s.FindByDownloadId("hash"))
                    .Throws(new System.Exception("temporary history error")).Returns(new List<EpisodeHistory> { failure });
            }

            Subject.Invoking(s => s.TrackDownload(client, item)).Should().Throw<System.Exception>();
            Subject.Find("hash").Should().BeSameAs(previous);
            var restored = Subject.TrackDownload(client, item);
            restored.State.Should().Be(TrackedDownloadState.Failed);
            restored.PreserveFilesOnFailure.Should().BeTrue();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void should_allow_manual_override_after_failed_refresh(bool restoredAfterRestart, bool previewFirst)
        {
            var series = new Series { Id = 42, Path = "/library/Series" };
            var episodes = new List<Episode> { new Episode { Id = 1 } };
            var failure = new EpisodeHistory { EventType = EpisodeHistoryEventType.DownloadFailed };
            failure.Data.Add("DevRecovery", "minimum-format-score");
            Mocker.GetMock<IHistoryService>().Setup(s => s.FindByDownloadId("hash")).Returns(new List<EpisodeHistory> { failure });
            Mocker.GetMock<IDownloadHistoryService>().Setup(s => s.GetLatestDownloadHistoryItem("hash"))
                .Returns(new DownloadHistory { EventType = DownloadHistoryEventType.DownloadFailed, DownloadClientId = 1 });
            var client = new DownloadClientDefinition { Id = 1, Protocol = DownloadProtocol.Torrent };
            var item = new DownloadClientItem { DownloadId = "hash", Title = "Series.S01E01", OutputPath = new OsPath("/downloads/Series.S01E01"), DownloadClientInfo = new DownloadClientItemClientInfo { Id = 1, Name = "qBittorrent" } };
            var importItem = item.Clone();
            if (!restoredAfterRestart)
            {
                Subject.TrackDownload(client, item).ImportItem = importItem;
            }

            var refreshed = Subject.TrackDownload(client, item);
            refreshed.PreserveFilesOnFailure.Should().BeTrue();
            Mocker.SetConstant<ITrackedDownloadService>(Subject);
            Mocker.GetMock<IProvideImportItemService>().Setup(s => s.ProvideImportItem(item, null)).Returns(importItem);
            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(42)).Returns(series);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodes(It.IsAny<List<int>>())).Returns(episodes);
            Mocker.GetMock<IAggregationService>().Setup(s => s.Augment(It.IsAny<LocalEpisode>(), item)).Returns<LocalEpisode, DownloadClientItem>((local, download) => local);
            Mocker.GetMock<IImportApprovedEpisodes>().Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, item, It.IsAny<ImportMode>()))
                .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, newDownload, download, mode) => decisions.Select(d => new ImportResult(d, new NzbDrone.Core.MediaFiles.EpisodeFile { SeriesId = 42, SeasonNumber = 1 })).ToList());
            var manual = Mocker.Resolve<ManualImportService>();
            if (previewFirst)
            {
                manual.GetMediaFiles("", "hash", 42, true).Should().BeEmpty();
            }

            manual.Execute(new ManualImportCommand { Files = new List<ManualImportFile> { new ManualImportFile { Path = "/downloads/Series.S01E01/Series.S01E01.mkv", SeriesId = 42, EpisodeIds = new List<int> { 1 }, DownloadId = "hash" } } });
            refreshed.ImportItem.Should().BeSameAs(importItem);
            refreshed.State.Should().Be(TrackedDownloadState.Imported);
            Mocker.GetMock<IProvideImportItemService>().Verify(s => s.ProvideImportItem(item, null), restoredAfterRestart ? Times.Once() : Times.Never());
            Mocker.GetMock<IEventAggregator>().Verify(s => s.PublishEvent(It.IsAny<DownloadCompletedEvent>()), Times.Once());
        }

        [TestCase("zero-filled-video", DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, 1, true)]
        [TestCase("zero-filled-video", DownloadHistoryEventType.DownloadGrabbed, EpisodeHistoryEventType.DownloadFailed, 1, false)]
        [TestCase("zero-filled-video", DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.Grabbed, 1, false)]
        [TestCase("zero-filled-video", DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, 2, false)]
        [TestCase("unknown-recovery", DownloadHistoryEventType.DownloadFailed, EpisodeHistoryEventType.DownloadFailed, 1, false)]
        public void should_restore_only_current_known_zero_filled_recovery(string recovery, DownloadHistoryEventType downloadEvent, EpisodeHistoryEventType historyEvent, int historyClient, bool expected)
        {
            should_restore_cleanup_only_for_latest_custom_failure(downloadEvent, historyEvent, true, historyClient, expected, true, recovery);
        }
    }
}
