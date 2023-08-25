// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using Microsoft.Azure.Documents;

    internal static class JsonSerializationFormatExtensions
    {
        private static readonly string Text = ContentSerializationFormat.JsonText.ToString();
        private static readonly string Binary = ContentSerializationFormat.CosmosBinary.ToString();
        private static readonly string HybridRow = ContentSerializationFormat.HybridRow.ToString();

        public static string ToContentSerializationFormatString(this JsonSerializationFormat jsonSerializationFormat)
        {
            return jsonSerializationFormat switch
            {
                JsonSerializationFormat.Text => Text,
                JsonSerializationFormat.Binary => Binary,
                JsonSerializationFormat.HybridRow => HybridRow,
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonSerializationFormat)}: {jsonSerializationFormat}"),
            };
        }
    }
}
