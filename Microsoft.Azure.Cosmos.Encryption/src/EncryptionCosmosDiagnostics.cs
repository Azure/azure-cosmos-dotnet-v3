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
            JObject encryptContent = null,
            JObject decryptContent = null)
        {
            this.coreDiagnostics = coreDiagnostics ?? throw new ArgumentNullException(nameof(coreDiagnostics));
            this.encryptionDiagnostics = new JObject();
            if (encryptContent?.Count > 0)
            {
                this.encryptionDiagnostics.Add(Constants.DiagnosticsEncryptOperation, encryptContent);
            }

            if (decryptContent?.Count > 0)
            {
                this.encryptionDiagnostics.Add(Constants.DiagnosticsDecryptOperation, decryptContent);
            }
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
