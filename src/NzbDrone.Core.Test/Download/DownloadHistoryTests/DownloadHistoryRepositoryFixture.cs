using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Download.DownloadHistoryTests
{
    [TestFixture]
    public class DownloadHistoryRepositoryFixture : DbTest<DownloadHistoryRepository, DownloadHistory>
    {
        private Series _series1;
        private Series _series2;

        [SetUp]
        public void Setup()
        {
            _series1 = Builder<Series>.CreateNew()
                                      .With(s => s.Id = 7)
                                      .Build();

            _series2 = Builder<Series>.CreateNew()
                                      .With(s => s.Id = 8)
                                      .Build();
        }

        [Test]
        public void should_delete_history_items_by_seriesId()
        {
            var items = Builder<DownloadHistory>.CreateListOfSize(5)
                .TheFirst(1)
                .With(c => c.Id = 0)
                .With(c => c.SeriesId = _series2.Id)
                .TheRest()
                .With(c => c.Id = 0)
                .With(c => c.SeriesId = _series1.Id)
                .BuildListOfNew();

            Db.InsertMany(items);

            Subject.DeleteBySeriesIds(new List<int> { _series1.Id });

            var removedItems = Subject.All().Where(h => h.SeriesId == _series1.Id);
            var nonRemovedItems = Subject.All().Where(h => h.SeriesId == _series2.Id);

            removedItems.Should().HaveCount(0);
            nonRemovedItems.Should().HaveCount(1);
        }

        [Test]
        public void should_return_newer_grab_before_failure_when_dates_match()
        {
            var date = System.DateTime.UtcNow;
            var failed = new DownloadHistory { DownloadId = "hash", SourceTitle = "torrent", Date = date, EventType = DownloadHistoryEventType.DownloadFailed };
            var regrabbed = new DownloadHistory { DownloadId = "hash", SourceTitle = "torrent", Date = date, EventType = DownloadHistoryEventType.DownloadGrabbed };
            Db.Insert(failed);
            Db.Insert(regrabbed);

            Subject.FindByDownloadId("hash").Select(h => h.EventType)
                .Should().Equal(DownloadHistoryEventType.DownloadGrabbed, DownloadHistoryEventType.DownloadFailed);
        }
    }
}
