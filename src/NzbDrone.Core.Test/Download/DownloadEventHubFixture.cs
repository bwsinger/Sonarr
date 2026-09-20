using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class DownloadEventHubFixture : CoreTest<DownloadEventHub>
    {
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void failed_download_removal_respects_payload_preservation(bool preserveFiles, bool deleteData)
        {
            var client = Mocker.GetMock<IDownloadClient>();
            client.SetupGet(c => c.Definition).Returns(new DownloadClientDefinition { RemoveFailedDownloads = true });
            Mocker.GetMock<IProvideDownloadClient>().Setup(p => p.Get(1)).Returns(client.Object);
            var tracked = new TrackedDownload
            {
                DownloadClient = 1,
                PreserveFilesOnFailure = preserveFiles,
                DownloadItem = new DownloadClientItem
                {
                    CanBeRemoved = true,
                    DownloadClientInfo = new DownloadClientItemClientInfo { Name = "Test" }
                }
            };

            Subject.Handle(new DownloadFailedEvent { TrackedDownload = tracked });

            client.Verify(c => c.RemoveItem(tracked.DownloadItem, deleteData), Times.Once());
        }
    }
}
