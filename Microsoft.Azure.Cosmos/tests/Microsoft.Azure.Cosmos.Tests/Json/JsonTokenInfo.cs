//-----------------------------------------------------------------------
// <copyright file="JsonTokenInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;

    internal struct JsonTokenInfo
    {
        public JsonTokenInfo(JsonTokenType jsonTokenType)
            : this(jsonTokenType, 0, null, false)
        {
        }

        public JsonTokenInfo(double doubleValue)
            : this(JsonTokenType.Number, doubleValue, null, false)
        {
        }

        public JsonTokenInfo(JsonTokenType jsonTokenType, IReadOnlyList<byte> bufferedToken)
            : this(jsonTokenType, 0, bufferedToken, false)
        {
        }

        public JsonTokenInfo(JsonTokenType jsonTokenType, double value, IReadOnlyList<byte> bufferedToken, bool unicode)
        {
            this.JsonTokenType = jsonTokenType;
            this.Value = value;
            this.BufferedToken = bufferedToken;
            this.Unicode = unicode;
        }

        public JsonTokenType JsonTokenType { get; }

        public double Value { get; }

        public IReadOnlyList<byte> BufferedToken { get; }

        public bool Unicode { get; }

        public override string ToString()
        {
            switch (this.JsonTokenType)
            {
                case JsonTokenType.BeginArray:
                    return "[";
                case JsonTokenType.EndArray:
                    return "]";
                case JsonTokenType.BeginObject:
                    return "{";
                case JsonTokenType.EndObject:
                    return "}";
                case JsonTokenType.String:
                    return Encoding.UTF8.GetString(this.BufferedToken.ToArray());
                case JsonTokenType.Number:
                    return this.Value.ToString(CultureInfo.InvariantCulture);
                case JsonTokenType.True:
                    return "true";
                case JsonTokenType.False:
                    return "false";
                case JsonTokenType.Null:
                    return "null";
                case JsonTokenType.FieldName:
                    return $"{Encoding.UTF8.GetString(this.BufferedToken.ToArray())}:";
                default:
                    throw new InvalidOperationException($"{this.JsonTokenType} is not a valid json token type.");
            }
        }

        public static JsonTokenInfo ArrayStart()
        {
            return new JsonTokenInfo(JsonTokenType.BeginArray);
        }

        public static JsonTokenInfo ArrayEnd()
        {
            return new JsonTokenInfo(JsonTokenType.EndArray);
        }

        public static JsonTokenInfo ObjectStart()
        {
            return new JsonTokenInfo(JsonTokenType.BeginObject);
        }

        public static JsonTokenInfo ObjectEnd()
        {
            return new JsonTokenInfo(JsonTokenType.EndObject);
        }

        public static JsonTokenInfo Number(double number)
        {
            return new JsonTokenInfo(number);
        }

        public static JsonTokenInfo Boolean(bool value)
        {
            return new JsonTokenInfo(value ? JsonTokenType.True : JsonTokenType.False);
        }

        public static JsonTokenInfo Null()
        {
            return new JsonTokenInfo(JsonTokenType.Null);
        }

        public static JsonTokenInfo String(string value)
        {
            return new JsonTokenInfo(JsonTokenType.String, Encoding.Unicode.GetBytes("\"" + value + "\""));
        }

        public static JsonTokenInfo FieldName(string name)
        {
            return new JsonTokenInfo(JsonTokenType.FieldName, Encoding.Unicode.GetBytes("\"" + name + "\""));
        }

        public bool Equals(JsonTokenInfo other)
        {
            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            // If the token types don't match then we know they aren't equal
            if (this.JsonTokenType != other.JsonTokenType)
            {
                return false;
            }

            // If the token is a string, fieldname or number then we have to check the values
            switch (this.JsonTokenType)
            {
                case JsonTokenType.NotStarted:
                case JsonTokenType.BeginArray:
                case JsonTokenType.EndArray:
                case JsonTokenType.BeginObject:
                case JsonTokenType.EndObject:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    return true;
                case JsonTokenType.String:
                case JsonTokenType.FieldName:
                    return this.BufferedToken.SequenceEqual(other.BufferedToken);
                case JsonTokenType.Number:
                    return this.Value == other.Value;
                default:
                    throw new ArgumentException("Invalid token type");
            }
        }

        public override bool Equals(object obj)
        {
            return this.Equals((JsonTokenInfo)obj);
        }

        public override int GetHashCode()
        {
            switch (this.JsonTokenType)
            {
                case JsonTokenType.NotStarted:
                case JsonTokenType.BeginArray:
                case JsonTokenType.EndArray:
                case JsonTokenType.BeginObject:
                case JsonTokenType.EndObject:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    return (int)this.JsonTokenType;
                case JsonTokenType.String:
                case JsonTokenType.FieldName:
                    return Encoding.UTF8.GetString(this.BufferedToken.ToArray()).GetHashCode();
                case JsonTokenType.Number:
                    return this.Value.GetHashCode();
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
