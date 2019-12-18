//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using Newtonsoft.Json;

    /// <summary>
    /// This represents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal class CosmosDiagnosticsContext : CosmosDiagnostics
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Culture = CultureInfo.InvariantCulture,
            Formatting = Formatting.None
        };

        private static readonly string DefaultUserAgentString;

        [JsonProperty(PropertyName = "RetryCount")]
        private long? retryCount;

        [JsonProperty(PropertyName = "RetryBackoffTimeSpan")]
        private TimeSpan? retryBackoffTimeSpan;

        [JsonProperty(PropertyName = "UserAgent")]
        private string userAgent;

        [JsonProperty(PropertyName = "ContextList")]
        private List<object> contextList { get; }

        static CosmosDiagnosticsContext()
        {
            // Default user agent string does not contain client id or features.
            UserAgentContainer userAgentContainer = new UserAgentContainer();
            CosmosDiagnosticsContext.DefaultUserAgentString = userAgentContainer.UserAgent;
        }

        internal CosmosDiagnosticsContext()
        {
            this.retryCount = null;
            this.retryBackoffTimeSpan = null;
            this.userAgent = CosmosDiagnosticsContext.DefaultUserAgentString;
            this.contextList = new List<object>();
        }

        internal CosmosDiagnosticsScope CreateScope(string name)
        {
            CosmosDiagnosticsScope scope = new CosmosDiagnosticsScope(name);
            this.contextList.Add(scope);
            return scope;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, CosmosDiagnosticsContext.JsonSerializerSettings);
        }

        internal void SetSdkUserAgent(string userAgent)
        {
            this.userAgent = userAgent ?? throw new ArgumentNullException(nameof(userAgent));
        }

        internal void AddSdkRetry(TimeSpan backOffTimeSpan)
        {
            if (this.retryBackoffTimeSpan == null)
            {
                this.retryBackoffTimeSpan = TimeSpan.Zero;
            }

            this.retryBackoffTimeSpan = this.retryBackoffTimeSpan.Value.Add(backOffTimeSpan);
            this.retryCount = this.retryCount.GetValueOrDefault(0) + 1;
        }

        internal void AddJsonAttribute(string name, dynamic property)
        {
            this.contextList.Add(new CosmosDiagnosticAttribute(name, property));
        }

        /// <summary>
        /// Supports appending an object that implements ICosmosDiagnosticsJsonWriter so
        /// it can be lazy.
        /// </summary>
        private class CosmosDiagnosticAttribute
        {
            [JsonProperty(PropertyName = "Id")]
            private readonly string id;

            [JsonProperty(PropertyName = "Value")]
            private readonly dynamic value;

            internal CosmosDiagnosticAttribute(
                string name,
                dynamic value)
            {
                this.id = name ?? throw new ArgumentNullException(nameof(name));
                this.value = value ?? throw new ArgumentNullException(nameof(value));
            }
        }

        /// <summary>
        /// This represents a single scope in the diagnostics.
        /// A scope is a section of code that is important to track.
        /// For example there is a scope for serialization, retry handlers, etc..
        /// </summary>
        internal class CosmosDiagnosticsScope : IDisposable
        {
            [JsonProperty(PropertyName = "Id")]
            private string Id { get; }

            [JsonProperty(PropertyName = "StartTimeUtc")]
            private DateTime StartTimeUtc { get; }

            private Stopwatch ElapsedTimeStopWatch { get; }

            [JsonProperty(PropertyName = "ElapsedTime")]
            private TimeSpan? ElapsedTime { get; set; }

            internal CosmosDiagnosticsScope(
                string name)
            {
                this.Id = name;
                this.StartTimeUtc = DateTime.UtcNow;
                this.ElapsedTimeStopWatch = Stopwatch.StartNew();
                this.ElapsedTime = null;
            }

            public void Dispose()
            {
                this.ElapsedTimeStopWatch.Stop();
                this.ElapsedTime = this.ElapsedTimeStopWatch.Elapsed;
            }
        }
    }
}
