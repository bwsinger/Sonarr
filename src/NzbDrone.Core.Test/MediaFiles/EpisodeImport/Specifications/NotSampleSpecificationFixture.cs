using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class NotSampleSpecificationFixture : CoreTest<NotSampleSpecification>
    {
        private Series _series;
        private LocalEpisode _localEpisode;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .With(s => s.SeriesType = SeriesTypes.Standard)
                                     .Build();

            var episodes = Builder<Episode>.CreateListOfSize(1)
                                           .All()
                                           .With(e => e.SeasonNumber = 1)
                                           .Build()
                                           .ToList();

            _localEpisode = new LocalEpisode
                                {
                                    Path = @"C:\Test\30 Rock\30.rock.s01e01.avi",
                                    Episodes = episodes,
                                    Series = _series,
                                    Quality = new QualityModel(Quality.HDTV720p)
                                };
        }

        [Test]
        public void should_return_true_for_existing_file()
        {
            _localEpisode.ExistingFile = true;
            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [TestCase(true, DownloadItemStatus.Completed, 0L, ImportRejectionReason.ZeroFilledVideo)]
        [TestCase(false, DownloadItemStatus.Completed, 0L, ImportRejectionReason.SampleIndeterminate)]
        [TestCase(true, DownloadItemStatus.Downloading, 0L, ImportRejectionReason.SampleIndeterminate)]
        [TestCase(true, DownloadItemStatus.Completed, 1L, ImportRejectionReason.SampleIndeterminate)]
        public void should_classify_only_verified_completed_zero_filled_video(bool zeroFilled, DownloadItemStatus status, long remaining, ImportRejectionReason reason)
        {
            Mocker.GetMock<IDetectSample>().Setup(d => d.IsSample(_localEpisode)).Returns(DetectSampleResult.Indeterminate);
            Mocker.GetMock<IZeroFilledVideoDetector>().Setup(d => d.IsZeroFilled(_localEpisode.Path, _localEpisode.Size)).Returns(zeroFilled);

            Subject.IsSatisfiedBy(_localEpisode, new DownloadClientItem { Status = status, RemainingSize = remaining })
                .Reason.Should().Be(reason);
        }

        [Test]
        public void should_leave_manual_probe_failure_for_review()
        {
            Mocker.GetMock<IDetectSample>().Setup(d => d.IsSample(_localEpisode)).Returns(DetectSampleResult.Indeterminate);
            Subject.IsSatisfiedBy(_localEpisode, null).Reason.Should().Be(ImportRejectionReason.SampleIndeterminate);
            Mocker.GetMock<IZeroFilledVideoDetector>().Verify(d => d.IsZeroFilled(It.IsAny<string>(), It.IsAny<long>()), Times.Never());
        }
    }
}
