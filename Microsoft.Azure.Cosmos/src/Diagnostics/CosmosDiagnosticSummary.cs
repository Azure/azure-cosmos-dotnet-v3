//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Newtonsoft.Json;

    internal sealed class CosmosDiagnosticSummary
    {
        private static readonly string DefaultUserAgentString;

        private readonly DateTime StartUtc;

        private long? RetryCount = null;

        private TimeSpan? RetryBackoffTimeSpan = null;

        private TimeSpan? TotalElapsedTime = null;

        private string UserAgent = CosmosDiagnosticSummary.DefaultUserAgentString;

        private bool IsDefaultUserAgent = true;

        static CosmosDiagnosticSummary()
        {
            // Default user agent string does not contain client id or features.
            UserAgentContainer userAgentContainer = new UserAgentContainer();
            CosmosDiagnosticSummary.DefaultUserAgentString = userAgentContainer.UserAgent;
        }

        internal CosmosDiagnosticSummary(
            DateTime startTimeUtc)
        {
            this.StartUtc = startTimeUtc;
        }

        internal void SetSdkUserAgent(string userAgent)
        {
            this.UserAgent = userAgent ?? throw new ArgumentNullException(nameof(userAgent));
            this.IsDefaultUserAgent = false;
        }

        internal void AddSdkRetry(TimeSpan backOffTimeSpan)
        {
            if (this.RetryBackoffTimeSpan == null)
            {
                this.RetryBackoffTimeSpan = TimeSpan.Zero;
            }

            this.RetryBackoffTimeSpan = this.RetryBackoffTimeSpan.Value.Add(backOffTimeSpan);
            this.RetryCount = this.RetryCount.GetValueOrDefault(0) + 1;
        }

        internal void SetElapsedTime(TimeSpan totalElapsedTime)
        {
            this.TotalElapsedTime = totalElapsedTime;
        }

        internal void Append(CosmosDiagnosticSummary newSummary)
        {
            if (Object.ReferenceEquals(this, newSummary))
            {
                return;
            }

            if (this.IsDefaultUserAgent && !newSummary.IsDefaultUserAgent)
            {
                this.SetSdkUserAgent(newSummary.UserAgent);
            }

            if (newSummary.RetryCount.HasValue)
            {
                this.RetryCount += newSummary.RetryCount;
            }

            if (newSummary.RetryBackoffTimeSpan.HasValue)
            {
                this.RetryBackoffTimeSpan += newSummary.RetryBackoffTimeSpan;
            }

            if (newSummary.TotalElapsedTime.HasValue &&
                (!this.TotalElapsedTime.HasValue ||
                    this.TotalElapsedTime < newSummary.TotalElapsedTime))
            {
                this.TotalElapsedTime = newSummary.TotalElapsedTime;
            }
        }

        internal void WriteJsonProperty(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("StartUtc");
            writer.WriteValue(this.StartUtc.ToString("o", CultureInfo.InvariantCulture));
            
            if (this.TotalElapsedTime.HasValue)
            {
                writer.WritePropertyName("ElapsedTime");
                writer.WriteValue(this.TotalElapsedTime.Value);
            }

            writer.WritePropertyName("UserAgent");
            writer.WriteValue(this.UserAgent);

            if (this.RetryCount.HasValue && this.RetryCount.Value > 0)
            {
                writer.WritePropertyName("RetryCount");
                writer.WriteValue(this.RetryCount.Value);
            }

            if (this.RetryBackoffTimeSpan.HasValue && this.RetryBackoffTimeSpan.Value > TimeSpan.Zero)
            {
                writer.WritePropertyName("RetryBackOffTime");
                writer.WriteValue(this.RetryBackoffTimeSpan.Value);
            }

            writer.WriteEndObject();
        }
    }
}
