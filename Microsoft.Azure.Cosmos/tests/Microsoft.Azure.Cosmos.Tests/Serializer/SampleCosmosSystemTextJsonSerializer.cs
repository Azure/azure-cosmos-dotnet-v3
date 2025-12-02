// This is a copy of the sample serializer from:
// Microsoft.Azure.Cosmos.Samples/Usage/SystemTextJson/CosmosSystemTextJsonSerializer.cs
// Used for testing the JsonObjectSerializer-based implementation.

namespace Microsoft.Azure.Cosmos.Tests.Serializer
{
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Uses <see cref="JsonObjectSerializer"/> which leverages System.Text.Json providing a simple API to interact with on the Azure SDKs.
    /// </summary>
    /// <remarks>
    /// For item CRUD operations and non-LINQ queries, implementing CosmosSerializer is sufficient. To support LINQ query translations as well, CosmosLinqSerializer must be implemented.
    /// </remarks>
    public class SampleCosmosSystemTextJsonSerializer : CosmosLinqSerializer
    {
        private readonly JsonObjectSerializer systemTextJsonSerializer;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public SampleCosmosSystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
        {
            this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
            this.jsonSerializerOptions = jsonSerializerOptions;
        }

        public override T FromStream<T>(Stream stream)
        {
            using (stream)
            {
                if (stream.CanSeek
                       && stream.Length == 0)
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

        public override Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            this.systemTextJsonSerializer.Serialize(streamPayload, input, typeof(T), default);
            streamPayload.Position = 0;
            return streamPayload;
        }

        public override string SerializeMemberName(MemberInfo memberInfo)
        {
            JsonExtensionDataAttribute jsonExtensionDataAttribute = memberInfo.GetCustomAttribute<JsonExtensionDataAttribute>(true);
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

            // Do any additional handling of JsonSerializerOptions here.

            return memberInfo.Name;
        }
    }
}
