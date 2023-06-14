namespace CosmosBenchmark.Fx
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;

    internal class DiagnosticDataListener : EventListener
    {
        private const string BlobContainerName = "diagnostics";
        private const string DiagnosticsFilePath = "BenchmarkDiagnostics.out";

        public DiagnosticDataListener()
        {
            if (!File.Exists(DiagnosticsFilePath))
            {
                File.Create(DiagnosticsFilePath);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            using (StreamWriter writer = new StreamWriter(DiagnosticsFilePath, true))
            {
                writer.WriteLine($"{eventData.Payload[2]} ; {eventData.Payload[3]}");
            }
        }

        public static void UploadDiagnostcs(BenchmarkConfig config)
        {
            try
            {
                string BlobName = $"{Environment.MachineName}-BenchmarkDiagnostics.out";

                Console.WriteLine("Uploading diagnostics");
                BlobContainerClient blobContainerClient = GetBlobServiceClient(config);
                BlobClient blobClient = blobContainerClient.GetBlobClient(BlobName);
                blobClient.Upload(DiagnosticsFilePath, overwrite: true);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static BlobContainerClient GetBlobServiceClient(BenchmarkConfig config)
        {
            BlobContainerClient blobContainerClient = new BlobContainerClient(config.ResultsStorageConnectionString, BlobContainerName);
            blobContainerClient.CreateIfNotExists();
            return blobContainerClient;
        }
    }
}
