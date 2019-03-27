//-----------------------------------------------------------------------
// <copyright file="JsonWriter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Base abstract class for JSON writers.
    /// The writer defines methods that allow for writing a JSON encoded value to a buffer.
    /// </summary>
    internal abstract partial class JsonWriter : IJsonWriter
    {
        /// <summary>
        /// The <see cref="JsonObjectState"/>
        /// </summary>
        protected readonly JsonObjectState JsonObjectState;

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

        /// <summary>
        /// Gets the SerializationFormat of the JsonWriter.
        /// </summary>
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Gets the current length of the internal buffer.
        /// </summary>
        public abstract long CurrentLength { get; }

        /// <summary>
        /// Creates a JsonTextWriter that can write in a particular encoding
        /// </summary>
        /// <param name="encoding">The encoding to write in.</param>
        /// <param name="skipValidation">Whether or not to skip validation</param>
        /// <returns>A JsonWriter that can write in a particular JsonSerializationFormat</returns>
        public static IJsonWriter Create(Encoding encoding, bool skipValidation = false)
        {
            if (encoding != Encoding.UTF8 && encoding != Encoding.Unicode && encoding != Encoding.UTF32)
            {
                throw new ArgumentException("Text encoding must be UTF8, UTF16 / Unicode or UTF32");
            }

            return new JsonTextWriter(encoding, skipValidation);
        }

        /// <summary>
        /// Creates a JsonWriter that can write in a particular JsonSerializationFormat (utf8 if text)
        /// </summary>
        /// <param name="jsonSerializationFormat">The JsonSerializationFormat of the writer.</param>
        /// <param name="skipValidation">Whether or not to skip validation</param>
        /// <returns>A JsonWriter that can write in a particular JsonSerializationFormat</returns>
        public static IJsonWriter Create(JsonSerializationFormat jsonSerializationFormat, bool skipValidation = false)
        {
            switch (jsonSerializationFormat)
            {
                case JsonSerializationFormat.Text:
                    return new JsonTextWriter(Encoding.UTF8, skipValidation);
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryWriter(skipValidation, false);
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, RMResources.UnexpectedJsonSerializationFormat, jsonSerializationFormat));
            }
        }

        /// <summary>
        /// Writes the object start symbol to internal buffer.
        /// </summary>
        public abstract void WriteObjectStart();

        /// <summary>
        /// Writes the object end symbol to the internal buffer.
        /// </summary>
        public abstract void WriteObjectEnd();

        /// <summary>
        /// Writes the array start symbol to the internal buffer.
        /// </summary>
        public abstract void WriteArrayStart();

        /// <summary>
        /// Writes the array end symbol to the internal buffer.
        /// </summary>
        public abstract void WriteArrayEnd();

        /// <summary>
        /// Writes a field name to the the internal buffer.
        /// </summary>
        /// <param name="fieldName">The name of the field to write.</param>
        public abstract void WriteFieldName(string fieldName);

        /// <summary>
        /// Writes a string to the internal buffer.
        /// </summary>
        /// <param name="value">The value of the string to write.</param>
        public abstract void WriteStringValue(string value);

        /// <summary>
        /// Writes an integer to the internal buffer.
        /// </summary>
        /// <param name="value">The value of the integer to write.</param>
        public abstract void WriteIntValue(long value);

        /// <summary>
        /// Writes a number to the internal buffer.
        /// </summary>
        /// <param name="value">The value of the number to write.</param>
        public abstract void WriteNumberValue(double value);

        /// <summary>
        /// Writes a boolean to the internal buffer.
        /// </summary>
        /// <param name="value">The value of the boolean to write.</param>
        public abstract void WriteBoolValue(bool value);

        /// <summary>
        /// Writes a null to the internal buffer.
        /// </summary>
        public abstract void WriteNullValue();

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

            // For now short circuit this to false until we figure out how to optimize this.
            bool sameFormat = jsonReader.SerializationFormat == this.SerializationFormat && false;

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
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.FieldName:
                    {
                        if (sameFormat)
                        {
                            IReadOnlyList<byte> bufferedRawJson = jsonReader.GetBufferedRawJsonToken();
                            this.WriteRawJsonToken(jsonTokenType, bufferedRawJson);
                        }
                        else
                        {
                            if (jsonTokenType == JsonTokenType.Number)
                            {
                                double number = jsonReader.GetNumberValue();
                                this.WriteNumberValue(number);
                            }
                            else
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
                            }
                        }

                        break;
                    }

                default:
                    throw new ArgumentException($"Unexpected JsonTokenType: {jsonTokenType}");
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
        public void WriteJsonFragment(IReadOnlyList<byte> jsonFragment)
        {
            if (jsonFragment == null)
            {
                throw new ArgumentNullException("jsonFragment can not be null");
            }

            IJsonReader jsonReader = JsonReader.Create(new MemoryStream(jsonFragment.ToArray()));
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

            // For now short circuit this to false until we figure out how to optimize this.
            bool sameFormat = jsonNavigator.SerializationFormat == this.SerializationFormat && false;

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

            // If the navigator has the same format as this writer then we try to retrieve the node raw JSON
            IReadOnlyList<byte> bufferedRawJson;
            if (sameFormat && jsonNavigator.TryGetBufferedRawJson(jsonNavigatorNode, out bufferedRawJson))
            {
                // Token type really doesn't make any difference other than whether this is a field name
                JsonTokenType jsonTokenType = (jsonNodeType == JsonNodeType.FieldName ? JsonTokenType.FieldName : JsonTokenType.Null);
                this.WriteRawJsonToken(jsonTokenType, bufferedRawJson);
            }
            else
            {
                // Either the formats did not match or we couldn't retrieve the buffered raw JSON
                switch (jsonNodeType)
                {
                    case JsonNodeType.Number:
                        double numberValue = jsonNavigator.GetNumberValue(jsonNavigatorNode);
                        this.WriteNumberValue(numberValue);
                        break;

                    case JsonNodeType.String:
                    case JsonNodeType.FieldName:
                        bool fieldName = jsonNodeType == JsonNodeType.FieldName;
                        IReadOnlyList<byte> bufferedStringValue;
                        if (jsonNavigator.TryGetBufferedStringValue(jsonNavigatorNode, out bufferedStringValue))
                        {
                            if (fieldName)
                            {
                                this.WriteRawJsonToken(JsonTokenType.FieldName, bufferedStringValue);
                            }
                            else
                            {
                                this.WriteRawJsonToken(JsonTokenType.String, bufferedStringValue);
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

        /// <summary>
        /// Gets the result of the JsonWriter.
        /// </summary>
        /// <returns>The result of the JsonWriter as an array of bytes.</returns>
        public abstract byte[] GetResult();

        /// <summary>
        /// Writes a raw json token to the internal buffer.
        /// </summary>
        /// <param name="jsonTokenType">The JsonTokenType of the rawJsonToken</param>
        /// <param name="rawJsonToken">The raw json token.</param>
        protected abstract void WriteRawJsonToken(JsonTokenType jsonTokenType, IReadOnlyList<byte> rawJsonToken);
    }
}
