// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using Microsoft.Azure.Documents;

    internal static class ContentSerializationFormatExtensions
    {
        private static readonly string Text = ContentSerializationFormat.JsonText.ToString();
        private static readonly string Binary = ContentSerializationFormat.CosmosBinary.ToString();
        private static readonly string HybridRow = ContentSerializationFormat.HybridRow.ToString();

        public static string ToStringOptimized(this ContentSerializationFormat contentSerializationFormat)
        {
            return contentSerializationFormat switch
            {
                ContentSerializationFormat.JsonText => Text,
                ContentSerializationFormat.CosmosBinary => Binary,
                ContentSerializationFormat.HybridRow => HybridRow,
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(ContentSerializationFormat)}: {contentSerializationFormat}"),
            };
        }
    }
}
