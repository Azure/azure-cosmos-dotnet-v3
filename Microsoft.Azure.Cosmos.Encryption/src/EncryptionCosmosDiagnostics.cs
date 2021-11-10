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
        private readonly JObject encryptContent;
        private readonly JObject decryptContent;

        public EncryptionCosmosDiagnostics(
            CosmosDiagnostics coreDiagnostics,
            JObject encryptContent,
            JObject decryptContent)
        {
            this.coreDiagnostics = coreDiagnostics ?? throw new ArgumentNullException(nameof(coreDiagnostics));
            if (encryptContent?.Count > 0)
            {
                this.encryptContent = encryptContent;
            }

            if (decryptContent?.Count > 0)
            {
                this.decryptContent = decryptContent;
            }
        }

        public override IReadOnlyList<(string regionName, Uri uri)> GetContactedRegions()
        {
            return this.coreDiagnostics.GetContactedRegions();
        }

        public override TimeSpan GetClientElapsedTime()
        {
            TimeSpan clientElapsedTime = this.coreDiagnostics.GetClientElapsedTime();

            int durationInMilliseconds = 0;
            if (this.encryptContent != null)
            {
                durationInMilliseconds = this.encryptContent.Value<int>(Constants.DiagnosticsDuration);
            }

            if (this.decryptContent != null)
            {
                durationInMilliseconds += this.decryptContent.Value<int>(Constants.DiagnosticsDuration);
            }

            if (durationInMilliseconds > 0)
            {
                clientElapsedTime = clientElapsedTime.Add(TimeSpan.FromMilliseconds(durationInMilliseconds));
            }

            return clientElapsedTime;
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
                writer.WriteStartObject();

                if (this.encryptContent != null)
                {
                    writer.WritePropertyName(Constants.DiagnosticsEncryptOperation);
                    writer.WriteRawValue(this.encryptContent.ToString());
                }

                if (this.decryptContent != null)
                {
                    writer.WritePropertyName(Constants.DiagnosticsDecryptOperation);
                    writer.WriteRawValue(this.decryptContent.ToString());
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return stringWriter.ToString();
        }
    }
}
