//-----------------------------------------------------------------------
// <copyright file="JsonNewtonsoftReader.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Newtonsoft.Json;

    /// <summary>
    /// IJsonReader adapter for a Newtonsoft reader meaning we get a newtonsoft reader for testing purposes.
    /// </summary>
    internal abstract class JsonNewtonsoftReader : Microsoft.Azure.Cosmos.Json.JsonReader
    {
        private readonly Newtonsoft.Json.JsonReader reader;

        protected JsonNewtonsoftReader(Newtonsoft.Json.JsonReader reader)
            : base(true)
        {
            this.reader = reader;
        }

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
                numberString = ((double)value).ToString("R");
            }
            else
            {
                numberString = value.ToString();
            }

            return double.Parse(numberString);
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

            return succesfullyRead;
        }
    }
}
