// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;
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
            StringBuilder stringBuilder = new StringBuilder();
            StringWriter stringWriter = new StringWriter(stringBuilder);

            using (JsonWriter writer = new JsonTextWriter(stringWriter))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(Constants.DiagnosticsCoreDiagnostics);
                writer.WriteRawValue(this.coreDiagnostics.ToString());
                writer.WritePropertyName(Constants.DiagnosticsEncryptionDiagnostics);
                writer.WriteRawValue(this.encryptionDiagnostics.ToString());
                writer.WriteEndObject();
            }

            return stringWriter.ToString();
        }
    }
}
