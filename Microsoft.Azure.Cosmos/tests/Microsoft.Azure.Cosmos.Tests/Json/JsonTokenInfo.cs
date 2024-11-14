//-----------------------------------------------------------------------
// <copyright file="JsonTokenInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract class JsonToken
    {
        public JsonTokenType JsonTokenType { get; }
        public bool IsNumberArray { get; }

        protected JsonToken(JsonTokenType jsonTokenType, bool isNumberArray = default)
        {
            this.JsonTokenType = jsonTokenType;
            this.IsNumberArray = isNumberArray;
        }

        public static JsonToken ArrayStart()
        {
            return new JsonStartArrayToken();
        }

        public static JsonToken ArrayEnd()
        {
            return new JsonEndArrayToken();
        }

        public static JsonToken ObjectStart()
        {
            return new JsonStartObjectToken();
        }

        public static JsonToken ObjectEnd()
        {
            return new JsonEndObjectToken();
        }

        public static JsonToken Number(Number64 number)
        {
            return new JsonNumberToken(number);
        }

        public static JsonToken Boolean(bool value)
        {
            return value ? (JsonToken)new JsonTrueToken() : (JsonToken)new JsonFalseToken();
        }

        public static JsonToken Null()
        {
            return new JsonNullToken();
        }

        public static JsonToken String(string value)
        {
            return new JsonStringToken(value);
        }

        public static JsonToken FieldName(string name)
        {
            return new JsonFieldNameToken(name);
        }

        public static JsonToken Int8(sbyte value)
        {
            return new JsonInt8Token(value);
        }

        public static JsonToken Int16(short value)
        {
            return new JsonInt16Token(value);
        }

        public static JsonToken Int32(int value)
        {
            return new JsonInt32Token(value);
        }

        public static JsonToken Int64(long value)
        {
            return new JsonInt64Token(value);
        }

        public static JsonToken UInt32(uint value)
        {
            return new JsonUInt32Token(value);
        }

        public static JsonToken Float32(float value)
        {
            return new JsonFloat32Token(value);
        }

        public static JsonToken Float64(double value)
        {
            return new JsonFloat64Token(value);
        }

        public static JsonToken Guid(Guid value)
        {
            return new JsonGuidToken(value);
        }

        public static JsonToken Binary(ReadOnlyMemory<byte> value)
        {
            return new JsonBinaryToken(value);
        }

        public static JsonToken UInt8NumberArray(IReadOnlyList<byte> values)
        {
            return new JsonNumberArrayToken<byte>(JsonTokenType.UInt8, values);
        }

        public static JsonToken Int8NumberArray(IReadOnlyList<sbyte> values)
        {
            return new JsonNumberArrayToken<sbyte>(JsonTokenType.Int8, values);
        }

        public static JsonToken Int16NumberArray(IReadOnlyList<short> values)
        {
            return new JsonNumberArrayToken<short>(JsonTokenType.Int16, values);
        }

        public static JsonToken Int32NumberArray(IReadOnlyList<int> values)
        {
            return new JsonNumberArrayToken<int>(JsonTokenType.Int32, values);
        }

        public static JsonToken Int64NumberArray(IReadOnlyList<long> values)
        {
            return new JsonNumberArrayToken<long>(JsonTokenType.Int64, values);
        }

        public static JsonToken Float32NumberArray(IReadOnlyList<float> values)
        {
            return new JsonNumberArrayToken<float>(JsonTokenType.Float32, values);
        }

        public static JsonToken Float64NumberArray(IReadOnlyList<double> values)
        {
            return new JsonNumberArrayToken<double>(JsonTokenType.Float64, values);
        }
    }

    internal sealed class JsonStartArrayToken : JsonToken
    {
        public JsonStartArrayToken()
            : base(JsonTokenType.BeginArray)
        {
        }

        public override bool Equals(object obj)
        {
            return obj is JsonStartArrayToken;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonEndArrayToken : JsonToken
    {
        public JsonEndArrayToken()
            : base(JsonTokenType.EndArray)
        {
        }

        public override bool Equals(object obj)
        {
            return obj is JsonEndArrayToken;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonStartObjectToken : JsonToken
    {
        public JsonStartObjectToken()
            : base(JsonTokenType.BeginObject)
        {
        }

        public override bool Equals(object obj)
        {
            return obj is JsonStartObjectToken;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonEndObjectToken : JsonToken
    {
        public JsonEndObjectToken()
            : base(JsonTokenType.EndObject)
        {
        }

        public override bool Equals(object obj)
        {
            return obj is JsonEndObjectToken;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonStringToken : JsonToken
    {
        public JsonStringToken(string value)
            : base(JsonTokenType.String)
        {
            this.Value = value;
        }

        public string Value
        {
            get;
        }

        public override bool Equals(object obj)
        {
            if (obj is JsonStringToken jsonStringToken)
            {
                return this.Value == jsonStringToken.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonNumberToken : JsonToken
    {
        public JsonNumberToken(Number64 value)
            : base(JsonTokenType.Number)
        {
            this.Value = value;
        }

        public Number64 Value
        {
            get;
        }

        public override bool Equals(object obj)
        {
            if (obj is JsonNumberToken other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonTrueToken : JsonToken
    {
        public JsonTrueToken()
            : base(JsonTokenType.True)
        {
        }

        public override bool Equals(object obj)
        {
            return obj is JsonTrueToken;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonFalseToken : JsonToken
    {
        public JsonFalseToken()
            : base(JsonTokenType.False)
        {
        }

        public override bool Equals(object obj)
        {
            return obj is JsonFalseToken;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonNullToken : JsonToken
    {
        public JsonNullToken()
            : base(JsonTokenType.Null)
        {
        }

        public override bool Equals(object obj)
        {
            return obj is JsonNullToken;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonFieldNameToken : JsonToken
    {
        public JsonFieldNameToken(string fieldName)
            : base(JsonTokenType.FieldName)
        {
            this.Value = fieldName;
        }

        public string Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonFieldNameToken other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonInt8Token : JsonToken
    {
        public JsonInt8Token(sbyte value)
            : base(JsonTokenType.Int8)
        {
            this.Value = value;
        }

        public sbyte Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonInt8Token other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonInt16Token : JsonToken
    {
        public JsonInt16Token(short value)
            : base(JsonTokenType.Int16)
        {
            this.Value = value;
        }

        public short Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonInt16Token other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonInt32Token : JsonToken
    {
        public JsonInt32Token(int value)
            : base(JsonTokenType.Int32)
        {
            this.Value = value;
        }

        public int Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonInt32Token other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonInt64Token : JsonToken
    {
        public JsonInt64Token(long value)
            : base(JsonTokenType.Int64)
        {
            this.Value = value;
        }

        public long Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonInt64Token other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonUInt32Token : JsonToken
    {
        public JsonUInt32Token(uint value)
            : base(JsonTokenType.UInt32)
        {
            this.Value = value;
        }

        public uint Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonUInt32Token other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonFloat32Token : JsonToken
    {
        public JsonFloat32Token(float value)
            : base(JsonTokenType.Float32)
        {
            this.Value = value;
        }

        public float Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonFloat32Token other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonFloat64Token : JsonToken
    {
        public JsonFloat64Token(double value)
            : base(JsonTokenType.Float64)
        {
            this.Value = value;
        }

        public double Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonFloat64Token other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonGuidToken : JsonToken
    {
        public JsonGuidToken(Guid value)
            : base(JsonTokenType.Guid)
        {
            this.Value = value;
        }

        public Guid Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonGuidToken other)
            {
                return this.Value == other.Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonBinaryToken : JsonToken
    {
        public JsonBinaryToken(ReadOnlyMemory<byte> value)
            : base(JsonTokenType.Binary)
        {
            this.Value = value;
        }

        public ReadOnlyMemory<byte> Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonBinaryToken other)
            {
                return this.Value.Span.SequenceEqual(other.Value.Span);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    internal sealed class JsonNumberArrayToken<T> : JsonToken
    {
        public JsonNumberArrayToken(JsonTokenType jsonTokenType, IReadOnlyList<T> values)
            : base(jsonTokenType, isNumberArray: true)
        {
            this.Values = values;
        }

        public IReadOnlyList<T> Values { get; }

        public override bool Equals(object obj)
        {
            if (obj is JsonNumberArrayToken<T> other)
            {
                return this.Values.Equals(other.Values);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }
}