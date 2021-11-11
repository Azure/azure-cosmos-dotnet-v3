// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionDiagnosticsContext
    {
        private DateTime startTime;
        private Stopwatch stopwatch;
        private bool isDecryptionOperation;

        public EncryptionDiagnosticsContext()
        {
            this.EncryptContent = new JObject();
            this.DecryptContent = new JObject();
            this.TotalDurationInMilliseconds = 0;
        }

        // time taken for encryption + decryption
        public long TotalDurationInMilliseconds { get; private set; }

        public JObject EncryptContent { get; }

        public JObject DecryptContent { get; }

        public void Begin(string operation)
        {
            this.stopwatch = Stopwatch.StartNew();
            this.startTime = DateTime.UtcNow;

            switch (operation)
            {
                case Constants.DiagnosticsEncryptOperation:
                    this.EncryptContent.Add(Constants.DiagnosticsStartTime, this.startTime);
                    this.isDecryptionOperation = false;
                    break;

                case Constants.DiagnosticsDecryptOperation:
                    this.DecryptContent.Add(Constants.DiagnosticsStartTime, this.startTime);
                    this.isDecryptionOperation = true;
                    break;

                default:
                    throw new NotSupportedException($"Operation: {operation} is not supported. " +
                        $"Should be either {Constants.DiagnosticsEncryptOperation} or {Constants.DiagnosticsDecryptOperation}.");
            }
        }

        public void End(int? propertiesCount = null)
        {
            this.stopwatch.Stop();
            this.TotalDurationInMilliseconds += this.stopwatch.ElapsedMilliseconds;

            if (this.isDecryptionOperation)
            {
                this.DecryptContent.Add(Constants.DiagnosticsDuration, this.stopwatch.ElapsedMilliseconds);

                if (propertiesCount.HasValue)
                {
                    this.DecryptContent.Add(Constants.DiagnosticsPropertiesDecryptedCount, propertiesCount);
                }
            }
            else
            {
                this.EncryptContent.Add(Constants.DiagnosticsDuration, this.stopwatch.ElapsedMilliseconds);

                if (propertiesCount.HasValue)
                {
                    this.EncryptContent.Add(Constants.DiagnosticsPropertiesEncryptedCount, propertiesCount);
                }
            }
        }

        public void AddEncryptionDiagnosticsToResponseMessage(
            ResponseMessage responseMessage)
        {
            EncryptionCosmosDiagnostics encryptionDiagnostics = new EncryptionCosmosDiagnostics(
                responseMessage.Diagnostics,
                this.EncryptContent,
                this.DecryptContent,
                this.TotalDurationInMilliseconds);

            responseMessage.Diagnostics = encryptionDiagnostics;
        }
    }
}
