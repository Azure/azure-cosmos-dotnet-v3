//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Globalization;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    /// Placeholder for VST Logger.
    /// </summary>
    internal class CosmosSerializerHelper : CosmosSerializer
    {
        private readonly CosmosSerializer cosmosSerializer;
        private readonly Action<dynamic> fromStreamCallback;
        private readonly Action<dynamic> toStreamCallBack;

        public CosmosSerializerHelper(
            JsonSerializerSettings jsonSerializerSettings,
            Action<dynamic> fromStreamCallback,
            Action<dynamic> toStreamCallBack)
        {
            this.cosmosSerializer = jsonSerializerSettings == null ? new CosmosJsonDotNetSerializer() : (CosmosSerializer)new CosmosJsonDotNetSerializer(jsonSerializerSettings);

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

        public sealed class FormatNumbersAsTextConverter : JsonConverter
        {
            public override bool CanRead => false;
            public override bool CanWrite => true;
            public override bool CanConvert(Type type)
            {
                return type == typeof(int) || type == typeof(double);
            }

            public override void WriteJson(
                JsonWriter writer,
                object value,
                JsonSerializer serializer)
            {
                if (value.GetType() == typeof(int))
                {
                    int number = (int)value;
                    writer.WriteValue(number.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    double number = (double)value;
                    writer.WriteValue(number.ToString(CultureInfo.InvariantCulture));
                }

            }

            public override object ReadJson(
                JsonReader reader,
                Type type,
                object existingValue,
                JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
    }
}