//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core;
    using RMResources = Documents.RMResources;

    /// <summary>
    /// Base abstract class for JSON writers.
    /// The writer defines methods that allow for writing a JSON encoded value to a buffer.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonWriter : IJsonWriter
    {
        /// <summary>
        /// The <see cref="JsonObjectState"/>
        /// </summary>
        internal readonly JsonObjectState JsonObjectState;

        /// <summary>
        /// Whether to skip validation.
        /// </summary>
        protected readonly bool SkipValidation;

        /// <summary>
        /// Initializes a new instance of the JsonWriter class.
        /// </summary>
        /// <param name="skipValidation">Whether or not to skip validation.</param>
        protected JsonWriter(bool skipValidation)
        {
            this.JsonObjectState = new JsonObjectState(false);
            this.SkipValidation = skipValidation;
        }

        /// <inheritdoc />
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <inheritdoc />
        public abstract long CurrentLength { get; }

        /// <summary>
        /// Creates a JsonWriter that can write in a particular JsonSerializationFormat (utf8 if text)
        /// </summary>
        /// <param name="jsonSerializationFormat">The JsonSerializationFormat of the writer.</param>
        /// <param name="jsonStringDictionary">The dictionary to use for user string encoding.</param>
        /// <param name="skipValidation">Whether or not to skip validation</param>
        /// <returns>A JsonWriter that can write in a particular JsonSerializationFormat</returns>
        public static IJsonWriter Create(
            JsonSerializationFormat jsonSerializationFormat,
            JsonStringDictionary jsonStringDictionary = null,
            bool skipValidation = false)
        {
            switch (jsonSerializationFormat)
            {
                case JsonSerializationFormat.Text:
                    return new JsonTextWriter(skipValidation);
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryWriter(
                        skipValidation,
                        jsonStringDictionary,
                        serializeCount: false);
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, RMResources.UnexpectedJsonSerializationFormat, jsonSerializationFormat));
            }
        }

        /// <inheritdoc />
        public abstract void WriteObjectStart();

        /// <inheritdoc />
        public abstract void WriteObjectEnd();

        /// <inheritdoc />
        public abstract void WriteArrayStart();

        /// <inheritdoc />
        public abstract void WriteArrayEnd();

        /// <inheritdoc />
        public abstract void WriteFieldName(string fieldName);

        /// <inheritdoc />
        public abstract void WriteFieldName(ReadOnlySpan<byte> utf8FieldName);

        /// <inheritdoc />
        public abstract void WriteStringValue(string value);

        /// <inheritdoc />
        public abstract void WriteStringValue(ReadOnlySpan<byte> utf8StringValue);

        /// <inheritdoc />
        public abstract void WriteNumber64Value(Number64 value);

        /// <inheritdoc />
        public abstract void WriteBoolValue(bool value);

        /// <inheritdoc />
        public abstract void WriteNullValue();

        /// <inheritdoc />
        public abstract void WriteInt8Value(sbyte value);

        /// <inheritdoc />
        public abstract void WriteInt16Value(short value);

        /// <inheritdoc />
        public abstract void WriteInt32Value(int value);

        /// <inheritdoc />
        public abstract void WriteInt64Value(long value);

        /// <inheritdoc />
        public abstract void WriteFloat32Value(float value);

        /// <inheritdoc />
        public abstract void WriteFloat64Value(double value);

        /// <inheritdoc />
        public abstract void WriteUInt32Value(uint value);

        /// <inheritdoc />
        public abstract void WriteGuidValue(Guid value);

        /// <inheritdoc />
        public abstract void WriteBinaryValue(ReadOnlySpan<byte> value);

        /// <summary>
        /// Writes current token from a json reader to the internal buffer.
        /// </summary>
        /// <param name="jsonReader">The JsonReader to the get the current token from.</param>
        public void WriteCurrentToken(IJsonReader jsonReader)
        {
            if (jsonReader == null)
            {
                throw new ArgumentNullException("jsonReader can not be null");
            }

            bool sameFormat = jsonReader.SerializationFormat == this.SerializationFormat;

            JsonTokenType jsonTokenType = jsonReader.CurrentTokenType;
            switch (jsonTokenType)
            {
                case JsonTokenType.NotStarted:
                    break;

                case JsonTokenType.BeginArray:
                    this.WriteArrayStart();
                    break;

                case JsonTokenType.EndArray:
                    this.WriteArrayEnd();
                    break;

                case JsonTokenType.BeginObject:
                    this.WriteObjectStart();
                    break;

                case JsonTokenType.EndObject:
                    this.WriteObjectEnd();
                    break;

                case JsonTokenType.True:
                    this.WriteBoolValue(true);
                    break;

                case JsonTokenType.False:
                    this.WriteBoolValue(false);
                    break;

                case JsonTokenType.Null:
                    this.WriteNullValue();
                    break;

                default:
                    {
                        if (sameFormat && jsonReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                        {
                            this.WriteRawJsonToken(jsonTokenType, bufferedRawJsonToken.Span);
                        }
                        else
                        {
                            switch (jsonTokenType)
                            {
                                case JsonTokenType.String:
                                case JsonTokenType.FieldName:
                                    {
                                        string value = jsonReader.GetStringValue();
                                        if (jsonTokenType == JsonTokenType.FieldName)
                                        {
                                            this.WriteFieldName(value);
                                        }
                                        else
                                        {
                                            this.WriteStringValue(value);
                                        }

                                        break;
                                    }

                                case JsonTokenType.Number:
                                    {
                                        Number64 value = jsonReader.GetNumberValue();
                                        this.WriteNumber64Value(value);
                                    }
                                    break;

                                case JsonTokenType.Int8:
                                    {
                                        sbyte value = jsonReader.GetInt8Value();
                                        this.WriteInt8Value(value);
                                    }
                                    break;

                                case JsonTokenType.Int16:
                                    {
                                        short value = jsonReader.GetInt16Value();
                                        this.WriteInt16Value(value);
                                    }
                                    break;

                                case JsonTokenType.Int32:
                                    {
                                        int value = jsonReader.GetInt32Value();
                                        this.WriteInt32Value(value);
                                    }
                                    break;

                                case JsonTokenType.Int64:
                                    {
                                        long value = jsonReader.GetInt64Value();
                                        this.WriteInt64Value(value);
                                    }
                                    break;

                                case JsonTokenType.UInt32:
                                    {
                                        uint value = jsonReader.GetUInt32Value();
                                        this.WriteUInt32Value(value);
                                    }
                                    break;

                                case JsonTokenType.Float32:
                                    {
                                        float value = jsonReader.GetFloat32Value();
                                        this.WriteFloat32Value(value);
                                    }
                                    break;

                                case JsonTokenType.Float64:
                                    {
                                        double value = jsonReader.GetFloat64Value();
                                        this.WriteFloat64Value(value);
                                    }
                                    break;

                                case JsonTokenType.Guid:
                                    {
                                        Guid value = jsonReader.GetGuidValue();
                                        this.WriteGuidValue(value);
                                    }
                                    break;

                                case JsonTokenType.Binary:
                                    {
                                        ReadOnlyMemory<byte> value = jsonReader.GetBinaryValue();
                                        this.WriteBinaryValue(value.Span);
                                    }
                                    break;

                                default:
                                    throw new ArgumentException($"Unexpected JsonTokenType: {jsonTokenType}");
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Writes every token from the JsonReader to the internal buffer.
        /// </summary>
        /// <param name="jsonReader">The JsonReader to get the tokens from.</param>
        public void WriteAll(IJsonReader jsonReader)
        {
            if (jsonReader == null)
            {
                throw new ArgumentNullException("jsonReader can not be null");
            }

            while (jsonReader.Read())
            {
                this.WriteCurrentToken(jsonReader);
            }
        }

        /// <summary>
        /// Writes a fragment of a json to the internal buffer
        /// </summary>
        /// <param name="jsonFragment">A section of a valid json</param>
        public void WriteJsonFragment(ReadOnlyMemory<byte> jsonFragment)
        {
            IJsonReader jsonReader = JsonReader.Create(jsonFragment);
            this.WriteAll(jsonReader);
        }

        /// <summary>
        /// Writes a json node to the internal buffer.
        /// </summary>
        /// <param name="jsonNavigator">The navigator to use to navigate the node</param>
        /// <param name="jsonNavigatorNode">The node to write.</param>
        public void WriteJsonNode(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
        {
            if (jsonNavigator == null)
            {
                throw new ArgumentNullException($"{nameof(jsonNavigator)} can not be null");
            }

            if (jsonNavigatorNode == null)
            {
                throw new ArgumentNullException($"{nameof(jsonNavigatorNode)} can not be null");
            }

            JsonNodeType jsonNodeType = jsonNavigator.GetNodeType(jsonNavigatorNode);

            // See if we can write the node without looking at it's value
            switch (jsonNodeType)
            {
                case JsonNodeType.Null:
                    this.WriteNullValue();
                    return;
                case JsonNodeType.False:
                    this.WriteBoolValue(false);
                    return;
                case JsonNodeType.True:
                    this.WriteBoolValue(true);
                    return;
            }

            bool sameFormat = jsonNavigator.SerializationFormat == this.SerializationFormat;

            // If the navigator has the same format as this writer then we try to retrieve the node raw JSON
            if (sameFormat && jsonNavigator.TryGetBufferedRawJson(jsonNavigatorNode, out ReadOnlyMemory<byte> bufferedRawJson))
            {
                // Token type really doesn't make any difference other than whether this is a field name
                JsonTokenType jsonTokenType = jsonNodeType == JsonNodeType.FieldName ? JsonTokenType.FieldName : JsonTokenType.Null;
                this.WriteRawJsonToken(jsonTokenType, bufferedRawJson.Span);
            }
            else
            {
                // Either the formats did not match or we couldn't retrieve the buffered raw JSON
                switch (jsonNodeType)
                {
                    case JsonNodeType.Number64:
                        Number64 numberValue = jsonNavigator.GetNumber64Value(jsonNavigatorNode);
                        this.WriteNumber64Value(numberValue);
                        break;

                    case JsonNodeType.String:
                    case JsonNodeType.FieldName:
                        bool fieldName = jsonNodeType == JsonNodeType.FieldName;
                        if (jsonNavigator.TryGetBufferedUtf8StringValue(
                            jsonNavigatorNode,
                            out ReadOnlyMemory<byte> bufferedStringValue))
                        {
                            if (fieldName)
                            {
                                this.WriteFieldName(bufferedStringValue.Span);
                            }
                            else
                            {
                                this.WriteStringValue(bufferedStringValue.Span);
                            }
                        }
                        else
                        {
                            string value = jsonNavigator.GetStringValue(jsonNavigatorNode);
                            if (fieldName)
                            {
                                this.WriteFieldName(value);
                            }
                            else
                            {
                                this.WriteStringValue(value);
                            }
                        }

                        break;

                    case JsonNodeType.Int8:
                        {
                            sbyte number = jsonNavigator.GetInt8Value(jsonNavigatorNode);
                            this.WriteInt8Value(number);
                            break;
                        }

                    case JsonNodeType.Int16:
                        {
                            short number = jsonNavigator.GetInt16Value(jsonNavigatorNode);
                            this.WriteInt16Value(number);
                            break;
                        }

                    case JsonNodeType.Int32:
                        {
                            int number = jsonNavigator.GetInt32Value(jsonNavigatorNode);
                            this.WriteInt32Value(number);
                            break;
                        }

                    case JsonNodeType.Int64:
                        {
                            long number = jsonNavigator.GetInt64Value(jsonNavigatorNode);
                            this.WriteInt64Value(number);
                            break;
                        }

                    case JsonNodeType.UInt32:
                        {
                            uint number = jsonNavigator.GetUInt32Value(jsonNavigatorNode);
                            this.WriteUInt32Value(number);
                            break;
                        }

                    case JsonNodeType.Float32:
                        {
                            float number = jsonNavigator.GetFloat32Value(jsonNavigatorNode);
                            this.WriteFloat32Value(number);
                            break;
                        }

                    case JsonNodeType.Float64:
                        {
                            double number = jsonNavigator.GetFloat64Value(jsonNavigatorNode);
                            this.WriteFloat64Value(number);
                            break;
                        }

                    case JsonNodeType.Guid:
                        {
                            Guid number = jsonNavigator.GetGuidValue(jsonNavigatorNode);
                            this.WriteGuidValue(number);
                            break;
                        }

                    case JsonNodeType.Binary:
                        {
                            if (jsonNavigator.TryGetBufferedBinaryValue(jsonNavigatorNode, out ReadOnlyMemory<byte> bufferedBinaryValue))
                            {
                                this.WriteRawJsonToken(JsonTokenType.Binary, bufferedBinaryValue.Span);
                            }
                            else
                            {
                                ReadOnlyMemory<byte> value = jsonNavigator.GetBinaryValue(jsonNavigatorNode);
                                this.WriteBinaryValue(value.Span);
                            }

                            break;
                        }

                    case JsonNodeType.Array:
                        this.WriteArrayStart();
                        foreach (IJsonNavigatorNode arrayItem in jsonNavigator.GetArrayItems(jsonNavigatorNode))
                        {
                            this.WriteJsonNode(jsonNavigator, arrayItem);
                        }

                        this.WriteArrayEnd();
                        break;

                    case JsonNodeType.Object:
                        this.WriteObjectStart();
                        foreach (ObjectProperty objectProperty in jsonNavigator.GetObjectProperties(jsonNavigatorNode))
                        {
                            this.WriteJsonNode(jsonNavigator, objectProperty.NameNode);
                            this.WriteJsonNode(jsonNavigator, objectProperty.ValueNode);
                        }

                        this.WriteObjectEnd();
                        break;

                    default:
                        throw new ArgumentException($"Unexpected JsonNodeType: {jsonNodeType}");
                }
            }
        }

        /// <inheritdoc />
        public abstract ReadOnlyMemory<byte> GetResult();

        /// <summary>
        /// Writes a raw json token to the internal buffer.
        /// </summary>
        /// <param name="jsonTokenType">The JsonTokenType of the rawJsonToken</param>
        /// <param name="rawJsonToken">The raw json token.</param>
        protected abstract void WriteRawJsonToken(
            JsonTokenType jsonTokenType,
            ReadOnlySpan<byte> rawJsonToken);
    }
}
