//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// JsonReader partial.
    /// </summary>
    internal abstract partial class JsonReader : IJsonReader
    {
        /// <summary>
        /// JsonReader that knows how to read text
        /// </summary>
        private sealed class JsonTextReader : JsonReader
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

            /// <summary>
            /// Array for null literal character array.
            /// </summary>
            private static readonly char[] Null = { 'n', 'u', 'l', 'l' };

            /// <summary>
            /// Array for true literal character array.
            /// </summary>
            private static readonly char[] True = { 't', 'r', 'u', 'e' };

            /// <summary>
            /// Array for false literal character array.
            /// </summary>
            private static readonly char[] False = { 'f', 'a', 'l', 's', 'e' };

            // See http://www.ietf.org/rfc/rfc4627.txt for JSON whitespace definition (Section 2).
            private static readonly HashSet<char> WhitespaceSet = new HashSet<char> { ' ', '\t', '\r', '\n' };

            private readonly IJsonTextBuffer jsonTextBuffer;
            private bool hasSeperator;

            /// <summary>
            /// Initializes a new instance of the JsonTextReader class.
            /// </summary>
            /// <param name="buffer">The byte array to read from (assumes UTF8 encoding)</param>
            /// <param name="skipValidation">Whether or not to skip validation</param>
            public JsonTextReader(byte[] buffer, bool skipValidation = false)
                : this(new JsonTextArrayBuffer(buffer), skipValidation)
            {
            }

            /// <summary>
            /// Initializes a new instance of the JsonTextReader class.
            /// </summary>
            /// <param name="stream">The stream to read from</param>
            /// <param name="encoding">The encoding of the stream</param>
            /// <param name="skipValidation">Whether to skip validation.</param>
            public JsonTextReader(Stream stream, Encoding encoding, bool skipValidation)
                : this(new JsonTextStreamBuffer(stream, encoding), skipValidation)
            {
            }

            /// <summary>
            /// Initializes a new instance of the JsonTextReader class.
            /// </summary>
            /// <param name="jsonTextBuffer">The IJsonTextBuffer to read from.</param>
            /// <param name="skipValidation">Whether or not to skip validation.</param>
            private JsonTextReader(IJsonTextBuffer jsonTextBuffer, bool skipValidation = false)
                : base(skipValidation)
            {
                if (jsonTextBuffer == null)
                {
                    throw new ArgumentNullException("jsonTextBuffer");
                }

                this.jsonTextBuffer = jsonTextBuffer;
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

            #region IJsonTextBuffer
            /// <summary>
            /// Interface for JsonTextBuffers, which are responsible for buffering text tokens for <see cref="JsonTextReader"/>s.
            /// </summary>
            private interface IJsonTextBuffer
            {
                /// <summary>
                /// Gets a value indicating whether the buffer is at the End of File for it's source.
                /// </summary>
                bool IsEof { get; }

                /// <summary>
                /// Gets a value indicating the encoding for the buffer, which is needed when you want to materialize a string from the buffered raw json token.
                /// </summary>
                Encoding Encoding { get; }

                JsonTextTokenType CurrentTokenType { get; set; }

                /// <summary>
                /// Gets the number of value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The number of value of the token that was just read from the buffer.</returns>
                Number64 GetNumberValue();

                /// <summary>
                /// Gets the Int8 value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The Int8 value of the token that was just read from the buffer.</returns>
                sbyte GetInt8Value();

                /// <summary>
                /// Gets the Int16 value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The Int16 value of the token that was just read from the buffer.</returns>
                short GetInt16Value();

                /// <summary>
                /// Gets the Int32 value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The Int32 value of the token that was just read from the buffer.</returns>
                int GetInt32Value();

                /// <summary>
                /// Gets the Int64 value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The Int64 value of the token that was just read from the buffer.</returns>
                long GetInt64Value();

                /// <summary>
                /// Gets the UInt32 value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The UInt32 value of the token that was just read from the buffer.</returns>
                uint GetUInt32Value();

                /// <summary>
                /// Gets the float value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The float value of the token that was just read from the buffer.</returns>
                float GetFloat32Value();

                /// <summary>
                /// Gets the float value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The float value of the token that was just read from the buffer.</returns>
                double GetFloat64Value();

                /// <summary>
                /// Gets the string value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The string value of the token that was just read from the buffer.</returns>
                string GetStringValue();

                IReadOnlyList<byte> GetBinaryValue();

                Guid GetGuidValue();

                /// <summary>
                /// Lets the buffer know that a token is about to be read.
                /// </summary>
                void BeginToken();

                /// <summary>
                /// Lets the buffer know that a token just finished reading.
                /// </summary>
                void EndToken();

                /// <summary>
                /// Reads a character from the buffer.
                /// </summary>
                /// <returns>The character that was just read.</returns>
                char ReadCharacter();

                /// <summary>
                /// Peeks at the next character from the buffer.
                /// </summary>
                /// <returns>The character that was just peeked at.</returns>
                char PeekCharacter();

                char PeekCharacter(int index);

                /// <summary>
                /// Gets the buffered raw json token from the buffer.
                /// </summary>
                /// <returns>The buffered raw json token from the buffer.</returns>
                IReadOnlyList<byte> GetBufferedRawJsonToken();
            }
            #endregion

            /// <summary>
            /// Gets the SerializationFormat for the JsonReader
            /// </summary>
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Text;
                }
            }

            /// <summary>
            /// Advances the JsonReader by one token.
            /// </summary>
            /// <returns>Whether the reader successfully read a token.</returns>
            public override bool Read()
            {
                // Skip past whitespace to the start of the next token
                // (or to the end of the buffer if the whitespace is trailing)
                while (WhitespaceSet.Contains(this.jsonTextBuffer.PeekCharacter()))
                {
                    this.jsonTextBuffer.ReadCharacter();
                }

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

                this.jsonTextBuffer.BeginToken();
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
                        if (this.jsonTextBuffer.PeekCharacter(1) == JsonTextReader.Int32TokenPrefix)
                        {
                            this.ProcessInt64();
                        }
                        else
                        {
                            this.ProcessInt32();
                        }
                        break;

                    case JsonTextReader.UnsignedTokenPrefix:
                        if (this.jsonTextBuffer.PeekCharacter(1) == JsonTextReader.Int32TokenPrefix)
                        {
                            this.ProcessUInt32();
                        }
                        else
                        {
                            throw new JsonUnexpectedTokenException();
                        }
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

                this.jsonTextBuffer.EndToken();
                return true;
            }

            /// <summary>
            /// Gets the next JSON token from the JsonReader as a double.
            /// </summary>
            /// <returns>The next JSON token from the JsonReader as a double.</returns>
            public override Number64 GetNumberValue()
            {
                return this.jsonTextBuffer.GetNumberValue();
            }

            /// <summary>
            /// Gets the next JSON token from the JsonReader as a string.
            /// </summary>
            /// <returns>The next JSON token from the JsonReader as a string.</returns>
            public override string GetStringValue()
            {
                return this.jsonTextBuffer.GetStringValue();
            }

            public override sbyte GetInt8Value()
            {
                return this.jsonTextBuffer.GetInt8Value();
            }

            public override short GetInt16Value()
            {
                return this.jsonTextBuffer.GetInt16Value();
            }

            public override int GetInt32Value()
            {
                return this.jsonTextBuffer.GetInt32Value();
            }

            public override long GetInt64Value()
            {
                return this.jsonTextBuffer.GetInt64Value();
            }

            public override uint GetUInt32Value()
            {
                return this.jsonTextBuffer.GetUInt32Value();
            }

            public override float GetFloat32Value()
            {
                return this.jsonTextBuffer.GetFloat32Value();
            }

            public override double GetFloat64Value()
            {
                return this.jsonTextBuffer.GetFloat64Value();
            }

            public override Guid GetGuidValue()
            {
                return Guid.Parse(
                    this.jsonTextBuffer.Encoding.GetString(
                        this.jsonTextBuffer.GetBufferedRawJsonToken().ToArray()));
            }

            public override IReadOnlyList<byte> GetBinaryValue()
            {
                return this.jsonTextBuffer.GetBufferedRawJsonToken();
            }

            /// <summary>
            /// Gets the next JSON token from the JsonReader as a buffered list of bytes
            /// </summary>
            /// <returns>the next JSON token from the JsonReader as a buffered list of bytes</returns>
            public override IReadOnlyList<byte> GetBufferedRawJsonToken()
            {
                return this.jsonTextBuffer.GetBufferedRawJsonToken();
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
                this.jsonTextBuffer.CurrentTokenType = jsonTextTokenType;
                this.jsonTextBuffer.ReadCharacter();
                this.RegisterToken();
            }

            private void ProcessTrue()
            {
                ////https://tools.ietf.org/html/rfc7159#section-3
                ////true  = %x74.72.75.65      ; true
                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.True;
                this.ProcessLiteral(JsonTextReader.True);
                this.RegisterToken();
            }

            private void ProcessFalse()
            {
                ////https://tools.ietf.org/html/rfc7159#section-3
                ////false = %x66.61.6c.73.65   ; false
                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.False;
                this.ProcessLiteral(JsonTextReader.False);
                this.RegisterToken();
            }

            private void ProcessNull()
            {
                ////https://tools.ietf.org/html/rfc7159#section-3
                ////null  = %x6e.75.6c.6c      ; null
                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Null;
                this.ProcessLiteral(JsonTextReader.Null);
                this.RegisterToken();
            }

            private void ProcessLiteral(char[] literal)
            {
                for (int i = 0; i < literal.Length; i++)
                {
                    char characterRead = this.jsonTextBuffer.ReadCharacter();
                    if (characterRead != literal[i])
                    {
                        throw new JsonInvalidTokenException();
                    }
                }
            }

            private void ProcessNumber()
            {
                this.ProcessNumberValueToken();
                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Number;
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

                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Number;

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
                    this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Number;

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
                    this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Number;
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
                if (!(this.jsonTextBuffer.IsEof || JsonTextReader.WhitespaceSet.Contains(current) || current == '}' || current == ',' || current == ']'))
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

            private void ProcessInt32()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.Int32TokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                this.ProcessIntegerToken(JsonTextTokenType.Int32);
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

            private void ProcessInt64()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.Int32TokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.Int32TokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                this.ProcessIntegerToken(JsonTextTokenType.Int64);
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

                this.jsonTextBuffer.CurrentTokenType = jsonTextTokenType;
                this.RegisterToken();
            }

            private void ProcessFloat32()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.FloatTokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                this.ProcessNumberValueToken();
                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Float32;
                this.RegisterToken();
            }

            private void ProcessFloat64()
            {
                if (this.jsonTextBuffer.ReadCharacter() != JsonTextReader.DoubleTokenPrefix)
                {
                    throw new JsonInvalidTokenException();
                }

                this.ProcessNumberValueToken();
                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Float32;
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

                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Guid;
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
                }

                this.jsonTextBuffer.CurrentTokenType = JsonTextTokenType.Binary;
                this.RegisterToken();
            }

            private void ProcessString()
            {
                this.jsonTextBuffer.CurrentTokenType = this.JsonObjectState.IsPropertyExpected ? JsonTextTokenType.UnescapedFieldName : JsonTextTokenType.UnescapedString;

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
                            this.jsonTextBuffer.CurrentTokenType = (JsonTextTokenType)(this.jsonTextBuffer.CurrentTokenType | JsonTextTokenType.EscapedFlag);
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
                JsonTokenType jsonTokenType = JsonTextReader.JsonTextToJsonTokenType(this.jsonTextBuffer.CurrentTokenType);

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

            #region JsonTextArrayBuffer
            /// <summary>
            /// The <see cref="IJsonTextBuffer"/> for when the source is a byte of array (UTF8 encoding) that knows how to store the last token read from the source.
            /// </summary>
            private sealed class JsonTextArrayBuffer : IJsonTextBuffer
            {
                private readonly byte[] buffer;

                private JsonTextTokenType currentJsonTextTokenType;
                private int currentBeginOffset;
                private int currentEndOffset;
                private int bytesRead;

                /// <summary>
                /// Initializes a new instance of the JsonTextArrayBuffer class.
                /// </summary>
                /// <param name="buffer">The source for the JsonTextArrayBuffer.</param>
                public JsonTextArrayBuffer(byte[] buffer)
                {
                    if (buffer == null)
                    {
                        throw new ArgumentNullException("buffer");
                    }

                    this.buffer = buffer;
                }

                /// <summary>
                /// Gets a value indicating whether the buffer is at the End of File for it's source.
                /// </summary>
                public bool IsEof
                {
                    get
                    {
                        return this.bytesRead == this.buffer.Length;
                    }
                }

                /// <summary>
                /// Gets a value indicating the encoding for the buffer, which is needed when you want to materialize a string from the buffered raw json token.
                /// </summary>
                public Encoding Encoding
                {
                    get
                    {
                        return Encoding.UTF8;
                    }
                }

                public JsonTextTokenType CurrentTokenType
                {
                    get
                    {
                        return this.currentJsonTextTokenType;
                    }

                    set
                    {
                        this.currentJsonTextTokenType = value;
                    }
                }

                /// <summary>
                /// Gets the number value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The number value of the token that was just read from the buffer.</returns>
                public Number64 GetNumberValue()
                {
                    return JsonTextUtil.GetNumberValue((ArraySegment<byte>)this.GetBufferedRawJsonToken());
                }

                public sbyte GetInt8Value()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    long value = JsonTextUtil.GetIntegerValue(new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 1, rawToken.Count - 1));
                    if (value > sbyte.MaxValue || value < sbyte.MinValue)
                    {
                        throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(sbyte).FullName}");
                    }

                    return (sbyte)value;
                }

                public short GetInt16Value()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    long value = JsonTextUtil.GetIntegerValue(new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 1, rawToken.Count - 1));
                    if (value > short.MaxValue || value < short.MinValue)
                    {
                        throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(short).FullName}");
                    }

                    return (short)value;
                }

                public int GetInt32Value()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    long value = JsonTextUtil.GetIntegerValue(new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 1, rawToken.Count - 1));
                    if (value > int.MaxValue || value < int.MinValue)
                    {
                        throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(int).FullName}");
                    }

                    return (int)value;
                }

                public long GetInt64Value()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    long value = JsonTextUtil.GetIntegerValue(new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 1, rawToken.Count - 1));
                    if (value > long.MaxValue || value < long.MinValue)
                    {
                        throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(long).FullName}");
                    }

                    return (long)value;
                }

                public uint GetUInt32Value()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    long value = JsonTextUtil.GetIntegerValue(new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 2, rawToken.Count - 2));
                    if (value > uint.MaxValue || value < uint.MinValue)
                    {
                        throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(uint).FullName}");
                    }

                    return (uint)value;
                }

                public float GetFloat32Value()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    long value = JsonTextUtil.GetIntegerValue(new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 1, rawToken.Count - 1));
                    if (value > float.MaxValue || value < float.MinValue)
                    {
                        throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(float).FullName}");
                    }

                    return (float)value;
                }

                public double GetFloat64Value()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    long value = JsonTextUtil.GetIntegerValue(new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 1, rawToken.Count - 1));
                    if (value > double.MaxValue || value < double.MinValue)
                    {
                        throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(double).FullName}");
                    }

                    return (double)value;
                }

                /// <summary>
                /// Gets the string value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The string value of the token that was just read from the buffer.</returns>
                public string GetStringValue()
                {
                    IReadOnlyList<byte> jsonTextToken = this.GetBufferedRawJsonToken();

                    // Offsetting by an additional character and removing 2 from the length since I want to skip the quotes.
                    ArraySegment<byte> bufferedToken = (ArraySegment<byte>)jsonTextToken;
                    ArraySegment<byte> stringToken = new ArraySegment<byte>(bufferedToken.Array, bufferedToken.Offset + 1, bufferedToken.Count - 2);
                    bool needsUnescaping = this.currentJsonTextTokenType == JsonTextTokenType.EscapedFieldName || this.currentJsonTextTokenType == JsonTextTokenType.EscapedString;
                    if (!needsUnescaping)
                    {
                        return Encoding.UTF8.GetString(stringToken.Array, stringToken.Offset, stringToken.Count);
                    }

                    return JsonTextUtil.UnescapeJson(this.Encoding.GetChars(stringToken.Array, stringToken.Offset, stringToken.Count));
                }

                public IReadOnlyList<byte> GetBinaryValue()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    ArraySegment<byte> offsetToken = new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 1, rawToken.Count - 1);
                    byte[] bytes = offsetToken.Array;
                    int offset = offsetToken.Offset;
                    int count = offsetToken.Count;

                    string encodedString = this.Encoding.GetString(bytes);
                    return System.Convert.FromBase64String(encodedString);
                }

                public Guid GetGuidValue()
                {
                    ArraySegment<byte> rawToken = (ArraySegment<byte>)this.GetBufferedRawJsonToken();
                    ArraySegment<byte> offsetToken = new ArraySegment<byte>(rawToken.Array, rawToken.Offset + 1, rawToken.Count - 1);
                    byte[] bytes = offsetToken.Array;
                    int offset = offsetToken.Offset;
                    int count = offsetToken.Count;
                    string stringGuid = this.Encoding.GetString(bytes, offset, count);
                    return Guid.Parse(stringGuid);
                }

                /// <summary>
                /// Lets the buffer know that a token is about to be read.
                /// </summary>
                public void BeginToken()
                {
                    this.currentBeginOffset = this.bytesRead;
                }

                /// <summary>
                /// Lets the buffer know that a token just finished reading.
                /// </summary>
                public void EndToken()
                {
                    this.currentEndOffset = this.bytesRead;
                }

                /// <summary>
                /// Reads a character from the buffer.
                /// </summary>
                /// <returns>The character that was just read.</returns>
                public char ReadCharacter()
                {
                    char currentCharacter = this.PeekCharacter();
                    if (currentCharacter != (char)0)
                    {
                        this.bytesRead++;
                    }

                    return currentCharacter;
                }

                /// <summary>
                /// Peeks at the next character from the buffer.
                /// </summary>
                /// <returns>The character that was just peeked at.</returns>
                public char PeekCharacter()
                {
                    return this.PeekCharacter(0);
                }

                public char PeekCharacter(int offset)
                {
                    return this.IsEof ? (char)0 : (char)this.buffer[this.bytesRead + offset];
                }

                /// <summary>
                /// Gets the buffered raw json token from the buffer.
                /// </summary>
                /// <returns>The buffered raw json token from the buffer.</returns>
                public IReadOnlyList<byte> GetBufferedRawJsonToken()
                {
                    return new ArraySegment<byte>(
                        this.buffer,
                        this.currentBeginOffset,
                        this.currentEndOffset - this.currentBeginOffset);
                }
            }
            #endregion

            #region JsonTextStreamBuffer
            /// <summary>
            /// The <see cref="IJsonTextBuffer"/> for when the source is a stream of a specific encoding that knows how to store the last token read from the source.
            /// </summary>
            private sealed class JsonTextStreamBuffer : IJsonTextBuffer
            {
                /// <summary>
                /// We need to buffer one token from the stream incase a user wants to materialize it and the stream is not seekable (like a network stream).
                /// </summary>
                private readonly MemoryStream bufferedToken;
                private readonly StreamWriter bufferedTokenWriter;
                private readonly Encoding encoding;
                private readonly StreamReader streamReader;
                private JsonTextTokenType currentJsonTextTokenType;
                private int tokenLength;

                /// <summary>
                /// Initializes a new instance of the JsonTextStreamBuffer class.
                /// </summary>
                /// <param name="stream">The stream to read from.</param>
                /// <param name="encoding">The encoding of the stream.</param>
                public JsonTextStreamBuffer(Stream stream, Encoding encoding)
                {
                    this.streamReader = new StreamReader(stream, encoding);

                    this.bufferedToken = new MemoryStream();
                    this.bufferedTokenWriter = new StreamWriter(this.bufferedToken, encoding);
                    this.encoding = encoding;
                }

                /// <summary>
                /// Gets a value indicating whether the buffer is at the End of File for it's source.
                /// </summary>
                public bool IsEof
                {
                    get
                    {
                        return this.streamReader.EndOfStream;
                    }
                }

                /// <summary>
                /// Gets a value indicating the encoding for the buffer, which is needed when you want to materialize a string from the buffered raw json token.
                /// </summary>
                public Encoding Encoding
                {
                    get
                    {
                        return this.encoding;
                    }
                }

                public JsonTextTokenType CurrentTokenType
                {
                    get
                    {
                        return this.currentJsonTextTokenType;
                    }

                    set
                    {
                        this.currentJsonTextTokenType = value;
                    }
                }

                /// <summary>
                /// Gets the number of value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The number of value of the token that was just read from the buffer.</returns>
                public Number64 GetNumberValue()
                {
                    string stringDouble = this.encoding.GetString(this.bufferedToken.GetBuffer(), 0, this.tokenLength);
                    return double.Parse(stringDouble, CultureInfo.InvariantCulture);
                }

                public sbyte GetInt8Value()
                {
                    string stringified = this.encoding.GetString(
                        this.bufferedToken.GetBuffer(),
                        this.GetBytesPerChar(), 
                        this.tokenLength - this.GetBytesPerChar());
                    return sbyte.Parse(stringified, CultureInfo.InvariantCulture);
                }

                public short GetInt16Value()
                {
                    string stringified = this.encoding.GetString(
                        this.bufferedToken.GetBuffer(),
                        this.GetBytesPerChar(),
                        this.tokenLength - this.GetBytesPerChar());
                    return short.Parse(stringified, CultureInfo.InvariantCulture);
                }

                public int GetInt32Value()
                {
                    string stringified = this.encoding.GetString(
                        this.bufferedToken.GetBuffer(),
                        this.GetBytesPerChar(),
                        this.tokenLength - this.GetBytesPerChar());
                    return int.Parse(stringified, CultureInfo.InvariantCulture);
                }

                public long GetInt64Value()
                {
                    string stringified = this.encoding.GetString(
                        this.bufferedToken.GetBuffer(),
                        this.GetBytesPerChar(),
                        this.tokenLength - this.GetBytesPerChar());
                    return long.Parse(stringified, CultureInfo.InvariantCulture);
                }

                public uint GetUInt32Value()
                {
                    string stringified = this.encoding.GetString(
                        this.bufferedToken.GetBuffer(),
                        2 * this.GetBytesPerChar(),
                        this.tokenLength - (2 * this.GetBytesPerChar()));
                    return uint.Parse(stringified, CultureInfo.InvariantCulture);
                }

                public float GetFloat32Value()
                {
                    string stringified = this.encoding.GetString(
                        this.bufferedToken.GetBuffer(),
                        this.GetBytesPerChar(),
                        this.tokenLength - this.GetBytesPerChar());
                    return float.Parse(stringified, CultureInfo.InvariantCulture);
                }

                public double GetFloat64Value()
                {
                    string stringified = this.encoding.GetString(
                        this.bufferedToken.GetBuffer(),
                        this.GetBytesPerChar(),
                        this.tokenLength - this.GetBytesPerChar());
                    return double.Parse(stringified, CultureInfo.InvariantCulture);
                }

                /// <summary>
                /// Gets the string value of the token that was just read from the buffer.
                /// </summary>
                /// <returns>The string value of the token that was just read from the buffer.</returns>
                public string GetStringValue()
                {
                    // Get the string from the buffer but don't include the quotes;
                    int quoteSizeInBytes;
                    if (this.encoding == Encoding.UTF8)
                    {
                        quoteSizeInBytes = 1;
                    }
                    else if (this.encoding == Encoding.Unicode)
                    {
                        quoteSizeInBytes = 2;
                    }
                    else
                    {
                        // UTF-32
                        quoteSizeInBytes = 4;
                    }

                    bool needsUnescaping = this.currentJsonTextTokenType == JsonTextTokenType.EscapedFieldName || this.currentJsonTextTokenType == JsonTextTokenType.EscapedString;

                    if (!needsUnescaping)
                    {
                        // Offsetting by an additional character and removing 2 from the length since I want to skip the quotes.
                        return this.Encoding.GetString(this.bufferedToken.GetBuffer(), quoteSizeInBytes, this.tokenLength - (2 * quoteSizeInBytes));
                    }

                    char[] escapedString = this.encoding.GetChars(this.bufferedToken.GetBuffer(), quoteSizeInBytes, this.tokenLength - (2 * quoteSizeInBytes));
                    return JsonTextUtil.UnescapeJson(escapedString);
                }

                public IReadOnlyList<byte> GetBinaryValue()
                {
                    string stringified = this.encoding.GetString(this.bufferedToken.GetBuffer());
                    return Convert.FromBase64String(stringified);
                }

                public Guid GetGuidValue()
                {
                    string stringified = this.encoding.GetString(this.bufferedToken.GetBuffer(), 1, this.tokenLength);
                    return Guid.Parse(stringified);
                }

                /// <summary>
                /// Lets the buffer know that a token is about to be read.
                /// </summary>
                public void BeginToken()
                {
                    this.bufferedTokenWriter.Flush();
                    this.bufferedToken.Position = 0;
                }

                /// <summary>
                /// Lets the buffer know that a token just finished reading.
                /// </summary>
                public void EndToken()
                {
                    this.bufferedTokenWriter.Flush();
                    this.tokenLength = (int)this.bufferedToken.Position;
                }

                /// <summary>
                /// Reads a character from the buffer.
                /// </summary>
                /// <returns>The character that was just read.</returns>
                public char ReadCharacter()
                {
                    if (this.IsEof)
                    {
                        return (char)0;
                    }

                    char characterRead = (char)this.streamReader.Read();
                    this.bufferedTokenWriter.Write(characterRead);
                    return characterRead;
                }

                /// <summary>
                /// Peeks at the next character from the buffer.
                /// </summary>
                /// <returns>The character that was just peeked at.</returns>
                public char PeekCharacter()
                {
                    return this.IsEof ? (char)0 : (char)this.streamReader.Peek();
                }

                public char PeekCharacter(int index)
                {
                    char returnValue;
                    if (this.IsEof)
                    {
                        returnValue = (char)0;
                    }
                    else
                    {
                        this.streamReader.BaseStream.Position += index;
                        returnValue = (char)this.streamReader.Peek();
                        this.streamReader.BaseStream.Position -= index;
                    }

                    return returnValue;
                }

                /// <summary>
                /// Gets the buffered raw json token from the buffer.
                /// </summary>
                /// <returns>The buffered raw json token from the buffer.</returns>
                public IReadOnlyList<byte> GetBufferedRawJsonToken()
                {
                    return new ArraySegment<byte>(this.bufferedToken.GetBuffer(), 0, this.tokenLength);
                }

                private int GetBytesPerChar()
                {
                    int bytesPerChar;
                    if (this.encoding == Encoding.UTF8)
                    {
                        bytesPerChar = 1;
                    }
                    else if (this.encoding == Encoding.Unicode)
                    {
                        bytesPerChar = 2;
                    }
                    else
                    {
                        // UTF-32
                        bytesPerChar = 4;
                    }

                    return bytesPerChar;
                }
            }
            #endregion
        }
    }
}
