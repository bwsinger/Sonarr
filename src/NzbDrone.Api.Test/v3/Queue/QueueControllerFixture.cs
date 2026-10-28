using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Queue;
using Sonarr.Http;

namespace NzbDrone.Api.Test.v3.Queue
{
    [TestFixture]
    public class QueueControllerFixture : TestBase<QueueController>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IQualityProfileService>()
                  .Setup(s => s.GetDefaultProfile(string.Empty, null))
                  .Returns(new QualityProfile
                  {
                      Items = new List<QualityProfileQualityItem>
                      {
                          new QualityProfileQualityItem { Quality = Quality.HDTV720p }
                      }
                  });

            Mocker.GetMock<IQueueService>().Setup(s => s.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>
            {
                new NzbDrone.Core.Queue.Queue
                {
                    Id = 1,
                    Series = new Series { Id = 10 },
                    Languages = new List<Language> { Language.English },
                    Quality = new QualityModel(Quality.HDTV720p),
                    Status = QueueStatus.Downloading
                },
                new NzbDrone.Core.Queue.Queue
                {
                    Id = 2,
                    Series = new Series { Id = 20 },
                    Languages = new List<Language> { Language.French },
                    Quality = new QualityModel(Quality.SDTV),
                    Status = QueueStatus.Queued
                }
            });
            Mocker.GetMock<IPendingReleaseService>().Setup(s => s.GetPendingQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
        }

        [TestCase(null)]
        [TestCase("empty")]
        [TestCase("seriesIds")]
        [TestCase("languages")]
        [TestCase("quality")]
        [TestCase("status")]
        public void should_allow_omitted_filters_and_apply_supplied_filters(string filter)
        {
            var empty = filter == "empty";
            var result = Subject.GetQueue(
                new PagingRequestResource(),
                seriesIds: filter == "seriesIds" ? new[] { 10 } : empty ? Array.Empty<int>() : null,
                languages: filter == "languages" ? new[] { Language.English.Id } : empty ? Array.Empty<int>() : null,
                quality: filter == "quality" ? new[] { Quality.HDTV720p.Id } : empty ? Array.Empty<int>() : null,
                status: filter == "status" ? new[] { QueueStatus.Downloading } : empty ? Array.Empty<QueueStatus>() : null);

            var expectedIds = filter == null || empty ? new[] { 1, 2 } : new[] { 1 };
            result.TotalRecords.Should().Be(expectedIds.Length);
            result.Records.Select(q => q.Id).Should().BeEquivalentTo(expectedIds);
        }
    }
}
