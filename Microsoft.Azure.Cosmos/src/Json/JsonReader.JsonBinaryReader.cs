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
            /// JsonBinaryReader can read from either a stream or a byte array and that is abstracted as a JsonBinaryBuffer.
            /// </summary>
            private readonly JsonBinaryBufferBase jsonBinaryBuffer;

            /// <summary>
            /// Dictionary used for user string encoding.
            /// </summary>
            private readonly JsonStringDictionary jsonStringDictionary;

            /// <summary>
            /// For binary there is no end of token marker in the actual binary, but the JsonReader interface still needs to surface ObjectEndToken and ArrayEndToken.
            /// To accommodate for this we have a progress stack to let us know how many bytes there are left to read for all levels of nesting. 
            /// With this information we know that we are at the end of a context and can now surface an end object / array token.
            /// </summary>
            private readonly ProgressStack progressStack;

            public JsonBinaryReader(byte[] buffer, JsonStringDictionary jsonStringDictionary = null, bool skipValidation = false)
                : this(new JsonBinaryArrayBuffer(buffer, jsonStringDictionary))
            {
            }

            public JsonBinaryReader(Stream stream, JsonStringDictionary jsonStringDictionary = null, bool skipValidation = false)
                : this(new JsonBinaryStreamBuffer(stream, jsonStringDictionary))
            {
            }

            private JsonBinaryReader(JsonBinaryBufferBase jsonBinaryBuffer, JsonStringDictionary jsonStringDictionary = null, bool skipValidation = false)
                : base(skipValidation)
            {
                this.jsonBinaryBuffer = jsonBinaryBuffer;

                // First byte is the serialization format so we are skipping over it
                this.jsonBinaryBuffer.ReadByte();
                this.progressStack = new ProgressStack();
                this.jsonStringDictionary = jsonStringDictionary;
            }

            /// <summary>
            /// Callback for processing json tokens.
            /// </summary>
            /// <param name="newContextLength">The length of the new context if there is one.</param>
            /// <returns>Whether or not there is a new context.</returns>
            private delegate bool ProcessJsonTokenCallback(out long newContextLength);

            private enum ContextType
            {
                EmptyContext = 0,
                SingleItemContext = -1,
                SinglePropertyContext = -2,
                ContextWithLength = 1,
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
                // First check if we just finished an array or object context
                if (this.progressStack.IsAtEndOfContext)
                {
                    JsonTokenType jsonTokenType = JsonTokenType.NotStarted;
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
                        DefaultTrace.TraceCritical("Expected to be in either array or object context");
                    }

                    this.JsonObjectState.RegisterToken(jsonTokenType);
                    this.progressStack.Pop();
                    return true;
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
                                DefaultTrace.TraceCritical("Expected to be in either array or object context");
                            }
                        }

                        return false;
                    }
                    else
                    {
                        if (this.JsonObjectState.CurrentDepth == 0 && this.CurrentTokenType != JsonTokenType.NotStarted)
                        {
                            // There are trailing characters outside of the outter most object or array
                            throw new JsonUnexpectedTokenException();
                        }

                        this.ReadToken();
                        return true;
                    }
                }
            }

            /// <summary>
            /// Gets the next JSON token from the JsonReader as a double.
            /// </summary>
            /// <returns>The next JSON token from the JsonReader as a double.</returns>
            public override double GetNumberValue()
            {
                return this.jsonBinaryBuffer.GetNumberValue();
            }

            /// <summary>
            /// Gets the next JSON token from the JsonReader as a string.
            /// </summary>
            /// <returns>The next JSON token from the JsonReader as a string.</returns>
            public override string GetStringValue()
            {
                return this.jsonBinaryBuffer.GetStringValue();
            }

            /// <summary>
            /// Gets next JSON token from the JsonReader as a raw series of bytes that is buffered.
            /// </summary>
            /// <returns>The next JSON token from the JsonReader as a raw series of bytes that is buffered.</returns>
            public override IReadOnlyList<byte> GetBufferedRawJsonToken()
            {
                return this.jsonBinaryBuffer.GetBufferedRawJsonToken();
            }

            private void ReadToken()
            {
                byte typeMarker = this.jsonBinaryBuffer.Peek();
                switch (this.GetJsonTokenType(typeMarker))
                {
                    case JsonTokenType.BeginArray:
                        this.ProcessBeginArray();
                        break;
                    case JsonTokenType.BeginObject:
                        this.ProcessBeginObject();
                        break;
                    case JsonTokenType.String:
                        this.ProcessString();
                        break;
                    case JsonTokenType.Number:
                        this.ProcessNumber();
                        break;
                    case JsonTokenType.True:
                        this.ProcessTrue();
                        break;
                    case JsonTokenType.False:
                        this.ProcessFalse();
                        break;
                    case JsonTokenType.Null:
                        this.ProcessNull();
                        break;
                    default:
                        throw new JsonUnexpectedTokenException();
                }
            }

            /// <summary>
            /// Given a jsonTokenType and supplied callback this function will update jsonBuffer, jsonObject, and progressStack.
            /// The buffer will know the current token type, where that token starts and ends.
            /// The object state will also know the current token type.
            /// Finally the progress stack will add a new context if there is one and make progress on all parent context.
            /// </summary>
            /// <param name="jsonTokenType">The type of token being processed.</param>
            /// <param name="processJsonTokenCallback">The callback used to actually process the token.</param>
            private void ProcessJsonToken(JsonTokenType jsonTokenType, ProcessJsonTokenCallback processJsonTokenCallback)
            {
                // Start the token
                this.jsonBinaryBuffer.CurrentJsonTokenType = jsonTokenType;
                this.jsonBinaryBuffer.StartToken();

                // Process the value and see how many bytes to push on to the progess stack.
                long newContextLength;
                bool hasNewContext = processJsonTokenCallback(out newContextLength);

                // End the token
                this.jsonBinaryBuffer.EndToken();
                this.JsonObjectState.RegisterToken(jsonTokenType);
                long bytesRead = this.jsonBinaryBuffer.GetBufferedRawJsonToken().Count;

                // reading bytes from the most nested context means we read bytes from all context
                // Need to modify all elements of the stack to reflect the number of bytes read.
                this.progressStack.UpdateProgress(bytesRead);

                if (this.progressStack.Count != 0)
                {
                    switch (this.progressStack.CurrentContextType)
                    {
                        case ContextType.EmptyContext:
                            // Don't so anything, since the context just became empty
                            break;

                        case ContextType.SingleItemContext:
                            // This was the only item in a single item context
                            this.progressStack.Pop();

                            // Which means there is nothing left to read from said array
                            this.progressStack.PushEmptyContext();
                            break;

                        case ContextType.SinglePropertyContext:
                            // This was the fieldname of a single property object
                            this.progressStack.Pop();

                            // But there is still a property value left to read
                            this.progressStack.PushSingleItemContext();
                            break;

                        case ContextType.ContextWithLength:
                            // Still working on this context, so don't do anything to it.
                            break;

                        default:
                            DefaultTrace.TraceCritical("Progress stack's has unknown context type.");
                            break;
                    }
                }

                // Push the new context length is necessary.
                if (hasNewContext)
                {
                    this.progressStack.Push(newContextLength);
                }
            }

            private bool ProcessBeginArrayCallback(out long newContextLength)
            {
                byte typeMarker = this.jsonBinaryBuffer.ReadByte();
                long count;
                switch (typeMarker)
                {
                    case JsonBinaryEncoding.TypeMarker.EmptyArray:
                        newContextLength = ProgressStack.EmptyContext;
                        break;
                    case JsonBinaryEncoding.TypeMarker.SingleItemArray:
                        // We don't know the length of a single item array at this point so we just delay and will update once we do know.
                        newContextLength = ProgressStack.SingleItemContext;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array1ByteLength:
                        newContextLength = this.jsonBinaryBuffer.ReadByte();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount:
                        newContextLength = this.jsonBinaryBuffer.ReadByte();
                        count = this.jsonBinaryBuffer.ReadByte();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLength:
                        newContextLength = this.jsonBinaryBuffer.ReadUInt16();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount:
                        newContextLength = this.jsonBinaryBuffer.ReadUInt16();
                        count = this.jsonBinaryBuffer.ReadUInt16();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLength:
                        newContextLength = this.jsonBinaryBuffer.ReadUInt32();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount:
                        newContextLength = this.jsonBinaryBuffer.ReadUInt32();
                        count = this.jsonBinaryBuffer.ReadUInt32();
                        break;
                    default:
                        throw new JsonInvalidTokenException();
                }

                return true;
            }

            private void ProcessBeginArray()
            {
                this.ProcessJsonToken(JsonTokenType.BeginArray, this.ProcessBeginArrayCallback);
            }

            private bool ProcessBeingObjectCallback(out long newContextLength)
            {
                byte typeMarker = this.jsonBinaryBuffer.ReadByte();
                long count;
                switch (typeMarker)
                {
                    case JsonBinaryEncoding.TypeMarker.EmptyObject:
                        newContextLength = ProgressStack.EmptyContext;
                        break;
                    case JsonBinaryEncoding.TypeMarker.SinglePropertyObject:
                        // We don't know the length of a single property object so we will delay by saying "2" things need to be read.
                        newContextLength = ProgressStack.SinglePropertyContext;
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object1ByteLength:
                        newContextLength = this.jsonBinaryBuffer.ReadByte();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object1ByteLengthAndCount:
                        newContextLength = this.jsonBinaryBuffer.ReadByte();
                        count = this.jsonBinaryBuffer.ReadByte();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLength:
                        newContextLength = this.jsonBinaryBuffer.ReadUInt16();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount:
                        newContextLength = this.jsonBinaryBuffer.ReadUInt16();
                        count = this.jsonBinaryBuffer.ReadUInt16();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLength:
                        newContextLength = this.jsonBinaryBuffer.ReadUInt32();
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount:
                        newContextLength = this.jsonBinaryBuffer.ReadUInt32();
                        count = this.jsonBinaryBuffer.ReadUInt32();
                        break;
                    default:
                        throw new JsonInvalidTokenException();
                }

                return true;
            }

            private void ProcessBeginObject()
            {
                this.ProcessJsonToken(JsonTokenType.BeginObject, this.ProcessBeingObjectCallback);
            }

            private bool ProcessStringCallback(out long newContextLength)
            {
                byte typeMarker = this.jsonBinaryBuffer.ReadByte();

                if (JsonBinaryEncoding.TypeMarker.IsSystemString(typeMarker))
                {
                    this.ProcessEncodedSystemString(typeMarker);
                }
                else if (JsonBinaryEncoding.TypeMarker.IsUserString(typeMarker))
                {
                    this.ProcessEncodedUserString(typeMarker);
                }
                else
                {
                    // Retrieve utf-8 buffered string
                    this.ProcessStringWithLength(typeMarker);
                }

                newContextLength = 0;
                return false;
            }

            private void ProcessString()
            {
                this.ProcessJsonToken(
                    this.JsonObjectState.IsPropertyExpected ? JsonTokenType.FieldName : JsonTokenType.String,
                    this.ProcessStringCallback);
            }

            private void ProcessStringWithLength(byte typeMarker)
            {
                long length;
                if (JsonBinaryEncoding.TypeMarker.IsEncodedLengthString(typeMarker))
                {
                    length = JsonBinaryEncoding.GetStringLengths(typeMarker);
                }
                else
                {
                    switch (typeMarker)
                    {
                        case JsonBinaryEncoding.TypeMarker.String1ByteLength:
                            length = this.jsonBinaryBuffer.ReadByte();
                            break;
                        case JsonBinaryEncoding.TypeMarker.String2ByteLength:
                            length = this.jsonBinaryBuffer.ReadUInt16();
                            break;
                        case JsonBinaryEncoding.TypeMarker.String4ByteLength:
                            length = this.jsonBinaryBuffer.ReadUInt32();
                            break;
                        default:
                            throw new JsonNotStringTokenException();
                    }
                }

                if (length > int.MaxValue)
                {
                    throw new InvalidOperationException("Tried to read a string whose length is greater than int.MaxValue");
                }

                this.jsonBinaryBuffer.ReadBytes((int)length);
            }

            private void ProcessEncodedUserString(byte typeMarker)
            {
                if (JsonBinaryEncoding.TypeMarker.IsOneByteEncodedUserString(typeMarker))
                {
                    // typemarker is the encoded user string.
                }
                else if (JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedUserString(typeMarker))
                {
                    this.jsonBinaryBuffer.ReadByte();
                }
                else
                {
                    throw new JsonInvalidStringCharacterException();
                }
            }

            private void ProcessEncodedSystemString(byte typeMarker)
            {
                if (JsonBinaryEncoding.TypeMarker.IsOneByteEncodedSystemString(typeMarker))
                {
                    // typemarker is the encoded system string.
                }
                else if (JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedSystemString(typeMarker))
                {
                    this.jsonBinaryBuffer.ReadByte();
                }
                else
                {
                    throw new JsonInvalidStringCharacterException();
                }
            }

            private bool ProcessNumberCallback(out long newContextLength)
            {
                byte typeMarker = this.jsonBinaryBuffer.ReadByte();

                if (JsonBinaryEncoding.TypeMarker.IsEncodedIntegerLiteral(typeMarker))
                {
                    // The number marker is the value;
                }
                else
                {
                    switch (typeMarker)
                    {
                        case JsonBinaryEncoding.TypeMarker.UInt8:
                            this.jsonBinaryBuffer.ReadByte();
                            break;
                        case JsonBinaryEncoding.TypeMarker.Int16:
                            this.jsonBinaryBuffer.ReadInt16();
                            break;
                        case JsonBinaryEncoding.TypeMarker.Int32:
                            this.jsonBinaryBuffer.ReadInt32();
                            break;
                        case JsonBinaryEncoding.TypeMarker.Int64:
                            this.jsonBinaryBuffer.ReadInt64();
                            break;
                        case JsonBinaryEncoding.TypeMarker.Double:
                            this.jsonBinaryBuffer.ReadDouble();
                            break;
                        default:
                            throw new JsonInvalidNumberException();
                    }
                }

                newContextLength = 0;
                return false;
            }

            private void ProcessNumber()
            {
                this.ProcessJsonToken(JsonTokenType.Number, this.ProcessNumberCallback);
            }

            private void ProcessTrue()
            {
                this.ProcessSingleByteToken(JsonTokenType.True);
            }

            private void ProcessFalse()
            {
                this.ProcessSingleByteToken(JsonTokenType.False);
            }

            private void ProcessNull()
            {
                this.ProcessSingleByteToken(JsonTokenType.Null);
            }

            private bool ProcessSingleByteTokenCallback(out long newContextLength)
            {
                // Consume the type marker
                this.jsonBinaryBuffer.ReadByte();

                newContextLength = 0;
                return false;
            }

            private void ProcessSingleByteToken(JsonTokenType jsonTokenType)
            {
                this.ProcessJsonToken(jsonTokenType, this.ProcessSingleByteTokenCallback);
            }

            private JsonTokenType GetJsonTokenType(byte typeMarker)
            {
                JsonTokenType jsonTokenType;
                if (JsonBinaryEncoding.TypeMarker.IsEncodedIntegerLiteral(typeMarker))
                {
                    jsonTokenType = JsonTokenType.Number;
                }
                else if (JsonBinaryEncoding.TypeMarker.IsOneByteEncodedString(typeMarker))
                {
                    jsonTokenType = JsonTokenType.String;
                }
                else if (JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedString(typeMarker))
                {
                    jsonTokenType = JsonTokenType.String;
                }
                else if (JsonBinaryEncoding.TypeMarker.IsEncodedLengthString(typeMarker))
                {
                    jsonTokenType = JsonTokenType.String;
                }
                else
                {
                    switch (typeMarker)
                    {
                        // Single-byte values
                        case JsonBinaryEncoding.TypeMarker.Null:
                            jsonTokenType = JsonTokenType.Null;
                            break;

                        case JsonBinaryEncoding.TypeMarker.False:
                            jsonTokenType = JsonTokenType.False;
                            break;

                        case JsonBinaryEncoding.TypeMarker.True:
                            jsonTokenType = JsonTokenType.True;
                            break;

                        // Number values
                        case JsonBinaryEncoding.TypeMarker.UInt8:
                        case JsonBinaryEncoding.TypeMarker.Int16:
                        case JsonBinaryEncoding.TypeMarker.Int32:
                        case JsonBinaryEncoding.TypeMarker.Int64:
                        case JsonBinaryEncoding.TypeMarker.Double:
                            jsonTokenType = JsonTokenType.Number;
                            break;

                        // Variable Length String Values
                        case JsonBinaryEncoding.TypeMarker.String1ByteLength:
                        case JsonBinaryEncoding.TypeMarker.String2ByteLength:
                        case JsonBinaryEncoding.TypeMarker.String4ByteLength:
                            jsonTokenType = JsonTokenType.String;
                            break;

                        // Array Values
                        case JsonBinaryEncoding.TypeMarker.EmptyArray:
                        case JsonBinaryEncoding.TypeMarker.SingleItemArray:
                        case JsonBinaryEncoding.TypeMarker.Array1ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Array2ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Array4ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount:
                        case JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount:
                        case JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount:
                            jsonTokenType = JsonTokenType.BeginArray;
                            break;

                        // Object Values
                        case JsonBinaryEncoding.TypeMarker.EmptyObject:
                        case JsonBinaryEncoding.TypeMarker.SinglePropertyObject:
                        case JsonBinaryEncoding.TypeMarker.Object1ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Object2ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Object4ByteLength:
                        case JsonBinaryEncoding.TypeMarker.Object1ByteLengthAndCount:
                        case JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount:
                        case JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount:
                            jsonTokenType = JsonTokenType.BeginObject;
                            break;

                        default:
                            throw new JsonInvalidTokenException();
                    }
                }

                return jsonTokenType;
            }

            #region JsonBinaryBufferBase
            /// <summary>
            /// Base implementation of JsonBinaryBuffer that other classes will derive from.
            /// </summary>
            private abstract class JsonBinaryBufferBase
            {
                /// <summary>
                /// The reader we will use to read from the stream.
                /// Note that this reader is able to read from a little endian stream even if the client is on a big endian machine.
                /// </summary>
                protected readonly LittleEndianBinaryReader BinaryReader;
                protected readonly JsonStringDictionary jsonStringDictionary;
                private JsonTokenType currentTokenType;

                /// <summary>
                /// Initializes a new instance of the JsonBinaryBufferBase class from an array of bytes.
                /// </summary>
                /// <param name="stream">A stream to read from.</param>
                /// <param name="jsonStringDictionary">The JSON string dictionary to use for user string encoding.</param>
                protected JsonBinaryBufferBase(Stream stream, JsonStringDictionary jsonStringDictionary = null)
                {
                    if (stream == null)
                    {
                        throw new ArgumentNullException("stream");
                    }

                    this.BinaryReader = new LittleEndianBinaryReader(stream, Encoding.UTF8);
                    this.jsonStringDictionary = jsonStringDictionary;
                }

                /// <summary>
                /// Gets a value indicating whether the buffer is at the End of File for it's source.
                /// </summary>
                public bool IsEof
                {
                    get
                    {
                        return this.BinaryReader.BaseStream.Position == this.BinaryReader.BaseStream.Length;
                    }
                }

                /// <summary>
                /// Gets or sets a value indicating the current <see cref="JsonTokenType"/>.
                /// </summary>
                public JsonTokenType CurrentJsonTokenType
                {
                    get
                    {
                        return this.currentTokenType;
                    }

                    set
                    {
                        this.currentTokenType = value;
                    }
                }

                /// <summary>
                /// Lets the IJsonBinaryBuffer know that it is at the start of a token.
                /// </summary>
                public abstract void StartToken();

                /// <summary>
                /// Lets the IJsonBinaryBuffer know that an end of a token has just been read from it.
                /// </summary>
                public abstract void EndToken();

                /// <summary>
                /// Returns the next available byte and does not advance the byte position
                /// </summary>
                /// <returns>The next available byte, or -1 if no more bytes are available or the buffer does not support seeking.</returns>
                public virtual byte Peek()
                {
                    byte byteRead = this.BinaryReader.ReadByte();
                    this.BinaryReader.BaseStream.Position--;
                    return byteRead;
                }

                /// <summary>
                /// Reads a Boolean value from the current stream and advances the current position of the stream by one byte.
                /// </summary>
                /// <returns>true if the byte is nonzero; otherwise, false.</returns>
                public virtual bool ReadBoolean()
                {
                    byte boolean = this.BinaryReader.ReadBoolean() ? (byte)1 : (byte)0;
                    return boolean == 1 ? true : false;
                }

                /// <summary>
                /// Reads the next byte from the current stream and advances the current position of the stream by one byte.
                /// </summary>
                /// <returns>The next byte read from the current stream.</returns>
                public virtual byte ReadByte()
                {
                    byte byteRead = this.BinaryReader.ReadByte();
                    return byteRead;
                }

                /// <summary>
                /// Reads the specified number of bytes from the current stream into a byte array and advances the current position by that number of bytes.
                /// </summary>
                /// <param name="count">The number of bytes to read.</param>
                /// <returns>A byte array containing data read from the underlying stream. This might be less than the number of bytes requested if the end of the stream is reached.</returns>
                public virtual byte[] ReadBytes(int count)
                {
                    byte[] bytesRead = this.BinaryReader.ReadBytes(count);
                    return bytesRead;
                }

                /// <summary>
                /// Reads an 8-byte floating point value from the current stream and advances the current position of the stream by eight bytes.
                /// </summary>
                /// <returns>An 8-byte floating point value read from the current stream.</returns>
                public virtual double ReadDouble()
                {
                    double doubleRead = this.BinaryReader.ReadDouble();
                    return doubleRead;
                }

                /// <summary>
                /// Reads a 2-byte signed integer from the current stream and advances the current position of the stream by two bytes.
                /// </summary>
                /// <returns>A 2-byte signed integer read from the current stream.</returns>
                public virtual short ReadInt16()
                {
                    short shortRead = this.BinaryReader.ReadInt16();
                    return shortRead;
                }

                /// <summary>
                /// Reads a 4-byte signed integer from the current stream and advances the current position of the stream by four bytes.
                /// </summary>
                /// <returns>A 4-byte signed integer read from the current stream.</returns>
                public virtual int ReadInt32()
                {
                    int intRead = this.BinaryReader.ReadInt32();
                    return intRead;
                }

                /// <summary>
                /// Reads an 8-byte signed integer from the current stream and advances the current position of the stream by eight bytes.
                /// </summary>
                /// <returns>An 8-byte signed integer read from the current stream.</returns>
                public virtual long ReadInt64()
                {
                    long longRead = this.BinaryReader.ReadInt64();
                    return longRead;
                }

                /// <summary>
                /// Reads a signed byte from this stream and advances the current position of the stream by one byte.
                /// </summary>
                /// <returns> A signed byte read from the current stream.</returns>
                public virtual sbyte ReadSByte()
                {
                    sbyte sbyteRead = this.BinaryReader.ReadSByte();
                    return sbyteRead;
                }

                /// <summary>
                /// Reads a 4-byte floating point value from the current stream and advances the current position of the stream by four bytes.
                /// </summary>
                /// <returns>A 4-byte floating point value read from the current stream.</returns>
                public virtual float ReadSingle()
                {
                    float floatRead = this.BinaryReader.ReadSingle();
                    return floatRead;
                }

                /// <summary>
                /// Reads a 2-byte unsigned integer from the current stream using little-endian encoding and advances the position of the stream by two bytes.
                /// </summary>
                /// <returns>A 2-byte unsigned integer read from this stream.</returns>
                public virtual ushort ReadUInt16()
                {
                    ushort ushortRead = this.BinaryReader.ReadUInt16();
                    return ushortRead;
                }

                /// <summary>
                /// Reads a 4-byte unsigned integer from the current stream and advances the position of the stream by four bytes.
                /// </summary>
                /// <returns>A 4-byte unsigned integer read from this stream.</returns>
                public virtual uint ReadUInt32()
                {
                    uint uintRead = this.BinaryReader.ReadUInt32();
                    return uintRead;
                }

                /// <summary>
                /// Reads an 8-byte unsigned integer from the current stream and advances the position of the stream by eight bytes.
                /// </summary>
                /// <returns>An 8-byte unsigned integer read from this stream.</returns>
                public virtual ulong ReadUInt64()
                {
                    ulong ulongRead = this.BinaryReader.ReadUInt64();
                    return ulongRead;
                }

                /// <summary>
                /// Gets the buffered raw json token.
                /// </summary>
                /// <returns>The buffered raw json token.</returns>
                public abstract IReadOnlyList<byte> GetBufferedRawJsonToken();

                /// <summary>
                /// Gets the next JSON token from the IJsonBinaryBuffer as a double.
                /// </summary>
                /// <returns>The next JSON token from the BinaryBuffer as a double.</returns>
                public double GetNumberValue()
                {
                    if (this.CurrentJsonTokenType != JsonTokenType.Number)
                    {
                        throw new JsonNotNumberTokenException();
                    }

                    long currentPosition = this.BinaryReader.BaseStream.Position;
                    double value = this.GetNumberValue((ArraySegment<byte>)this.GetBufferedRawJsonToken());
                    this.BinaryReader.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
                    return value;
                }

                /// <summary>
                /// Gets the next JSON token from the IJsonBinaryBuffer as a string.
                /// </summary>
                /// <returns>The next JSON token from the JsonReader as a string.</returns>
                public string GetStringValue()
                {
                    if (this.CurrentJsonTokenType != JsonTokenType.String && this.CurrentJsonTokenType != JsonTokenType.FieldName)
                    {
                        throw new JsonNotStringTokenException();
                    }

                    long currentPosition = this.BinaryReader.BaseStream.Position;
                    string value = this.GetStringValue((ArraySegment<byte>)this.GetBufferedRawJsonToken());
                    this.BinaryReader.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
                    return value;
                }

                /// <summary>
                /// Gets a binary reader whose position is at the beginning of the provided jsonToken.
                /// </summary>
                /// <param name="jsonToken">The json token input.</param>
                /// <returns>A binary reader whose position is at the beginning of the provided jsonToken.</returns>
                protected abstract BinaryReader GetBinaryReaderAtToken(ArraySegment<byte> jsonToken);

                /// <summary>
                /// Gets the number value from a json token as a double.
                /// </summary>
                /// <param name="jsonToken">The json token to get the number value of.</param>
                /// <returns>the number value from a json token as a double.</returns>
                private double GetNumberValue(ArraySegment<byte> jsonToken)
                {
                    BinaryReader binaryReader = this.GetBinaryReaderAtToken(jsonToken);
                    return JsonBinaryEncoding.GetNumberValue(binaryReader);
                }

                /// <summary>
                /// Gets the string value from a json token as a string.
                /// </summary>
                /// <param name="jsonToken">The json token to get the string value of.</param>
                /// <returns>the string value from a json token as a string.</returns>
                private string GetStringValue(ArraySegment<byte> jsonToken)
                {
                    BinaryReader binaryReader = this.GetBinaryReaderAtToken(jsonToken);
                    return JsonBinaryEncoding.GetStringValue(binaryReader, this.jsonStringDictionary);
                }
            }
            #endregion

            #region JsonBinaryArrayBuffer
            /// <summary>
            /// JsonBinaryBuffer where the source is an array of bytes.
            /// </summary>
            private sealed class JsonBinaryArrayBuffer : JsonBinaryBufferBase
            {
                private readonly byte[] buffer;
                private long currentBeginOffset;
                private long currentEndOffset;

                /// <summary>
                /// Initializes a new instance of the JsonBinaryArrayBuffer class.
                /// </summary>
                /// <param name="buffer">The source buffer to read from.</param>
                /// <param name="jsonStringDictionary">The string dictionary to use for dictionary encoding.</param>
                public JsonBinaryArrayBuffer(byte[] buffer, JsonStringDictionary jsonStringDictionary = null)
                    : base(new MemoryStream(buffer, 0, buffer.Length, false, true), jsonStringDictionary)
                {
                    if (buffer == null)
                    {
                        throw new ArgumentNullException("buffer");
                    }

                    this.buffer = buffer;
                }

                /// <summary>
                /// Gets a value indicating how many bytes have been read from the binary buffer.
                /// </summary>
                private long BytesRead
                {
                    get
                    {
                        return this.BinaryReader.BaseStream.Position;
                    }
                }

                /// <summary>
                /// Lets the IJsonBinaryBuffer know that it is at the start of a token.
                /// </summary>
                public override void StartToken()
                {
                    this.currentBeginOffset = this.BytesRead;
                }

                /// <summary>
                /// Lets the IJsonBinaryBuffer know that an end of a token has just been read from it.
                /// </summary>
                public override void EndToken()
                {
                    this.currentEndOffset = this.BytesRead;
                }

                /// <summary>
                /// Gets the buffered raw json token.
                /// </summary>
                /// <returns>The buffered raw json token.</returns>
                public override IReadOnlyList<byte> GetBufferedRawJsonToken()
                {
                    return new ArraySegment<byte>(
                        this.buffer,
                        (int)this.currentBeginOffset,
                        (int)(this.currentEndOffset - this.currentBeginOffset));
                }

                /// <summary>
                /// Gets a binary reader whose position is at the beginning of the provided jsonToken.
                /// </summary>
                /// <param name="jsonToken">The json token input.</param>
                /// <returns>A binary reader whose position is at the beginning of the provided jsonToken.</returns>
                protected override BinaryReader GetBinaryReaderAtToken(ArraySegment<byte> jsonToken)
                {
                    this.BinaryReader.BaseStream.Seek(jsonToken.Offset, SeekOrigin.Begin);
                    return this.BinaryReader;
                }
            }
            #endregion

            #region JsonBinaryStreamBuffer
            /// <summary>
            /// JsonBinaryBuffer whose source is a stream.
            /// </summary>
            private sealed class JsonBinaryStreamBuffer : JsonBinaryBufferBase
            {
                /// <summary>
                /// We need to buffer one token from the stream incase a user wants to materialize it and the stream is not seekable (like a network stream).
                /// </summary>
                private readonly MemoryStream bufferedToken;
                private readonly BinaryWriter bufferedTokenWriter;
                private readonly BinaryReader bufferedTokenReader;
                private int tokenLength;

                /// <summary>
                /// Initializes a new instance of the JsonBinaryStreamBuffer class.
                /// </summary>
                /// <param name="stream">The stream to buffer from.</param>
                /// <param name="jsonStringDictionary">The dictionary to use for user string encoding.</param>
                public JsonBinaryStreamBuffer(Stream stream, JsonStringDictionary jsonStringDictionary = null)
                    : base(stream, jsonStringDictionary)
                {
                    if (stream == null)
                    {
                        throw new ArgumentNullException("stream");
                    }

                    this.bufferedToken = new MemoryStream();
                    this.bufferedTokenWriter = new BinaryWriter(this.bufferedToken);
                    this.bufferedTokenReader = new BinaryReader(this.bufferedToken);
                }

                /// <summary>
                /// Reads a Boolean value from the current stream and advances the current position of the stream by one byte.
                /// </summary>
                /// <returns>true if the byte is nonzero; otherwise, false.</returns>
                public override bool ReadBoolean()
                {
                    bool value = base.ReadBoolean();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads the next byte from the current stream and advances the current position of the stream by one byte.
                /// </summary>
                /// <returns>The next byte read from the current stream.</returns>
                public override byte ReadByte()
                {
                    byte value = base.ReadByte();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads the specified number of bytes from the current stream into a byte array and advances the current position by that number of bytes.
                /// </summary>
                /// <param name="count">The number of bytes to read.</param>
                /// <returns>A byte array containing data read from the underlying stream. This might be less than the number of bytes requested if the end of the stream is reached.</returns>
                public override byte[] ReadBytes(int count)
                {
                    byte[] value = base.ReadBytes(count);
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads an 8-byte floating point value from the current stream and advances the current position of the stream by eight bytes.
                /// </summary>
                /// <returns>An 8-byte floating point value read from the current stream.</returns>
                public override double ReadDouble()
                {
                    double value = base.ReadDouble();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads a 2-byte signed integer from the current stream and advances the current position of the stream by two bytes.
                /// </summary>
                /// <returns>A 2-byte signed integer read from the current stream.</returns>
                public override short ReadInt16()
                {
                    short value = base.ReadInt16();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads a 4-byte signed integer from the current stream and advances the current position of the stream by four bytes.
                /// </summary>
                /// <returns>A 4-byte signed integer read from the current stream.</returns>
                public override int ReadInt32()
                {
                    int value = base.ReadInt32();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads an 8-byte signed integer from the current stream and advances the current position of the stream by eight bytes.
                /// </summary>
                /// <returns>An 8-byte signed integer read from the current stream.</returns>
                public override long ReadInt64()
                {
                    long value = base.ReadInt64();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads a signed byte from this stream and advances the current position of the stream by one byte.
                /// </summary>
                /// <returns> A signed byte read from the current stream.</returns>
                public override sbyte ReadSByte()
                {
                    sbyte value = base.ReadSByte();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads a 4-byte floating point value from the current stream and advances the current position of the stream by four bytes.
                /// </summary>
                /// <returns>A 4-byte floating point value read from the current stream.</returns>
                public override float ReadSingle()
                {
                    float value = base.ReadSingle();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads a 2-byte unsigned integer from the current stream using little-endian encoding and advances the position of the stream by two bytes.
                /// </summary>
                /// <returns>A 2-byte unsigned integer read from this stream.</returns>
                public override ushort ReadUInt16()
                {
                    ushort value = base.ReadUInt16();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads a 4-byte unsigned integer from the current stream and advances the position of the stream by four bytes.
                /// </summary>
                /// <returns>A 4-byte unsigned integer read from this stream.</returns>
                public override uint ReadUInt32()
                {
                    uint value = base.ReadUInt32();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Reads an 8-byte unsigned integer from the current stream and advances the position of the stream by eight bytes.
                /// </summary>
                /// <returns>An 8-byte unsigned integer read from this stream.</returns>
                public override ulong ReadUInt64()
                {
                    ulong value = base.ReadUInt64();
                    this.bufferedTokenWriter.Write(value);
                    return value;
                }

                /// <summary>
                /// Lets the IJsonBinaryBuffer know that it is at the start of a token.
                /// </summary>
                public override void StartToken()
                {
                    this.bufferedTokenWriter.Flush();
                    this.bufferedToken.Position = 0;
                }

                /// <summary>
                /// Lets the IJsonBinaryBuffer know that an end of a token has just been read from it.
                /// </summary>
                public override void EndToken()
                {
                    this.bufferedTokenWriter.Flush();
                    this.tokenLength = (int)this.bufferedToken.Position;
                }

                /// <summary>
                /// Gets the buffered raw json token.
                /// </summary>
                /// <returns>The buffered raw json token.</returns>
                public override IReadOnlyList<byte> GetBufferedRawJsonToken()
                {
                    return new ArraySegment<byte>(this.bufferedToken.GetBuffer(), 0, this.tokenLength);
                }

                /// <summary>
                /// Gets a binary reader whose position is at the beginning of the provided jsonToken.
                /// </summary>
                /// <param name="jsonToken">The json token input.</param>
                /// <returns>A binary reader whose position is at the beginning of the provided jsonToken.</returns>
                protected override BinaryReader GetBinaryReaderAtToken(ArraySegment<byte> jsonToken)
                {
                    this.bufferedTokenReader.BaseStream.Position = 0;
                    return this.bufferedTokenReader;
                }
            }
            #endregion

            #region ProgressStack
            private class ProgressStack
            {
                public const int EmptyContext = (int)ContextType.EmptyContext;
                public const int SingleItemContext = (int)ContextType.SingleItemContext;
                public const int SinglePropertyContext = (int)ContextType.SinglePropertyContext;
                private const int JsonMaxNestingDepth = 128;
                private readonly long[] bytesLeftAtNestingLevel;
                private int count;

                public ProgressStack()
                {
                    this.bytesLeftAtNestingLevel = new long[JsonMaxNestingDepth];
                }

                public int Count
                {
                    get
                    {
                        return this.count;
                    }
                }

                public ContextType CurrentContextType
                {
                    get
                    {
                        long returnValue = this.Peek();
                        ContextType contextType;
                        switch (returnValue)
                        {
                            case EmptyContext:
                                contextType = ContextType.EmptyContext;
                                break;

                            case SingleItemContext:
                                contextType = ContextType.SingleItemContext;
                                break;

                            case SinglePropertyContext:
                                contextType = ContextType.SinglePropertyContext;
                                break;

                            default:
                                contextType = ContextType.ContextWithLength;
                                break;
                        }

                        return contextType;
                    }
                }

                public bool IsAtEndOfContext
                {
                    get
                    {
                        return this.count != 0 && this.Peek() == ProgressStack.EmptyContext;
                    }
                }

                public void Push(long contextLength)
                {
                    // JsonObjectState will make sure that the caller doesn't overflow the stack
                    if (!ProgressStack.IsValidContext(contextLength))
                    {
                        throw new InvalidOperationException("Tried to push on an invalid context");
                    }

                    this.bytesLeftAtNestingLevel[this.count++] = contextLength;
                }

                public void PushSingleItemContext()
                {
                    this.Push(SingleItemContext);
                }

                public void PushEmptyContext()
                {
                    this.Push(EmptyContext);
                }

                public void PushSinglePropertyContext()
                {
                    this.Push(SinglePropertyContext);
                }

                public long Pop()
                {
                    return this.bytesLeftAtNestingLevel[--this.count];
                }

                public long Peek()
                {
                    return this.bytesLeftAtNestingLevel[this.count - 1];
                }

                public void UpdateProgress(long progress)
                {
                    for (int i = 0; i < this.count; i++)
                    {
                        if (this.bytesLeftAtNestingLevel[i] > 0)
                        {
                            if (progress > this.bytesLeftAtNestingLevel[i])
                            {
                                throw new InvalidOperationException("Tried to make more progress than there is progress to be made");
                            }

                            this.bytesLeftAtNestingLevel[i] -= progress;
                        }
                        else
                        {
                            if (!(this.bytesLeftAtNestingLevel[i] == SingleItemContext
                                || this.bytesLeftAtNestingLevel[i] == SinglePropertyContext
                                || this.bytesLeftAtNestingLevel[i] == EmptyContext))
                            {
                                throw new InvalidOperationException("Progress Stack got corruputed.");
                            }
                        }
                    }
                }

                private static bool IsValidContext(long context)
                {
                    return context == EmptyContext || context == SingleItemContext || context == SinglePropertyContext || context > 0;
                }
            }
            #endregion
        }
    }
}