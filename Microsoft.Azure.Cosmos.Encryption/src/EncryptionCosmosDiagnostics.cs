// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionCosmosDiagnostics : CosmosDiagnostics
    {
        private readonly CosmosDiagnostics coreDiagnostics;
        private readonly JObject encryptionDiagnostics;

        public EncryptionCosmosDiagnostics(
            CosmosDiagnostics coreDiagnostics,
            EncryptionDiagnosticsContent encryptContent = null,
            EncryptionDiagnosticsContent decryptContent = null)
        {
            this.coreDiagnostics = coreDiagnostics ?? throw new ArgumentNullException(nameof(coreDiagnostics));
            this.encryptionDiagnostics = new JObject();
            if (encryptContent != null)
            {
                this.AddChild(Constants.DiagnosticsEncryptOperation, encryptContent);
            }

            if (decryptContent != null)
            {
                this.AddChild(Constants.DiagnosticsDecryptOperation, decryptContent);
            }
        }

        public void AddChild(
            string operation,
            EncryptionDiagnosticsContent operationContent)
        {
            this.encryptionDiagnostics.Add(operation, operationContent.Content);
        }

        public override IReadOnlyList<(string regionName, Uri uri)> GetContactedRegions()
        {
            return this.coreDiagnostics.GetContactedRegions();
        }

        public override string ToString()
        {
            string coreDiagnosticsString = this.coreDiagnostics.ToString();
            JObject coreDiagnostics = JObject.Parse(coreDiagnosticsString);
            JObject diagnostics = new JObject
            {
                { Constants.DiagnosticsCoreDiagnostics, coreDiagnostics },
                { Constants.DiagnosticsEncryptionDiagnostics, this.encryptionDiagnostics },
            };

            return diagnostics.ToString();
        }
    }
}
