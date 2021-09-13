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
        public JObject EncryptContent { get; }

        public JObject DecryptContent { get; }

        private DateTime startTime;
        private Stopwatch stopwatch;
        private bool isDecryptionOperation;

        public EncryptionDiagnosticsContext()
        {
            this.EncryptContent = new JObject();
            this.DecryptContent = new JObject();
        }

        public void Begin(string operation)
        {
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

            this.stopwatch = Stopwatch.StartNew();
            this.startTime = DateTime.UtcNow;
        }

        public void End(int? propertiesCount = null)
        {
            this.stopwatch.Stop();

            if (this.isDecryptionOperation)
            {
                this.DecryptContent.Add(Constants.DiagnosticsDuration, this.stopwatch.ElapsedMilliseconds);

                if (propertiesCount.HasValue)
                {
                    this.DecryptContent.Add(Constants.DiagnosticsPropertiesCount, propertiesCount);
                }
            }
            else
            {
                this.EncryptContent.Add(Constants.DiagnosticsDuration, this.stopwatch.ElapsedMilliseconds);

                if (propertiesCount.HasValue)
                {
                    this.EncryptContent.Add(Constants.DiagnosticsPropertiesCount, propertiesCount);
                }
            }
        }

        public void AddEncryptionDiagnostics(
            ResponseMessage responseMessage)
        {
            EncryptionCosmosDiagnostics encryptionDiagnostics = new EncryptionCosmosDiagnostics(
                responseMessage.Diagnostics,
                this.EncryptContent,
                this.DecryptContent);

            responseMessage.Diagnostics = encryptionDiagnostics;
        }
    }
}
