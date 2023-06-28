namespace CosmosBenchmark.Fx.Tests
{
    using CosmosBenchmark.Fx;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.IO;
    using Moq;
    using Azure.Storage.Blobs;

    [TestClass]
    public class DiagnosticDataListenerTests
    {
        private DiagnosticDataListener listener;

        [TestInitialize]
        public void Setup()
        {
            this.listener = new DiagnosticDataListener();
        }

        [TestCleanup]
        public void Cleanup()
        {
            string[] diagnosticFiles = Directory.GetFiles(".", "BenchmarkDiagnostics.out*");
            foreach (string file in diagnosticFiles)
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void UploadDiagnostics_WhenFilesExist_ShouldUploadFilesToBlobStorage()
        {
            

            for (int i = 0; i < 10; i++)
            {
                string fileName = $"BenchmarkDiagnostics.out-{i}";
                File.Create(fileName).Close();
            }
            int filesCount = Directory.GetFiles(".", $"{DiagnosticDataListener.DiagnosticsFileName}*").Length;

            Mock<BlobContainerClient> mockContainer = new Mock<BlobContainerClient>();
            Mock<BlobClient> mockClient = new Mock<BlobClient>();
            mockContainer.Setup(mock => mock.GetBlobClient(It.IsAny<string>())).Returns(mockClient.Object);

            this.listener.UploadDiagnostcs(mockContainer.Object);
            mockClient.Verify(mock => mock.Upload(It.IsAny<string>(), true, default), Times.Exactly(filesCount));
        }
    }
}