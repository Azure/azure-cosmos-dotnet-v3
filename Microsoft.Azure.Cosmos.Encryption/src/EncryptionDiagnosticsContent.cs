// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionDiagnosticsContent
    {
        public JObject Content { get; }

        public EncryptionDiagnosticsContent(
            DateTime startTime,
            long durationInMs,
            int? propertiesCount = null)
        {
            this.Content = new JObject
            {
                { Constants.DiagnosticsStartTime, startTime },
                { Constants.DiagnosticsDuration, durationInMs },
            };

            if (propertiesCount != null)
            {
                this.Content.Add(Constants.DiagnosticsPropertiesCount, propertiesCount.Value);
            }
        }

        public void AddMember(string property, JToken value)
        {
            this.Content.Add(property, value);
        }
    }
}
