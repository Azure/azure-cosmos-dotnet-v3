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
        private readonly TimeSpan processingDuration;

        public EncryptionCosmosDiagnostics(
            CosmosDiagnostics coreDiagnostics,
            JObject encryptContent,
            JObject decryptContent,
            TimeSpan processingDuration)
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

            this.processingDuration = processingDuration;
        }

        public override IReadOnlyList<(string regionName, Uri uri)> GetContactedRegions()
        {
            return this.coreDiagnostics.GetContactedRegions();
        }

        public override TimeSpan GetClientElapsedTime()
        {
            TimeSpan clientElapsedTime = this.coreDiagnostics.GetClientElapsedTime();

            if (this.processingDuration.Ticks > 0)
            {
                clientElapsedTime += this.processingDuration;
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

#if SDKPROJECTREF

        public EncryptionCosmosDiagnostics(ITrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            // Need to set to the root trace, since we don't know which layer of the stack the response message was returned from.
            ITrace rootTrace = trace;
            while (rootTrace.Parent != null)
            {
                rootTrace = rootTrace.Parent;
            }

            this.Value = rootTrace;
        }
        
        public ITrace Value { get; }

        public override DateTime GetStartTimeUtc()
        {
            if (this.Value == null)
            {
                return DateTime.MinValue;
            }

            return this.Value.StartTime;
        }

        public override int GetFailedRequestCount()
        {
            return this.WalkTraceTreeForFailedRequestCount(this.Value);
        }

        private int WalkTraceTreeForFailedRequestCount(ITrace currentTrace)
        {
            if (currentTrace == null)
            {
                return this.failedRequestCount;
            }

            foreach (object datums in currentTrace.Data.Values)
            {
                if (datums is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    foreach (StoreResponseStatistics responseStatistics in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                    {
                        if (responseStatistics.StoreResult != null && !((HttpStatusCode)responseStatistics.StoreResult.StatusCode).IsSuccess())
                        {
                            this.failedRequestCount++;
                        }
                    }
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                this.failedRequestCount += this.WalkTraceTreeForFailedRequestCount(childTrace);
            }

            return this.failedRequestCount;
        }

        private int failedRequestCount;
#endif

    }
}
