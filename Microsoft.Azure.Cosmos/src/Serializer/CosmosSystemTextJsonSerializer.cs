﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// This class provides a default implementation of System.Text.Json Cosmos Linq Serializer.
    /// </summary>
    internal class CosmosSystemTextJsonSerializer : CosmosLinqSerializer
    {
        /// <summary>
        /// A read-only instance of <see cref="JsonSerializerOptions"/>.
        /// </summary>
        private readonly JsonSerializerOptions jsonSerializerOptions;

        /// <summary>
        /// Creates an instance of <see cref="CosmosSystemTextJsonSerializer"/>
        /// with the default values for the Cosmos SDK
        /// </summary>
        /// <param name="jsonSerializerOptions">An instance of <see cref="JsonSerializerOptions"/> containing the json serialization options.</param>
        internal CosmosSystemTextJsonSerializer(
            JsonSerializerOptions jsonSerializerOptions)
        {
            this.jsonSerializerOptions = jsonSerializerOptions;
        }

        /// <inheritdoc/>
        public override T FromStream<T>(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            if (stream.CanSeek && stream.Length == 0)
            {
                return default;
            }

            using (stream)
            {
                using StreamReader reader = new (stream);
                return JsonSerializer.Deserialize<T>(reader.ReadToEnd(), this.jsonSerializerOptions);
            }
        }

        /// <inheritdoc/>
        public override Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new ();
            using Utf8JsonWriter writer = new (streamPayload);

            JsonSerializer.Serialize(writer, input, this.jsonSerializerOptions);

            streamPayload.Position = 0;
            return streamPayload;
        }

        /// <summary>
        /// Convert a MemberInfo to a string for use in LINQ query translation.
        /// </summary>
        /// <param name="memberInfo">Any MemberInfo used in the query.</param>
        /// <returns>A serialized representation of the member.</returns>
        /// <remarks>
        /// Note that this is just a default implementation which handles the basic scenarios.To handle any special cases,
        /// please create a custom serializer which inherits from the <see cref="CosmosSystemTextJsonSerializer"/> and overrides the
        /// SerializeMemberName() method.
        /// </remarks>
        public override string SerializeMemberName(MemberInfo memberInfo)
        {
            JsonExtensionDataAttribute jsonExtensionDataAttribute =
                 memberInfo.GetCustomAttribute<JsonExtensionDataAttribute>(true);

            if (jsonExtensionDataAttribute != null)
            {
                return null;
            }

            JsonPropertyNameAttribute jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);

            if (!string.IsNullOrEmpty(jsonPropertyNameAttribute?.Name))
            {
                return jsonPropertyNameAttribute.Name;
            }

            if (this.jsonSerializerOptions.PropertyNamingPolicy != null)
            {
                return this.jsonSerializerOptions.PropertyNamingPolicy.ConvertName(memberInfo.Name);
            }

            return memberInfo.Name;
        }
    }
}
