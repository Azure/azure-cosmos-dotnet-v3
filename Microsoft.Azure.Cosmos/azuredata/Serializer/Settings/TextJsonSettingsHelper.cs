//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Text.Json;

    internal static class TextJsonSettingsHelper
    {
        public static void WriteId(
            Utf8JsonWriter writer,
            string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                writer.WriteString(JsonEncodedStrings.Id, id);
            }
        }

        public static void WriteETag(
            Utf8JsonWriter writer,
            ETag? etag)
        {
            if (etag.HasValue)
            {
                writer.WriteString(JsonEncodedStrings.ETag, etag.ToString());
            }
        }

        public static void WriteResourceId(
            Utf8JsonWriter writer,
            string resourceId)
        {
            if (!string.IsNullOrEmpty(resourceId))
            {
                writer.WriteString(JsonEncodedStrings.RId, resourceId);
            }
        }

        public static void WriteLastModified(
            Utf8JsonWriter writer,
            DateTime? lastModified,
            JsonSerializerOptions options)
        {
            if (lastModified.HasValue)
            {
                writer.WritePropertyName(JsonEncodedStrings.LastModified);
                TextJsonUnixDateTimeConverter.WritePropertyValues(writer, lastModified, options);
            }
        }

        public static ETag ReadETag(JsonProperty jsonProperty) => new ETag(jsonProperty.Value.GetString());

        public static bool TryParseEnum<TEnum>(
            JsonProperty property,
            Action<TEnum> action)
            where TEnum : struct, IComparable => TextJsonSettingsHelper.TryParseEnum<TEnum>(property.Value, action);

        public static bool TryParseEnum<TEnum>(
            JsonElement element,
            Action<TEnum> action)
            where TEnum : struct, IComparable
        {
            if (Enum.TryParse(value: element.GetString(), ignoreCase: true, out TEnum enumValue))
            {
                action(enumValue);
                return true;
            }

            return false;
        }
    }
}
