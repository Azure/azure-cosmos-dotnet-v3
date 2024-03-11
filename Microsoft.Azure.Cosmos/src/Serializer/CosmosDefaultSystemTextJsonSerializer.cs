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
    /// This class provides a default implementation of STJ Cosmos Linq Serializer.
    /// </summary>
    public class CosmosDefaultSystemTextJsonSerializer : CosmosLinqSerializer
    {
        /// <summary>
        /// A read-only instance of <see cref="JsonObjectSerializer"/>.
        /// </summary>
        private readonly JsonObjectSerializer systemTextJsonSerializer;

        /// <summary>
        /// Creates an instance of <see cref="CosmosDefaultSystemTextJsonSerializer"/>
        /// with the default values for the Cosmos SDK
        /// </summary>
        /// <param name="jsonSerializerOptions">An instance of <see cref="JsonSerializerOptions"/> containing the json serialization options.</param>
        public CosmosDefaultSystemTextJsonSerializer(
            JsonSerializerOptions jsonSerializerOptions)
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
            MemoryStream streamPayload = new ();
            this.systemTextJsonSerializer.Serialize(
                stream: streamPayload,
                value: input,
                inputType: input.GetType(),
                cancellationToken: default);

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

            return memberName;
        }
    }
}
