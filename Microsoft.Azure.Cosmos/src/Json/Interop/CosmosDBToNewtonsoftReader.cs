//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.Interop
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Wrapper class that implements a Newtonsoft JsonReader,
    /// but forwards all the calls to a CosmosDB JSON reader.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    sealed class CosmosDBToNewtonsoftReader : Newtonsoft.Json.JsonReader
    {
        /// <summary>
        /// Singleton boxed value for null.
        /// </summary>
        private static readonly object Null = null;

        /// <summary>
        /// Singleton boxed value for false.
        /// </summary>
        private static readonly object False = false;

        /// <summary>
        /// Singleton boxed value for true.
        /// </summary>
        private static readonly object True = true;

        /// <summary>
        /// The CosmosDB JSON Reader that will be used for implementation.
        /// </summary>
        private readonly IJsonReader jsonReader;

        /// <summary>
        /// Initializes a new instance of the NewtonsoftReader class.
        /// </summary>
        /// <param name="jsonReader">The reader to interop with.</param>
        public CosmosDBToNewtonsoftReader(IJsonReader jsonReader)
        {
            this.jsonReader = jsonReader ?? throw new ArgumentNullException(nameof(jsonReader));
        }

        /// <summary>
        /// Reads the next token from the reader.
        /// </summary>
        /// <returns>True if a token was read, else false.</returns>
        public override bool Read()
        {
            bool read = this.jsonReader.Read();
            if (!read)
            {
                this.SetToken(JsonToken.None);
                return false;
            }

            JsonTokenType jsonTokenType = this.jsonReader.CurrentTokenType;
            JsonToken newtonsoftToken;
            object value;
            switch (jsonTokenType)
            {
                case JsonTokenType.BeginArray:
                    newtonsoftToken = JsonToken.StartArray;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.EndArray:
                    newtonsoftToken = JsonToken.EndArray;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.BeginObject:
                    newtonsoftToken = JsonToken.StartObject;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.EndObject:
                    newtonsoftToken = JsonToken.EndObject;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.String:
                    newtonsoftToken = JsonToken.String;
                    value = this.jsonReader.GetStringValue().ToString();
                    break;

                case JsonTokenType.Number:
                    Number64 number64Value = this.jsonReader.GetNumberValue();
                    if (number64Value.IsInteger)
                    {
                        value = Number64.ToLong(number64Value);
                        newtonsoftToken = JsonToken.Integer;
                    }
                    else
                    {
                        value = Number64.ToDouble(number64Value);
                        newtonsoftToken = JsonToken.Float;
                    }
                    break;

                case JsonTokenType.True:
                    newtonsoftToken = JsonToken.Boolean;
                    value = CosmosDBToNewtonsoftReader.True;
                    break;

                case JsonTokenType.False:
                    newtonsoftToken = JsonToken.Boolean;
                    value = CosmosDBToNewtonsoftReader.False;
                    break;

                case JsonTokenType.Null:
                    newtonsoftToken = JsonToken.Null;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.FieldName:
                    newtonsoftToken = JsonToken.PropertyName;
                    value = this.jsonReader.GetStringValue().ToString();
                    break;

                case JsonTokenType.Int8:
                    newtonsoftToken = JsonToken.Integer;
                    value = this.jsonReader.GetInt8Value();
                    break;

                case JsonTokenType.Int16:
                    newtonsoftToken = JsonToken.Integer;
                    value = this.jsonReader.GetInt16Value();
                    break;

                case JsonTokenType.Int32:
                    newtonsoftToken = JsonToken.Integer;
                    value = this.jsonReader.GetInt32Value();
                    break;

                case JsonTokenType.Int64:
                    newtonsoftToken = JsonToken.Integer;
                    value = this.jsonReader.GetInt64Value();
                    break;

                case JsonTokenType.UInt32:
                    newtonsoftToken = JsonToken.Integer;
                    value = this.jsonReader.GetUInt32Value();
                    break;

                case JsonTokenType.Float32:
                    newtonsoftToken = JsonToken.Float;
                    value = this.jsonReader.GetFloat32Value();
                    break;

                case JsonTokenType.Float64:
                    newtonsoftToken = JsonToken.Float;
                    value = this.jsonReader.GetFloat64Value();
                    break;

                case JsonTokenType.Guid:
                    newtonsoftToken = JsonToken.String;
                    value = this.jsonReader.GetGuidValue().ToString();
                    break;

                case JsonTokenType.Binary:
                    newtonsoftToken = JsonToken.Bytes;
                    value = this.jsonReader.GetBinaryValue().ToArray();
                    break;

                default:
                    throw new ArgumentException($"Unexpected jsonTokenType: {jsonTokenType}");
            }

            this.SetToken(newtonsoftToken, value);
            return read;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="byte"/>[].
        /// </summary>
        /// <returns>A <see cref="byte"/>[] or <c>null</c> if the next JSON token is null. This method will return <c>null</c> at the end of an array.</returns>
        public override byte[] ReadAsBytes()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Nullable{T}"/> of <see cref="DateTime"/>.
        /// </summary>
        /// <returns>A <see cref="Nullable{T}"/> of <see cref="DateTime"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override DateTime? ReadAsDateTime()
        {
            this.Read();
            if (this.jsonReader.CurrentTokenType == JsonTokenType.Null || this.jsonReader.CurrentTokenType == JsonTokenType.EndArray)
            {
                return null;
            }

            string stringValue = this.jsonReader.GetStringValue();
            DateTime dateTime = DateTime.Parse(stringValue);
            this.SetToken(JsonToken.Date, dateTime);

            return dateTime;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <returns>A <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override DateTimeOffset? ReadAsDateTimeOffset()
        {
            this.Read();
            if (this.jsonReader.CurrentTokenType == JsonTokenType.Null || this.jsonReader.CurrentTokenType == JsonTokenType.EndArray)
            {
                return null;
            }

            string stringValue = this.jsonReader.GetStringValue();
            DateTimeOffset dateTimeOffset = DateTimeOffset.Parse(stringValue);
            this.SetToken(JsonToken.Date, dateTimeOffset);

            return dateTimeOffset;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Nullable{T}"/> of <see cref="decimal"/>.
        /// </summary>
        /// <returns>A <see cref="Nullable{T}"/> of <see cref="decimal"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override decimal? ReadAsDecimal()
        {
            decimal? value = (decimal?)this.ReadNumberValue();
            if (value != null)
            {
                this.SetToken(JsonToken.Float, value);
            }

            return value;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Nullable{T}"/> of <see cref="int"/>.
        /// </summary>
        /// <returns>A <see cref="Nullable{T}"/> of <see cref="int"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override int? ReadAsInt32()
        {
            int? value = (int?)this.ReadNumberValue();
            if (value != null)
            {
                this.SetToken(JsonToken.Integer, value);
            }

            return value;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="string"/>.
        /// </summary>
        /// <returns>A <see cref="string"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override string ReadAsString()
        {
            this.Read();
            if (this.jsonReader.CurrentTokenType == JsonTokenType.Null || this.jsonReader.CurrentTokenType == JsonTokenType.EndArray)
            {
                return null;
            }

            string stringValue = this.jsonReader.GetStringValue();
            this.SetToken(JsonToken.String, stringValue);

            return stringValue;
        }

        /// <summary>
        /// Reads the next number token but returns null at the end of an array.
        /// </summary>
        /// <returns>The next number token but returns null at the end of an array.</returns>
        private double? ReadNumberValue()
        {
            this.Read();
            if (this.jsonReader.CurrentTokenType == JsonTokenType.Null || this.jsonReader.CurrentTokenType == JsonTokenType.EndArray)
            {
                return null;
            }

            Number64 value = this.jsonReader.GetNumberValue();
            double doubleValue = Number64.ToDouble(value);
            return doubleValue;
        }
    }
}
