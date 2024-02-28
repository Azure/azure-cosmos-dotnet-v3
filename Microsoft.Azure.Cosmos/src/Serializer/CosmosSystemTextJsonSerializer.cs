// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using global::Azure.Core.Serialization;

    /// <summary>
    /// This class provides a way to configure Linq Serialization Properties
    /// </summary>
    public class CosmosSystemTextJsonSerializer : CosmosLinqSerializer
    {
        private readonly JsonObjectSerializer systemTextJsonSerializer;

        /// <summary>
        /// Create an instance of <see cref="CosmosSystemTextJsonSerializer"/>
        /// with the default values for the Cosmos SDK
        /// </summary>
        /// <param name="jsonSerializerOptions">options.</param>
        public CosmosSystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
        {
            this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
        }

        /// <inheritdoc/>
        public override T FromStream<T>(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using (stream)
            {
                if (stream.CanSeek && stream.Length == 0)
                {
                    return default;
                }

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)stream;
                }

                return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
            }
        }

        /// <inheritdoc/>
        public override Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            this.systemTextJsonSerializer.Serialize(streamPayload, input, input.GetType(), default);
            streamPayload.Position = 0;
            return streamPayload;
        }

        /// <inheritdoc/>
        public override string SerializeMemberName(MemberInfo memberInfo)
        {
            JsonExtensionDataAttribute jsonExtensionDataAttribute =
                 memberInfo.GetCustomAttribute<JsonExtensionDataAttribute>(true);
            if (jsonExtensionDataAttribute != null)
            {
                return null;
            }

            JsonPropertyNameAttribute jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);

            string memberName = !string.IsNullOrEmpty(jsonPropertyNameAttribute?.Name)
                ? jsonPropertyNameAttribute.Name
                : memberInfo.Name;

            // Users must add handling for any additional attributes here

            return memberName;
        }
    }
}
