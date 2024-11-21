//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    /// <summary>
    /// JsonReader partial.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonReader : IJsonReader
    {
        /// <summary>
        /// JsonReader that knows how to read text
        /// </summary>
        private sealed class JsonTextReader : JsonReader, IJsonTextReaderPrivateImplementation
        {
            private const char Int8TokenPrefix = 'I';
            private const char Int16TokenPrefix = 'H';
            private const char Int32TokenPrefix = 'L';
            private const char UnsignedTokenPrefix = 'U';
            private const char FloatTokenPrefix = 'S';
            private const char DoubleTokenPrefix = 'D';
            private const char GuidTokenPrefix = 'G';
            private const char BinaryTokenPrefix = 'B';

            /// <summary>
            /// Set of all escape characters in JSON.
            /// </summary>
            private static readonly HashSet<char> EscapeCharacters = new HashSet<char> { 'b', 'f', 'n', 'r', 't', '\\', '"', '/', 'u' };

            private readonly JsonTextMemoryReader jsonTextBuffer;
            private TokenState token;
            private bool hasSeperator;

            /// <summary>
            /// Initializes a new instance of the JsonTextReader class.
            /// </summary>
            /// <param name="buffer">The IJsonTextBuffer to read from.</param>
            public JsonTextReader(ReadOnlyMemory<byte> buffer)
            {
                this.jsonTextBuffer = new JsonTextMemoryReader(buffer);
            }

            /// <summary>
            /// Enum of JsonTextTokenType with extends the enum in JsonTokenType.
            /// </summary>
            private enum JsonTextTokenType
            {
                /// <summary>
                /// Flag for escaped characters.
                /// </summary>
                EscapedFlag = 0x10000,

                /// <summary>
                /// Flag for whether a number is a float
                /// </summary>
                FloatFlag = 0x10000,

                /// <summary>
                /// Reserved for no other value
                /// </summary>
                NotStarted = JsonTokenType.NotStarted,

                /// <summary>
                /// Corresponds to the beginning of a JSON array ('[')
                /// </summary>
                BeginArray = JsonTokenType.BeginArray,

                /// <summary>
                /// Corresponds to the end of a JSON array (']')
                /// </summary>
                EndArray = JsonTokenType.EndArray,

                /// <summary>
                /// Corresponds to the beginning of a JSON object ('{')
                /// </summary>
                BeginObject = JsonTokenType.BeginObject,

                /// <summary>
                /// Corresponds to the end of a JSON object ('}')
                /// </summary>
                EndObject = JsonTokenType.EndObject,

                /// <summary>
                /// Corresponds to a JSON string.
                /// </summary>
                UnescapedString = JsonTokenType.String,

                /// <summary>
                /// Corresponds to an escaped JSON string.
                /// </summary>
                EscapedString = JsonTokenType.String | EscapedFlag,

                /// <summary>
                /// Corresponds to a JSON number.
                /// </summary>
                Number = JsonTokenType.Number,
                Int8 = JsonTokenType.Int8,
                Int16 = JsonTokenType.Int16,
                Int32 = JsonTokenType.Int32,
                UInt32 = JsonTokenType.UInt32,
                Int64 = JsonTokenType.Int64,
                Float32 = JsonTokenType.Float32,
                Float64 = JsonTokenType.Float64,

                /// <summary>
                /// Corresponds to the JSON 'true' value.
                /// </summary>
                True = JsonTokenType.True,

                /// <summary>
                /// Corresponds to the JSON 'false' value.
                /// </summary>
                False = JsonTokenType.False,

                /// <summary>
                /// Corresponds to the JSON 'null' value.
                /// </summary>
                Null = JsonTokenType.Null,

                /// <summary>
                /// Corresponds to the JSON fieldname in a JSON object.
                /// </summary>
                UnescapedFieldName = JsonTokenType.FieldName,

                /// <summary>
                /// Corresponds to the an escaped JSON fieldname in a JSON object.
                /// </summary>
                EscapedFieldName = JsonTokenType.FieldName | EscapedFlag,

                Guid = JsonTokenType.Guid,
                Binary = JsonTokenType.Binary,
            }

            #region IJsonReader
            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Text;

            /// <inheritdoc />
            public override bool Read()
            {
                // Skip past whitespace to the start of the next token
                // (or to the end of the buffer if the whitespace is trailing)
                this.jsonTextBuffer.AdvanceWhileWhitespace();

                if (this.jsonTextBuffer.IsEof)
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
                            throw new JsonNotCompleteException();
                        }
                    }

                    return false;
                }

                this.token.Start = this.jsonTextBuffer.Position;
                char nextChar = this.jsonTextBuffer.PeekCharacter();

                switch (nextChar)
                {
                    case '\"':
                        {
                            this.ProcessString();
                            break;
                        }

                    case '-':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        this.ProcessNumber();
                        break;

                    case '[':
                        this.ProcessSingleByteToken(JsonTextTokenType.BeginArray);
                        break;

                    case ']':
                        this.ProcessSingleByteToken(JsonTextTokenType.EndArray);
                        break;

                    case '{':
                        this.ProcessSingleByteToken(JsonTextTokenType.BeginObject);
                        break;

                    case '}':
                        this.ProcessSingleByteToken(JsonTextTokenType.EndObject);
                        break;

                    case 't':
                        this.ProcessTrue();
                        break;

                    case 'f':
                        this.ProcessFalse();
                        break;

                    case 'n':
                        this.ProcessNull();
                        break;

                    case ',':
                        this.ProcessValueSeparator();
                        break;

                    case ':':
                        this.ProcessNameSeparator();
                        break;

                    case JsonTextReader.Int8TokenPrefix:
                        this.ProcessInt8();
                        break;

                    case JsonTextReader.Int16TokenPrefix:
                        this.ProcessInt16();
                        break;

                    case JsonTextReader.Int32TokenPrefix:
                        this.ProcessInt32OrInt64();
                        break;

                    case JsonTextReader.UnsignedTokenPrefix:
                        this.ProcessUInt32();
                        break;

                    case JsonTextReader.FloatTokenPrefix:
                        this.ProcessFloat32();
                        break;

                    case JsonTextReader.DoubleTokenPrefix:
                        this.ProcessFloat64();
                        break;

                    case JsonTextReader.GuidTokenPrefix:
                        this.ProcessGuid();
                        break;

                    case JsonTextReader.BinaryTokenPrefix:
                        this.ProcessBinary();
                        break;

                    default:
                        // We found a start token character which doesn't match any JSON token type
                        throw new JsonUnexpectedTokenException();
                }

                this.token.End = this.jsonTextBuffer.Position;
                return true;
            }

            /// <inheritdoc />
            public override Number64 GetNumberValue()
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(this.token.Start, this.token.End).Span;
                return JsonTextParser.GetNumberValue(numberToken);
            }

            /// <inheritdoc />
            public override UtfAnyString GetStringValue()
            {
                if (this.TryGetBufferedStringValue(out Utf8Memory memory))
                {
                    return Utf8String.UnsafeFromUtf8BytesNoValidation(memory.Memory);
                }

                ReadOnlyMemory<byte> stringToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End);

                return JsonTextParser.GetStringValue(Utf8Memory.UnsafeCreateNoValidation(stringToken));
            }

            /// <inheritdoc />
            public override bool TryGetBufferedStringValue(out Utf8Memory value)
            {
                if (this.token.JsonTextTokenType.HasFlag(JsonTextTokenType.EscapedFlag))
                {
                    value = default;
                    return false;
                }

                // Remove the quotes.
                value = Utf8Memory.UnsafeCreateNoValidation(
                    this.jsonTextBuffer.GetBufferedRawJsonToken(
                        this.token.Start + 1,
                        this.token.End - 1));
                return true;
            }

            /// <inheritdoc />
            public override sbyte GetInt8Value()
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetInt8Value(numberToken);
            }

            /// <inheritdoc />
            public override short GetInt16Value()
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetInt16Value(numberToken);
            }

            /// <inheritdoc />
            public override int GetInt32Value()
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetInt32Value(numberToken);
            }

            /// <inheritdoc />
            public override long GetInt64Value()
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetInt64Value(numberToken);
            }

            /// <inheritdoc />
            public override uint GetUInt32Value()
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetUInt32Value(numberToken);
            }

            /// <inheritdoc />
            public override float GetFloat32Value()
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetFloat32Value(numberToken);
            }

            /// <inheritdoc />
            public override double GetFloat64Value()
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetFloat64Value(numberToken);
            }

            /// <inheritdoc />
            public override Guid GetGuidValue()
            {
                ReadOnlySpan<byte> guidToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetGuidValue(guidToken);
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetBinaryValue()
            {
                ReadOnlySpan<byte> binaryToken = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End).Span;
                return JsonTextParser.GetBinaryValue(binaryToken);
            }
            #endregion

            Utf8Memory IJsonTextReaderPrivateImplementation.GetBufferedJsonToken()
            {
                ReadOnlyMemory<byte> bufferedRawJson = this.jsonTextBuffer.GetBufferedRawJsonToken(
                    this.token.Start,
                    this.token.End);
                return Utf8Memory.UnsafeCreateNoValidation(bufferedRawJson);
            }

            protected override bool TryGetUInt64NumberValue(out ulong value)
            {
                ReadOnlySpan<byte> numberToken = this.jsonTextBuffer.GetBufferedRawJsonToken(this.token.Start, this.token.End).Span;
                return JsonTextParser.TryGetUInt64Value(numberToken, out value);
            }

            private static JsonTokenType JsonTextToJsonTokenType(JsonTextTokenType jsonTextTokenType)
            {
                return (JsonTokenType)((int)jsonTextTokenType & 0xFFFF);
            }

            private void ProcessSingleByteToken(JsonTextTokenType jsonTextTokenType)
            {
                ////https://tools.ietf.org/html/rfc7159#section-2
                ////These are the six structural characters:
                ////begin-array     = ws %x5B ws  ; [ left square bracket
                ////begin-object    = ws %x7B ws  ; { left curly bracket
                ////end-array       = ws %x5D ws  ; ] right square bracket
                ////end-object      = ws %x7D ws  ; } right curly bracket
                ////name-separator  = ws %x3A ws  ; : colon
                ////value-separator = ws %x2C ws  ; , comma
                this.token.JsonTextTokenType = jsonTextTokenType;
                this.jsonTextBuffer.ReadCharacter();
                this.RegisterToken();
            }

            private void ProcessTrue()
            {
                ////https://tools.ietf.org/html/rfc7159#section-3
                ////true  = %x74.72.75.65      ; true
                this.token.JsonTextTokenType = JsonTextTokenType.True;
                if (!this.jsonTextBuffer.TryReadTrueToken())
                {
                    throw new JsonInvalidTokenException();
                }

                this.RegisterToken();
            }

            private void ProcessFalse()
            {
                ////https://tools.ietf.org/html/rfc7159#section-3
                ////false = %x66.61.6c.73.65   ; false
                this.token.JsonTextTokenType = JsonTextTokenType.False;
                if (!this.jsonTextBuffer.TryReadFalseToken())
                {
                    throw new JsonInvalidTokenException();
                }

                this.RegisterToken();
            }

            private void ProcessNull()
            {
                ////https://tools.ietf.org/html/rfc7159#section-3
                ////null  = %x6e.75.6c.6c      ; null
                this.token.JsonTextTokenType = JsonTextTokenType.Null;
                if (!this.jsonTextBuffer.TryReadNullToken())
                {
                    throw new JsonInvalidTokenException();
                }

                this.RegisterToken();
            }

            private void ProcessNumber()
            {
                this.ProcessNumberValueToken();
                this.token.JsonTextTokenType = JsonTextTokenType.Number;
                this.RegisterToken();
            }

            private void ProcessNumberValueToken()
            {
                ////https://tools.ietf.org/html/rfc7159#section-6
                ////number = [ minus ] int [ frac ] [ exp ]
                ////decimal-point = %x2E       ; .
                ////digit1-9 = %x31-39         ; 1-9
                ////e = %x65 / %x45            ; e E
                ////exp = e [ minus / plus ] 1*DIGIT
                ////frac = decimal-point 1*DIGIT
                ////int = zero / ( digit1-9 *DIGIT )
                ////minus = %x2D               ; -
                ////plus = %x2B                ; +
                ////zero = %x30                ; 

                this.token.JsonTextTokenType = JsonTextTokenType.Number;

                // Check for optional sign.
                if (this.jsonTextBuffer.PeekCharacter() == '-')
                {
                    this.jsonTextBuffer.ReadCharacter();
                }

                // There MUST best at least one digit before the dot
                if (!char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                {
                    throw new JsonInvalidNumberException();
                }

                // Only zero or a float can have a zero
                if (this.jsonTextBuffer.PeekCharacter() == '0')
                {
                    this.jsonTextBuffer.ReadCharacter();
                    if (this.jsonTextBuffer.PeekCharacter() == '0')
                    {
                        throw new JsonInvalidNumberException();
                    }
                }
                else
                {
                    // Read all digits before the dot
                    while (char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                    {
                        this.jsonTextBuffer.ReadCharacter();
                    }
                }

                // Check for optional '.'
                if (this.jsonTextBuffer.PeekCharacter() == '.')
                {
                    this.token.JsonTextTokenType = JsonTextTokenType.Number;

                    this.jsonTextBuffer.ReadCharacter();

                    // There MUST best at least one digit after the dot
                    if (!char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                    {
                        throw new JsonInvalidNumberException();
                    }

                    // Read all digits after the dot
                    while (char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                    {
                        this.jsonTextBuffer.ReadCharacter();
                    }
                }

                // Check for optional e/E.
                if (this.jsonTextBuffer.PeekCharacter() == 'e' || this.jsonTextBuffer.PeekCharacter() == 'E')
                {
                    this.token.JsonTextTokenType = JsonTextTokenType.Number;
                    this.jsonTextBuffer.ReadCharacter();

                    // Check for optional +/- after e/E.
                    if (this.jsonTextBuffer.PeekCharacter() == '+' || this.jsonTextBuffer.PeekCharacter() == '-')
                    {
                        this.jsonTextBuffer.ReadCharacter();
                    }

                    // There MUST best at least one digit after the e/E and optional +/-
                    if (!char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                    {
                        throw new JsonInvalidNumberException();
                    }

                    // Read all digits after the e/E
                    while (char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                    {
                        this.jsonTextBuffer.ReadCharacter();
                    }
                }

                // Make sure no gargbage came after the number
                char current = this.jsonTextBuffer.PeekCharacter();
                if (!(this.jsonTextBuffer.IsEof || JsonTextMemoryReader.IsWhitespace(current) || current == '}' || current == ',' || current == ']'))
                {
                    throw new JsonInvalidNumberException();
                }
            }

            private void ProcessInt8()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.Int8TokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                this.ProcessIntegerToken(JsonTextTokenType.Int8);
            }

            private void ProcessInt16()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.Int16TokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                this.ProcessIntegerToken(JsonTextTokenType.Int16);
            }

            private void ProcessInt32OrInt64()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.Int32TokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                if (this.jsonTextBuffer.PeekCharacter() == JsonTextReader.Int32TokenPrefix)
                {
                    if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.Int32TokenPrefix)
                    {
                        throw new JsonInvalidTokenException();
                    }

                    this.ProcessIntegerToken(JsonTextTokenType.Int64);
                }
                else
                {
                    this.ProcessIntegerToken(JsonTextTokenType.Int32);
                }
            }

            private void ProcessUInt32()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.UnsignedTokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.Int32TokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                // First character must be a digit.
                if (!char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                {
                    throw new JsonInvalidNumberException();
                }

                this.ProcessIntegerToken(JsonTextTokenType.UInt32);
            }

            private void ProcessIntegerToken(JsonTextTokenType jsonTextTokenType)
            {
                // Check for optional sign.
                if (this.jsonTextBuffer.PeekCharacter() == '-')
                {
                    this.jsonTextBuffer.ReadCharacter();
                }

                // There MUST best at least one digit
                if (!char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                {
                    throw new JsonInvalidNumberException();
                }

                // Read all digits 
                while (char.IsDigit(this.jsonTextBuffer.PeekCharacter()))
                {
                    this.jsonTextBuffer.ReadCharacter();
                }

                this.token.JsonTextTokenType = jsonTextTokenType;
                this.RegisterToken();
            }

            private void ProcessFloat32()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.FloatTokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                this.ProcessNumberValueToken();
                this.token.JsonTextTokenType = JsonTextTokenType.Float32;
                this.RegisterToken();
            }

            private void ProcessFloat64()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.DoubleTokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                this.ProcessNumberValueToken();
                this.token.JsonTextTokenType = JsonTextTokenType.Float64;
                this.RegisterToken();
            }

            private void ProcessGuid()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.GuidTokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                int length = 0;
                while (char.IsLetterOrDigit(this.jsonTextBuffer.PeekCharacter()) || this.jsonTextBuffer.PeekCharacter() == '-')
                {
                    this.jsonTextBuffer.ReadCharacter();
                    length++;
                }

                const int GuidLength = 36;
                if (length != GuidLength)
                {
                    throw new JsonInvalidTokenException();
                }

                this.token.JsonTextTokenType = JsonTextTokenType.Guid;
                this.RegisterToken();
            }

            private void ProcessBinary()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.BinaryTokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                char current = this.jsonTextBuffer.PeekCharacter();
                while (char.IsLetterOrDigit(current) || current == '+' || current == '/' || current == '=')
                {
                    this.jsonTextBuffer.ReadCharacter();
                    current = this.jsonTextBuffer.PeekCharacter();
                }

                this.token.JsonTextTokenType = JsonTextTokenType.Binary;
                this.RegisterToken();
            }

            private void ProcessString()
            {
                this.token.JsonTextTokenType = this.JsonObjectState.IsPropertyExpected ? JsonTextTokenType.UnescapedFieldName : JsonTextTokenType.UnescapedString;

                // Skip the opening quote
                char current = this.jsonTextBuffer.ReadCharacter();
                if (current != '"')
                {
                    throw new JsonUnexpectedTokenException();
                }

                bool registeredToken = false;
                while (!registeredToken)
                {
                    current = this.jsonTextBuffer.ReadCharacter();
                    switch (current)
                    {
                        case '"':
                            this.RegisterToken();
                            registeredToken = true;
                            break;

                        case '\\':
                            this.token.JsonTextTokenType = this.token.JsonTextTokenType | JsonTextTokenType.EscapedFlag;
                            char escapeCharacter = this.jsonTextBuffer.ReadCharacter();
                            if (escapeCharacter == 'u')
                            {
                                // parse escaped unicode of the form "\uXXXX"
                                const int UnicodeEscapeLength = 4;
                                for (int i = 0; i < UnicodeEscapeLength; i++)
                                {
                                    // Just need to make sure that we are getting valid hex characters
                                    char unicodeEscapeCharacter = this.jsonTextBuffer.ReadCharacter();
                                    if (!(
                                        (unicodeEscapeCharacter >= '0' && unicodeEscapeCharacter <= '9') ||
                                        (unicodeEscapeCharacter >= 'a' && unicodeEscapeCharacter <= 'f') ||
                                        (unicodeEscapeCharacter >= 'A' && unicodeEscapeCharacter <= 'F')))
                                    {
                                        throw new JsonInvalidEscapedCharacterException();
                                    }
                                }
                            }
                            else
                            {
                                // Validate and consume the escape character.
                                if (!JsonTextReader.EscapeCharacters.Contains(escapeCharacter))
                                {
                                    throw new JsonInvalidEscapedCharacterException();
                                }
                            }

                            break;

                        case (char)0:
                            if (this.jsonTextBuffer.IsEof)
                            {
                                throw new JsonMissingClosingQuoteException();
                            }

                            break;
                    }
                }
            }

            private void ProcessNameSeparator()
            {
                if (this.hasSeperator || (this.JsonObjectState.CurrentTokenType != JsonTokenType.FieldName))
                {
                    throw new JsonUnexpectedNameSeparatorException();
                }

                this.jsonTextBuffer.ReadCharacter();
                this.hasSeperator = true;

                // We don't surface Json.NameSeparator tokens to the caller, so proceed to the next token
                this.Read();
            }

            private void ProcessValueSeparator()
            {
                if (this.hasSeperator)
                {
                    throw new JsonUnexpectedValueSeparatorException();
                }

                switch (this.JsonObjectState.CurrentTokenType)
                {
                    case JsonTokenType.EndArray:
                    case JsonTokenType.EndObject:
                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                    case JsonTokenType.Int8:
                    case JsonTokenType.Int16:
                    case JsonTokenType.Int32:
                    case JsonTokenType.Int64:
                    case JsonTokenType.UInt32:
                    case JsonTokenType.Float32:
                    case JsonTokenType.Float64:
                    case JsonTokenType.Guid:
                    case JsonTokenType.Binary:
                        // Valid token, if in Array or Object.
                        this.hasSeperator = true;
                        break;
                    default:
                        throw new JsonUnexpectedValueSeparatorException();
                }

                this.jsonTextBuffer.ReadCharacter();

                // We don't surface Json_ValueSeparator tokens to the caller, so proceed to the next token
                this.Read();
            }

            private void RegisterToken()
            {
                JsonTokenType jsonTokenType = JsonTextReader.JsonTextToJsonTokenType(this.token.JsonTextTokenType);

                // Save the previous token before registering the new one
                JsonTokenType previousJsonTokenType = this.JsonObjectState.CurrentTokenType;

                // We register the token with the object state which should take care of all validity checks
                // but the separator check which we take care of below
                this.JsonObjectState.RegisterToken(jsonTokenType);

                switch (jsonTokenType)
                {
                    case JsonTokenType.EndArray:
                        if (this.hasSeperator)
                        {
                            throw new JsonUnexpectedEndArrayException();
                        }

                        break;
                    case JsonTokenType.EndObject:
                        if (this.hasSeperator)
                        {
                            throw new JsonUnexpectedEndObjectException();
                        }

                        break;
                    default:
                        switch (previousJsonTokenType)
                        {
                            case JsonTokenType.NotStarted:
                            case JsonTokenType.BeginArray:
                            case JsonTokenType.BeginObject:
                                // No seperator is required after these tokens
                                Debug.Assert(!this.hasSeperator, "Not valid to have a separator here!");
                                break;
                            case JsonTokenType.FieldName:
                                if (!this.hasSeperator)
                                {
                                    throw new JsonMissingNameSeparatorException();
                                }

                                break;
                            default:
                                if (!this.hasSeperator)
                                {
                                    throw new JsonUnexpectedTokenException();
                                }

                                break;
                        }

                        this.hasSeperator = false;
                        break;
                }
            }

            private sealed class JsonTextMemoryReader : JsonMemoryReader
            {
                private static readonly ReadOnlyMemory<byte> TrueMemory = new byte[] { (byte)'t', (byte)'r', (byte)'u', (byte)'e' };
                private static readonly ReadOnlyMemory<byte> FalseMemory = new byte[] { (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e' };
                private static readonly ReadOnlyMemory<byte> NullMemory = new byte[] { (byte)'n', (byte)'u', (byte)'l', (byte)'l' };

                public JsonTextMemoryReader(ReadOnlyMemory<byte> buffer)
                    : base(buffer)
                {
                }

                public char ReadCharacter()
                {
                    return (char)this.Read();
                }

                public char PeekCharacter()
                {
                    return (char)this.Peek();
                }

                public void AdvanceWhileWhitespace()
                {
                    while (JsonTextMemoryReader.IsWhitespace(this.PeekCharacter()))
                    {
                        this.ReadCharacter();
                    }
                }

                public bool TryReadTrueToken()
                {
                    return this.TryReadToken(JsonTextMemoryReader.TrueMemory.Span);
                }

                public bool TryReadFalseToken()
                {
                    return this.TryReadToken(JsonTextMemoryReader.FalseMemory.Span);
                }

                public bool TryReadNullToken()
                {
                    return this.TryReadToken(JsonTextMemoryReader.NullMemory.Span);
                }

                private bool TryReadToken(ReadOnlySpan<byte> token)
                {
                    if (this.position + token.Length <= this.buffer.Length)
                    {
                        bool read = this.buffer
                            .Slice(this.position, token.Length)
                            .Span
                            .SequenceEqual(token);
                        this.position += token.Length;
                        return read;
                    }

                    return false;
                }

                // See http://www.ietf.org/rfc/rfc4627.txt for JSON whitespace definition (Section 2).
                public static bool IsWhitespace(char value)
                {
                    return value == ' ' || value == '\t' || value == '\r' || value == '\n';
                }
            }

            private struct TokenState
            {
                public JsonTextTokenType JsonTextTokenType { get; set; }

                public int Start { get; set; }

                public int End { get; set; }
            }
        }
    }
}
