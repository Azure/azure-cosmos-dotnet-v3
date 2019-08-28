//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.NewtonsoftInterop
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Newtonsoft.Json;

    internal sealed class JsonNewtonsoftReader : Microsoft.Azure.Cosmos.Json.JsonReader
    {
        private readonly Newtonsoft.Json.JsonReader reader;

        private JsonNewtonsoftReader(Newtonsoft.Json.JsonReader reader)
            : base(true)
        {
            if (reader == null)
            {
                throw new ArgumentNullException($"{nameof(reader)}");
            }

            this.reader = reader;
        }

        public static JsonNewtonsoftReader Create(Newtonsoft.Json.JsonReader reader)
        {
            return new JsonNewtonsoftReader(reader);
        }

        public override JsonSerializationFormat SerializationFormat => this.reader is Newtonsoft.Json.JsonTextReader ? JsonSerializationFormat.Text : JsonSerializationFormat.Binary;

        public override IReadOnlyList<byte> GetBufferedRawJsonToken()
        {
            throw new NotImplementedException();
        }

        public override double GetNumberValue()
        {
            string numberString;
            object value = this.reader.Value;
            if (value is double)
            {
                numberString = ((double)value).ToString("R", CultureInfo.InvariantCulture);
            }
            else
            {
                numberString = value.ToString();
            }

            return double.Parse(numberString, CultureInfo.InvariantCulture);
        }

        public override string GetStringValue()
        {
            return this.reader.Value.ToString();
        }

        public override bool Read()
        {
            bool succesfullyRead = this.reader.Read();
            if (succesfullyRead)
            {
                this.RegisterToken();
            }

            return succesfullyRead;
        }

        private void RegisterToken()
        {
            switch (this.reader.TokenType)
            {
                case JsonToken.None:
                case JsonToken.StartConstructor:
                case JsonToken.EndConstructor:
                    throw new InvalidOperationException();
                case JsonToken.StartObject:
                    this.JsonObjectState.RegisterToken(JsonTokenType.BeginObject);
                    break;
                case JsonToken.StartArray:
                    this.JsonObjectState.RegisterToken(JsonTokenType.BeginArray);
                    break;
                case JsonToken.PropertyName:
                    this.JsonObjectState.RegisterToken(JsonTokenType.FieldName);
                    break;
                case JsonToken.Comment:
                case JsonToken.Raw:
                case JsonToken.String:
                case JsonToken.Date:
                case JsonToken.Bytes:
                    this.JsonObjectState.RegisterToken(JsonTokenType.String);
                    break;
                case JsonToken.Integer:
                case JsonToken.Float:
                    this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                    break;
                case JsonToken.Boolean:
                    this.JsonObjectState.RegisterToken(this.reader.Value.ToString() == true.ToString() ? JsonTokenType.True : JsonTokenType.False);
                    break;
                case JsonToken.Null:
                case JsonToken.Undefined:
                    this.JsonObjectState.RegisterToken(JsonTokenType.Null);
                    break;
                case JsonToken.EndObject:
                    this.JsonObjectState.RegisterToken(JsonTokenType.EndObject);
                    break;
                case JsonToken.EndArray:
                    this.JsonObjectState.RegisterToken(JsonTokenType.EndArray);
                    break;
                default:
                    throw new ArgumentException("Got an invalid newtonsoft type");
            }
        }
    }
}
