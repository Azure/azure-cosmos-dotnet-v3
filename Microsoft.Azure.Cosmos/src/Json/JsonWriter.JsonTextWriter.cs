//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Partial class for the JsonWriter that has a private JsonTextWriter below.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonWriter : IJsonWriter
    {
        /// <summary>
        /// This class is used to build a JSON string.
        /// It supports our defined IJsonWriter interface.
        /// It keeps an stack to keep track of scope, and provides error checking using that.
        /// It has few other variables for error checking
        /// The user can also provide initial size to reserve string buffer, that will help reduce cost of reallocation.
        /// It provides error checking based on JSON grammar. It provides escaping for nine characters specified in JSON.
        /// </summary>
        private sealed class JsonTextWriter : JsonWriter
        {
            private const char ValueSeperatorToken = ':';
            private const char MemberSeperatorToken = ',';
            private const char ObjectStartToken = '{';
            private const char ObjectEndToken = '}';
            private const char ArrayStartToken = '[';
            private const char ArrayEndToken = ']';
            private const char PropertyStartToken = '"';
            private const char PropertyEndToken = '"';
            private const char StringStartToken = '"';
            private const char StringEndToken = '"';

            private const string NotANumber = "NaN";
            private const string PositiveInfinity = "Infinity";
            private const string NegativeInfinity = "-Infinity";
            private const string TrueString = "true";
            private const string FalseString = "false";
            private const string NullString = "null";

            private static readonly Dictionary<Encoding, string> ByteOrderMarkDictionary = new Dictionary<Encoding, string>
            {
                { Encoding.UTF8, "\xEF\xBB\xBF" },
                { Encoding.Unicode, "\xFF\xFE" },
                { Encoding.UTF32, "\xFF\xFE\x00\x00" },
            };

            /// <summary>
            /// The internal StreamWriter
            /// </summary>
            private readonly StreamWriter streamWriter;

            /// <summary>
            /// Whether we are writing the first value of an array or object
            /// </summary>
            private bool firstValue;

            /// <summary>
            /// Initializes a new instance of the JsonTextWriter class.
            /// </summary>
            /// <param name="encoding">The encoding to use.</param>
            /// <param name="skipValidation">Whether or not to skip validation</param>
            public JsonTextWriter(Encoding encoding, bool skipValidation)
                : base(skipValidation)
            {
                this.firstValue = true;
                this.streamWriter = new StreamWriter(new MemoryStream(), encoding);
            }

            /// <summary>
            /// Gets the SerializationFormat of the JsonWriter.
            /// </summary>
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Text;
                }
            }

            /// <summary>
            /// Gets the current length of the internal buffer.
            /// </summary>
            public override long CurrentLength
            {
                get
                {
                    return this.streamWriter.BaseStream.Position;
                }
            }

            /// <summary>
            /// Writes the object start symbol to internal buffer.
            /// </summary>
            public override void WriteObjectStart()
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.BeginObject);
                this.PrefixMemberSeparator();
                this.WriteChar(ObjectStartToken);
                this.firstValue = true;
            }

            /// <summary>
            /// Writes the object end symbol to the internal buffer.
            /// </summary>
            public override void WriteObjectEnd()
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.EndObject);
                this.WriteChar(ObjectEndToken);

                // We reset firstValue here because we'll need a separator before the next value
                this.firstValue = false;
            }

            /// <summary>
            /// Writes the array start symbol to the internal buffer.
            /// </summary>
            public override void WriteArrayStart()
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.BeginArray);
                this.PrefixMemberSeparator();
                this.WriteChar(ArrayStartToken);
                this.firstValue = true;
            }

            /// <summary>
            /// Writes the array end symbol to the internal buffer.
            /// </summary>
            public override void WriteArrayEnd()
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.EndArray);
                this.WriteChar(ArrayEndToken);

                // We reset firstValue here because we'll need a separator before the next value
                this.firstValue = false;
            }

            /// <summary>
            /// Writes a field name to the the internal buffer.
            /// </summary>
            /// <param name="fieldName">The name of the field to write.</param>
            public override void WriteFieldName(string fieldName)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.FieldName);
                this.PrefixMemberSeparator();

                // no separator after property name
                this.firstValue = true;

                this.WriteChar(PropertyStartToken);
                this.WriteEscapedStringToStreamWriter(fieldName, this.streamWriter);
                this.WriteChar(PropertyEndToken);

                this.WriteChar(ValueSeperatorToken);
            }

            /// <summary>
            /// Writes a string to the internal buffer.
            /// </summary>
            /// <param name="value">The value of the string to write.</param>
            public override void WriteStringValue(string value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.String);
                this.PrefixMemberSeparator();

                this.WriteChar(StringStartToken);
                this.WriteEscapedStringToStreamWriter(value, this.streamWriter);
                this.WriteChar(StringEndToken);
            }

            /// <summary>
            /// Writes an integer to the internal buffer.
            /// </summary>
            /// <param name="value">The value of the integer to write.</param>
            public override void WriteIntValue(long value)
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                this.PrefixMemberSeparator();
                this.streamWriter.Write(value.ToString(CultureInfo.InvariantCulture));
            }

            /// <summary>
            /// Writes a number to the internal buffer.
            /// </summary>
            /// <param name="value">The value of the number to write.</param>
            public override void WriteNumberValue(double value)
            {
                // The maximum integer value that can be stored in an IEEE 754 double type w/o losing precision
                const double MaxFullPrecisionValue = ((long)1) << 53;

                // Check if the number is an integer
                double truncatedValue = Math.Floor(value);
                if ((truncatedValue == value) && (value >= -MaxFullPrecisionValue) && (value <= MaxFullPrecisionValue))
                {
                    // The number does not have any decimals and fits in a 64-bit value
                    this.WriteIntValue((long)value);
                    return;
                }

                this.JsonObjectState.RegisterToken(JsonTokenType.Number);
                this.PrefixMemberSeparator();

                if (double.IsNaN(value))
                {
                    this.WriteChar(StringStartToken);
                    this.streamWriter.Write(NotANumber);
                    this.WriteChar(StringEndToken);
                }
                else if (double.IsNegativeInfinity(value))
                {
                    this.WriteChar(StringStartToken);
                    this.streamWriter.Write(NegativeInfinity);
                    this.WriteChar(StringEndToken);
                }
                else if (double.IsPositiveInfinity(value))
                {
                    this.WriteChar(StringStartToken);
                    this.streamWriter.Write(PositiveInfinity);
                    this.WriteChar(StringEndToken);
                }
                else
                {
                    // If you require more precision, specify format with the "G17" format specification, which always returns 17 digits of precision,
                    // or "R", which returns 15 digits if the number can be represented with that precision or 17 digits if the number can only be represented with maximum precision.
                    // In some cases, Double values formatted with the "R" standard numeric format string do not successfully round-trip if compiled using the /platform:x64 or /platform:anycpu switches and run on 64-bit systems. To work around this problem, you can format Double values by using the "G17" standard numeric format string. 
                    this.streamWriter.Write(value.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            /// <summary>
            /// Writes a boolean to the internal buffer.
            /// </summary>
            /// <param name="value">The value of the boolean to write.</param>
            public override void WriteBoolValue(bool value)
            {
                this.JsonObjectState.RegisterToken(value ? JsonTokenType.True : JsonTokenType.False);
                this.PrefixMemberSeparator();

                if (value)
                {
                    this.streamWriter.Write(TrueString);
                }
                else
                {
                    this.streamWriter.Write(FalseString);
                }
            }

            /// <summary>
            /// Writes a null to the internal buffer.
            /// </summary>
            public override void WriteNullValue()
            {
                this.JsonObjectState.RegisterToken(JsonTokenType.Null);
                this.PrefixMemberSeparator();
                this.streamWriter.Write(NullString);
            }

            public override void WriteInt8Value(sbyte value)
            {
                throw new NotImplementedException();
            }

            public override void WriteInt16Value(short value)
            {
                throw new NotImplementedException();
            }

            public override void WriteInt32Value(int value)
            {
                throw new NotImplementedException();
            }

            public override void WriteInt64Value(long value)
            {
                throw new NotImplementedException();
            }

            public override void WriteFloat32Value(float value)
            {
                throw new NotImplementedException();
            }

            public override void WriteFloat64Value(double value)
            {
                throw new NotImplementedException();
            }

            public override void WriteUInt32Value(uint value)
            {
                throw new NotImplementedException();
            }

            public override void WriteGuidValue(Guid value)
            {
                throw new NotImplementedException();
            }

            public override void WriteBinaryValue(IReadOnlyList<byte> value)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Gets the result of the JsonWriter.
            /// </summary>
            /// <returns>The result of the JsonWriter as an array of bytes.</returns>
            public override byte[] GetResult()
            {
                // Flush the stream
                this.streamWriter.Flush();

                // Create a binaryreader to read from the stream
                BinaryReader binaryReader = new BinaryReader(this.streamWriter.BaseStream);

                // Figure out how many bytes have been written
                long bytesWritten = this.CurrentLength;

                // Seek to the begining but skip the byte order mark
                int byteOrderMarkLength = JsonTextWriter.ByteOrderMarkDictionary[this.streamWriter.Encoding].Length;
                this.streamWriter.BaseStream.Seek(byteOrderMarkLength, SeekOrigin.Begin);

                // Read the entire steam
                return binaryReader.ReadBytes((int)(bytesWritten - byteOrderMarkLength));
            }

            /// <summary>
            /// Gets the result of all the writes as a string.
            /// </summary>
            /// <returns>The result of all the writes as a string.</returns>
            public string GetStringResult()
            {
                // Flush the stream
                this.streamWriter.Flush();

                // Create a stream reader to read from the stream
                StreamReader streamReader = new StreamReader(
                    this.streamWriter.BaseStream,
                    this.streamWriter.Encoding);

                // Seek to the begining
                this.streamWriter.BaseStream.Seek(0, SeekOrigin.Begin);

                // Read the entire stream
                string stringResult = streamReader.ReadToEnd();
                return stringResult;
            }

            /// <summary>
            /// Writes a raw json token to the internal buffer.
            /// </summary>
            /// <param name="jsonTokenType">The JsonTokenType of the rawJsonToken</param>
            /// <param name="rawJsonToken">The raw json token.</param>
            protected override void WriteRawJsonToken(JsonTokenType jsonTokenType, IReadOnlyList<byte> rawJsonToken)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Writes a character to the stream.
            /// </summary>
            /// <param name="value">The character to write to the stream.</param>
            private void WriteChar(char value)
            {
                this.streamWriter.Write(value);
            }

            /// <summary>
            /// Will insert a member separator token if one is needed.
            /// </summary>
            private void PrefixMemberSeparator()
            {
                if (!this.firstValue)
                {
                    this.WriteChar(MemberSeperatorToken);
                }

                this.firstValue = false;
            }

            private bool RequiresEscapeSequence(char value)
            {
                switch (value)
                {
                    case '\\':
                    case '"':
                    case '/':
                    case '\b':
                    case '\f':
                    case '\n':
                    case '\r':
                    case '\t':
                        return true;
                    default:
                        return value < ' ';
                }
            }

            private char GetHexDigit(byte value)
            {
                if (value > 0xF)
                {
                    throw new ArgumentException("value must be less than 0xF");
                }

                return (value < 10) ? (char)('0' + value) : (char)('a' + value - 10);
            }

            private void WriteEscapedStringToStreamWriter(string value, StreamWriter streamWriter)
            {
                int readOffset = 0;
                while (readOffset != value.Length)
                {
                    if (!this.RequiresEscapeSequence(value[readOffset]))
                    {
                        // Just write the character as is
                        this.streamWriter.Write(value[readOffset++]);
                    }
                    else
                    {
                        char characterToEscape = value[readOffset++];
                        char escapeSequence = default(char);
                        switch (characterToEscape)
                        {
                            case '\\':
                                escapeSequence = '\\';
                                break;
                            case '"':
                                escapeSequence = '"';
                                break;
                            case '/':
                                escapeSequence = '/';
                                break;
                            case '\b':
                                escapeSequence = 'b';
                                break;
                            case '\f':
                                escapeSequence = 'f';
                                break;
                            case '\n':
                                escapeSequence = 'n';
                                break;
                            case '\r':
                                escapeSequence = 'r';
                                break;
                            case '\t':
                                escapeSequence = 't';
                                break;
                        }

                        if (escapeSequence >= ' ')
                        {
                            // We got a special character
                            streamWriter.Write('\\');
                            streamWriter.Write(escapeSequence);
                        }
                        else
                        {
                            // We got a control character (U+0000 through U+001F).
                            streamWriter.Write('\\');
                            streamWriter.Write('u');
                            streamWriter.Write(this.GetHexDigit((byte)(((byte)characterToEscape >> 12) & 0xF)));
                            streamWriter.Write(this.GetHexDigit((byte)(((byte)characterToEscape >> 8) & 0xF)));
                            streamWriter.Write(this.GetHexDigit((byte)(((byte)characterToEscape >> 4) & 0xF)));
                            streamWriter.Write(this.GetHexDigit((byte)(((byte)characterToEscape) & 0xF)));
                        }
                    }
                }
            }
        }
    }
}
