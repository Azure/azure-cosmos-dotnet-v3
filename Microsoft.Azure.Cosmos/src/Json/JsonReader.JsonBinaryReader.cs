//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Partial JsonReader with a private JsonBinaryReader implementation
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonReader : IJsonReader
    {
        /// <summary>
        /// JsonReader that can read from a json serialized in binary <see cref="JsonBinaryEncoding"/>.
        /// </summary>
        private sealed class JsonBinaryReader : JsonReader
        {
            /// <summary>
            /// Buffer to read from.
            /// </summary>
            private readonly JsonBinaryBuffer jsonBinaryBuffer;

            /// <summary>
            /// Dictionary used for user string encoding.
            /// </summary>
            private readonly JsonStringDictionary jsonStringDictionary;

            /// <summary>
            /// For binary there is no end of token marker in the actual binary, but the JsonReader interface still needs to surface ObjectEndToken and ArrayEndToken.
            /// To accommodate for this we have a progress stack to let us know how many bytes there are left to read for all levels of nesting. 
            /// With this information we know that we are at the end of a context and can now surface an end object / array token.
            /// </summary>
            private readonly Stack<int> arrayAndObjectEndStack;

            private int currentTokenPosition;

            public JsonBinaryReader(
                ReadOnlyMemory<byte> buffer,
                JsonStringDictionary jsonStringDictionary = null,
                bool skipValidation = false)
                : base(skipValidation)
            {
                this.jsonBinaryBuffer = new JsonBinaryBuffer(buffer);

                // First byte is the serialization format so we are skipping over it
                this.jsonBinaryBuffer.Read();
                this.arrayAndObjectEndStack = new Stack<int>();
                this.jsonStringDictionary = jsonStringDictionary;
            }

            /// <summary>
            /// Gets the <see cref="JsonSerializationFormat"/> for the JsonReader
            /// </summary>
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Binary;
                }
            }

            /// <summary>
            /// Advances the JsonReader by one token.
            /// </summary>
            /// <returns><code>true</code> if the JsonReader successfully advanced to the next token; <code>false</code> if the JsonReader has passed the end of the JSON.</returns>
            public override bool Read()
            {
                JsonTokenType jsonTokenType;
                int valueLength;
                // First check if we just finished an array or object context
                if (this.arrayAndObjectEndStack.Peek() == this.jsonBinaryBuffer.Position)
                {
                    if (this.JsonObjectState.InArrayContext)
                    {
                        jsonTokenType = JsonTokenType.EndArray;
                    }
                    else if (this.JsonObjectState.InObjectContext)
                    {
                        jsonTokenType = JsonTokenType.EndObject;
                    }
                    else
                    {
                        throw new JsonInvalidTokenException();
                    }

                    valueLength = 0;
                    this.arrayAndObjectEndStack.Pop();
                }
                else
                {
                    // We are not at the end of a context.
                    if (this.jsonBinaryBuffer.IsEof)
                    {
                        // Need to check if we are still inside of an object or array
                        if (this.JsonObjectState.CurrentDepth != 0)
                        {
                            if (this.JsonObjectState.InObjectContext)
                            {
                                throw new JsonMissingEndObjectException();
                            }
                            else if (this.JsonObjectState.InArrayContext)
                            {
                                throw new JsonMissingEndArrayException();
                            }
                            else
                            {
                                throw new InvalidOperationException("Expected to be in either array or object context");
                            }
                        }

                        return false;
                    }

                    if (this.JsonObjectState.CurrentDepth == 0 && this.CurrentTokenType != JsonTokenType.NotStarted)
                    {
                        // There are trailing characters outside of the outter most object or array
                        throw new JsonUnexpectedTokenException();
                    }

                    jsonTokenType = JsonBinaryEncoding.GetJsonTokenType(this.jsonBinaryBuffer.Peek());
                    if (!JsonBinaryEncoding.TryGetValueLength(
                        this.jsonBinaryBuffer.GetBufferedRawJsonToken(),
                        out valueLength))
                    {
                        throw new JsonUnexpectedTokenException();
                    }

                    // If this is begin array/object token then we need to identify the array/object end token offset
                    if ((jsonTokenType == JsonTokenType.BeginArray) || (jsonTokenType == JsonTokenType.BeginObject))
                    {
                        this.arrayAndObjectEndStack.Push(this.jsonBinaryBuffer.Position + valueLength);
                    }
                    else if ((jsonTokenType == JsonTokenType.String) && this.JsonObjectState.IsPropertyExpected)
                    {
                        jsonTokenType = JsonTokenType.FieldName;
                    }
                }

                this.JsonObjectState.RegisterToken(jsonTokenType);
                this.currentTokenPosition = this.jsonBinaryBuffer.Position;
                this.jsonBinaryBuffer.SkipBytes(valueLength);
                return true;
            }

            /// <summary>
            /// Gets the next JSON token from the JsonReader as a double.
            /// </summary>
            /// <returns>The next JSON token from the JsonReader as a double.</returns>
            public override Number64 GetNumberValue()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Number)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetNumberValue(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            /// <summary>
            /// Gets the next JSON token from the JsonReader as a string.
            /// </summary>
            /// <returns>The next JSON token from the JsonReader as a string.</returns>
            public override string GetStringValue()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Number)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetStringValue(
                    this.jsonBinaryBuffer.GetBufferedRawJsonToken(),
                    this.jsonStringDictionary);
            }

            /// <summary>
            /// Gets next JSON token from the JsonReader as a raw series of bytes that is buffered.
            /// </summary>
            /// <returns>The next JSON token from the JsonReader as a raw series of bytes that is buffered.</returns>
            public override ReadOnlySpan<byte> GetBufferedRawJsonToken()
            {
                if (!JsonBinaryEncoding.TryGetValueLength(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition), out int length))
                {
                    throw new InvalidOperationException();
                }

                return this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition, this.currentTokenPosition + length);
            }

            public override sbyte GetInt8Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Int8)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetInt8Value(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            public override short GetInt16Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Int16)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetInt16Value(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            public override int GetInt32Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Int32)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetInt32Value(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            public override long GetInt64Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Int64)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetInt64Value(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            public override uint GetUInt32Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.UInt32)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetUInt32Value(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            public override float GetFloat32Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Float32)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetFloat32Value(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            public override double GetFloat64Value()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Float64)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetFloat64Value(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            public override Guid GetGuidValue()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Guid)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetGuidValue(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }

            public override ReadOnlySpan<byte> GetBinaryValue()
            {
                if (this.JsonObjectState.CurrentTokenType != JsonTokenType.Binary)
                {
                    throw new JsonNotNumberTokenException();
                }

                return JsonBinaryEncoding.GetBinaryValue(this.jsonBinaryBuffer.GetBufferedRawJsonToken(this.currentTokenPosition));
            }
        }

        private sealed class JsonBinaryBuffer : JsonBuffer
        {
            public JsonBinaryBuffer(ReadOnlyMemory<byte> buffer)
                : base(buffer)
            {
            }

            public void SkipBytes(int offset)
            {
                this.position += offset;
            }
        }
    }
}