// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal ref struct RangeRefStruct
    {
        private const string MinPropertyName = "min";
        private const string MaxPropertyName = "max";

        public RangeRefStruct(string min, string max)
        {
            if (min == null)
            {
                throw new ArgumentNullException(nameof(min));
            }

            if (max == null)
            {
                throw new ArgumentNullException(nameof(max));
            }

            this.Min = min;
            this.Max = max;
        }

        public string Min { get; }
        public string Max { get; }

        public void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            jsonWriter.WriteObjectStart();

            jsonWriter.WriteFieldName(RangeRefStruct.MinPropertyName);
            jsonWriter.WriteStringValue(this.Min);

            jsonWriter.WriteFieldName(RangeRefStruct.MaxPropertyName);
            jsonWriter.WriteStringValue(this.Max);

            jsonWriter.WriteObjectEnd();
        }
    }
}
