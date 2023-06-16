namespace CosmosBenchmark.Fx
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Threading;
    using Azure.Storage.Blobs;

    internal class DiagnosticDataListener : EventListener
    {
        private const string BlobContainerName = "diagnostics";
        private const string DiagnosticsFilePath = "BenchmarkDiagnostics.out";
        private static readonly object fileLock = new object();
        private int filesCount = 0;

        public DiagnosticDataListener()
        {
            if (!File.Exists(DiagnosticsFilePath))
            {
                File.Create(DiagnosticsFilePath);
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                while (true)
                {
                    lock (fileLock)
                    {
                        // Check the file size
                        if (!File.Exists(DiagnosticsFilePath))
                        {
                            File.Create(DiagnosticsFilePath);
                        }

                        FileInfo fileInfo = new FileInfo(DiagnosticsFilePath);
                        long fileSize = fileInfo.Length;

                        // If the file size is greater than 100MB (100,000,000 bytes)
                        if (fileSize > 100_000_000)
                        {

                            // Create a new file with the same name
                            string newFilePath = Path.Combine(fileInfo.DirectoryName, $"{fileInfo.Name}-{this.filesCount}");
                            File.Move(DiagnosticsFilePath, newFilePath, true);
                            this.filesCount++;

                            // Optionally, you can perform additional actions on the new file
                            // For example, you can delete or process it

                            Console.WriteLine("File size exceeded 100MB. Renamed the file and created a new one.");
                        }

                    }

                    // Wait for 10 seconds before checking again
                    Thread.Sleep(5_000);
                }
            });
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            lock (fileLock)
            {
                using (StreamWriter writer = new StreamWriter(DiagnosticsFilePath, true))
                {
                    writer.WriteLine($"{eventData.Payload[2]} ; {eventData.Payload[3]}");
                }
            }
        }

        public static void UploadDiagnostcs(BenchmarkConfig config)
        {
            try
            {
                Console.WriteLine("Uploading diagnostics");
                string[] diagnosticFiles = Directory.GetFiles(".", $"{DiagnosticsFilePath}*");

                lock (fileLock)
                {
                    for (int i = 0; i < diagnosticFiles.Length; i++)
                    {
                        string diagnosticFile = diagnosticFiles[i];
                        Console.WriteLine($"Uploading {i+1} of {diagnosticFiles.Length} file: {diagnosticFile} ");

                        string BlobName = $"{Environment.MachineName}{diagnosticFile}";
                        BlobContainerClient blobContainerClient = GetBlobServiceClient(config);
                        BlobClient blobClient = blobContainerClient.GetBlobClient(BlobName);

                        blobClient.Upload(diagnosticFile, overwrite: true);
                    }
                }

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
