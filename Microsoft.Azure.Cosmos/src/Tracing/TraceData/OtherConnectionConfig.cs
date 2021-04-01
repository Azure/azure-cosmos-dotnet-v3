// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing.TraceData
{
    using System;
    using System.Globalization;

    internal class OtherConnectionConfig
    {
        public OtherConnectionConfig(
            bool limitToEndpoint,
            bool allowBulkExecution)
        {
            this.LimitToEndpoint = limitToEndpoint;
            this.AllowBulkExecution = allowBulkExecution;
            this.lazyString = new Lazy<string>(() => string.Format(CultureInfo.InvariantCulture,
                                 "(ed:{0}, be:{1})",
                                 limitToEndpoint,
                                 allowBulkExecution));
            this.lazyJsonString = new Lazy<string>(() => Newtonsoft.Json.JsonConvert.SerializeObject(this));
        }

        public bool LimitToEndpoint { get; }
        public bool AllowBulkExecution { get; }

        private readonly Lazy<string> lazyString;
        private readonly Lazy<string> lazyJsonString;

        public override string ToString()
        {
            return this.lazyString.Value;
        }

        public string ToJsonString()
        {
            return this.lazyJsonString.Value;
        }
    }
}