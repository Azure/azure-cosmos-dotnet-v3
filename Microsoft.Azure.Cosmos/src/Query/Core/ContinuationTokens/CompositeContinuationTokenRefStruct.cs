// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal ref struct CompositeContinuationTokenRefStruct
    {
        private const string TokenProperytName = "token";
        private const string RangePropertyName = "range";

        public CompositeContinuationTokenRefStruct(string backendContinuationToken, RangeRefStruct range)
        {
            this.BackendContinuationToken = backendContinuationToken;
            this.Range = range;
        }

        public string BackendContinuationToken { get; }

        public RangeRefStruct Range { get; }

        public void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            jsonWriter.WriteObjectStart();

            jsonWriter.WriteFieldName(CompositeContinuationTokenRefStruct.TokenProperytName);
            if (this.BackendContinuationToken != null)
            {
                jsonWriter.WriteStringValue(this.BackendContinuationToken);
            }
            else
            {
                jsonWriter.WriteNullValue();
            }

            jsonWriter.WriteFieldName(CompositeContinuationTokenRefStruct.RangePropertyName);
            this.Range.WriteTo(jsonWriter);

            jsonWriter.WriteObjectEnd();
        }
    }
}
