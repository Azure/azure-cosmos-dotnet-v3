//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using Microsoft.Azure.Documents;

    internal static class TextJsonSettingsHelper
    {
        public static void WriteId(
            Utf8JsonWriter writer,
            string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                writer.WriteString(Constants.Properties.Id, id);
            }
        }

        public static void WriteETag(
            Utf8JsonWriter writer,
            ETag? etag)
        {
            if (etag.HasValue)
            {
                writer.WriteString(Constants.Properties.ETag, etag.ToString());
            }
        }

        public static void WriteResourceId(
            Utf8JsonWriter writer,
            string resourceId)
        {
            if (!string.IsNullOrEmpty(resourceId))
            {
                writer.WriteString(Constants.Properties.RId, resourceId);
            }
        }

        public static void WriteLastModified(
            Utf8JsonWriter writer,
            DateTime? lastModified,
            JsonSerializerOptions options)
        {
            if (lastModified.HasValue)
            {
                writer.WritePropertyName(Constants.Properties.LastModified);
                TextJsonUnixDateTimeConverter.WritePropertyValues(writer, lastModified, options);
            }
        }

        public static ETag ReadETag(JsonProperty jsonProperty) => new ETag(jsonProperty.Value.GetString());
    }
}
