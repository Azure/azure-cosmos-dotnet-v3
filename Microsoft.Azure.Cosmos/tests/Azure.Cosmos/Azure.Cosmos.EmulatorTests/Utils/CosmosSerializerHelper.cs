//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    internal class CosmosSerializerHelper : Azure.Core.ObjectSerializer
    {
        private readonly Azure.Core.ObjectSerializer cosmosSerializer;
        private readonly Action<dynamic> fromStreamCallback;
        private readonly Action<dynamic> toStreamCallBack;

        public CosmosSerializerHelper(
            JsonSerializerOptions jsonSerializerSettings,
            Action<dynamic> fromStreamCallback,
            Action<dynamic> toStreamCallBack)
        {
            this.cosmosSerializer = new Azure.Core.JsonObjectSerializer(jsonSerializerSettings);
            this.fromStreamCallback = fromStreamCallback;
            this.toStreamCallBack = toStreamCallBack;
        }

        public override object Deserialize(Stream stream, Type returnType)
        {
            object item = this.cosmosSerializer.Deserialize(stream, returnType);
            this.fromStreamCallback?.Invoke(item);
            return item;
        }

        public override async ValueTask<object> DeserializeAsync(Stream stream, Type returnType)
        {
            object item = await this.cosmosSerializer.DeserializeAsync(stream, returnType);
            this.fromStreamCallback?.Invoke(item);
            return item;
        }

        public override void Serialize(Stream stream, object value, Type inputType)
        {
            this.toStreamCallBack?.Invoke(value);
            this.cosmosSerializer.Serialize(stream, value, inputType);
        }

        public override ValueTask SerializeAsync(Stream stream, object value, Type inputType)
        {
            this.toStreamCallBack?.Invoke(value);
            return this.cosmosSerializer.SerializeAsync(stream, value, inputType);
        }

        public sealed class FormatNumbersTextConverter : JsonConverter<int>
        {
            public override bool CanConvert(Type type)
            {
                return type == typeof(int);
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }

            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        public sealed class FormatDoubleAsTextConverter : JsonConverter<double>
        {
            public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }

            public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }
    }
}
