// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionDiagnosticsContent
    {
        public JObject Content { get; }

        private DateTime startTime;
        private Stopwatch stopwatch;

        public EncryptionDiagnosticsContent()
        {
            this.Content = new JObject();
        }

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

        public void Begin()
        {
            this.stopwatch = Stopwatch.StartNew();
            this.startTime = DateTime.UtcNow;
            this.Content.Add(Constants.DiagnosticsStartTime, this.startTime);
        }

        public void End()
        {
            this.stopwatch.Stop();
            this.Content.Add(Constants.DiagnosticsDuration, this.stopwatch.ElapsedMilliseconds);
        }
    }
}
