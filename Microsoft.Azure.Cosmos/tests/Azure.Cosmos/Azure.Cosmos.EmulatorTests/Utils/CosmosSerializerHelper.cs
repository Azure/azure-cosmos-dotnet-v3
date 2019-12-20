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
    using Azure.Cosmos;
    using Azure.Cosmos.Serialization;

    /// <summary>
    /// Placeholder for VST Logger.
    /// </summary>
    internal class CosmosSerializerHelper : CosmosSerializer
    {
        private readonly CosmosSerializer cosmosSerializer = TestCommon.Serializer;
        private readonly Action<dynamic> fromStreamCallback;
        private readonly Action<dynamic> toStreamCallBack;

        public CosmosSerializerHelper(
            JsonSerializerOptions jsonSerializerSettings,
            Action<dynamic> fromStreamCallback,
            Action<dynamic> toStreamCallBack)
        {
            if (jsonSerializerSettings == null)
            {
                this.cosmosSerializer = TestCommon.Serializer;
            }
            else
            {
                this.cosmosSerializer = new CosmosTextJsonSerializer(jsonSerializerSettings);
            }

            this.fromStreamCallback = fromStreamCallback;
            this.toStreamCallBack = toStreamCallBack;
        }

        public override T FromStream<T>(Stream stream)
        {
            T item = this.cosmosSerializer.FromStream<T>(stream);
            this.fromStreamCallback?.Invoke(item);

            return item;
        }

        public override Stream ToStream<T>(T input)
        {
            this.toStreamCallBack?.Invoke(input);
            return this.cosmosSerializer.ToStream<T>(input);
        }

        public sealed class FormatNumbersTextConverter : JsonConverter<int>
        {
            public override bool CanConvert(Type type)
            {
                return type == typeof(int);
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value);

            }

            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }

        public sealed class FormatDoubleAsTextConverter : JsonConverter<double>
        {
            public override bool CanConvert(Type type)
            {
                return type == typeof(double);
            }

            public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value);

            }

            public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }
        }
    }
}
