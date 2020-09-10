//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.Interop
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Wrapper class that implements Newtonsoft's JsonWriter,
    /// but calls into a CosmosDB JsonWriter for the implementation.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    sealed class CosmosDBToNewtonsoftWriter : Newtonsoft.Json.JsonWriter
    {
        /// <summary>
        /// A CosmosDB JSON writer used for the actual implementation.
        /// </summary>
        private readonly IJsonWriter jsonWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDBToNewtonsoftWriter"/> class.
        /// </summary>
        /// <param name="jsonSerializationFormat">The SerializationFormat to use.</param>
        public CosmosDBToNewtonsoftWriter(JsonSerializationFormat jsonSerializationFormat)
        {
            this.jsonWriter = JsonWriter.Create(jsonSerializationFormat);
        }

        /// <summary>
        /// Flushes whatever is in the buffer to the underlying <see cref="Stream"/> and also flushes the underlying stream.
        /// </summary>
        public override void Flush()
        {
            this.jsonWriter.GetResult();
        }

        /// <summary>
        /// Writes a comment <c>/*...*/</c> containing the specified text.
        /// </summary>
        /// <param name="text">Text to place inside the comment.</param>
        public override void WriteComment(string text)
        {
            throw new NotSupportedException("Cannot write JSON comment.");
        }

        /// <summary>
        /// Writes the end of an array.
        /// </summary>
        public override void WriteEndArray()
        {
            base.WriteEndArray();
            this.jsonWriter.WriteArrayEnd();
        }

        /// <summary>
        /// Writes the end constructor.
        /// </summary>
        public override void WriteEndConstructor()
        {
            throw new NotSupportedException("Cannot write end constructor.");
        }

        /// <summary>
        /// Writes the end of a JSON object.
        /// </summary>
        public override void WriteEndObject()
        {
            base.WriteEndObject();
            this.jsonWriter.WriteObjectEnd();
        }

        /// <summary>
        /// Writes a null value.
        /// </summary>
        public override void WriteNull()
        {
            base.WriteNull();
            this.jsonWriter.WriteNullValue();
        }

        /// <summary>
        /// Writes the property name of a name/value pair on a JSON object.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        public override void WritePropertyName(string name)
        {
            this.WritePropertyName(name, false);
        }

        /// <summary>
        /// Writes the property name of a name/value pair on a JSON object.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="escape">Whether or not to escape the name</param>
        public override void WritePropertyName(string name, bool escape)
        {
            base.WritePropertyName(name);
            this.jsonWriter.WriteFieldName(name);
        }

        /// <summary>
        /// Writes the start of a constructor with the given name.
        /// </summary>
        /// <param name="name">The name of the constructor.</param>
        public override void WriteStartConstructor(string name)
        {
            throw new NotSupportedException("Cannot write Start constructor.");
        }

        /// <summary>
        /// Writes raw JSON.
        /// </summary>
        /// <param name="json">The raw JSON to write.</param>
        public override void WriteRaw(string json)
        {
            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(json));
            jsonReader.WriteAll(this.jsonWriter);
        }

        /// <summary>
        /// Writes raw JSON where a value is expected and updates the writer's state.
        /// </summary>
        /// <param name="json">The raw JSON to write.</param>
        public override void WriteRawValue(string json)
        {
            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(json));
            jsonReader.WriteAll(this.jsonWriter);
        }

        /// <summary>
        /// Writes the beginning of a JSON array.
        /// </summary>
        public override void WriteStartArray()
        {
            base.WriteStartArray();
            this.jsonWriter.WriteArrayStart();
        }

        /// <summary>
        /// Writes the beginning of a JSON object.
        /// </summary>
        public override void WriteStartObject()
        {
            base.WriteStartObject();
            this.jsonWriter.WriteObjectStart();
        }

        /// <summary>
        /// Writes an undefined value.
        /// </summary>
        public override void WriteUndefined()
        {
            throw new NotSupportedException("Can not write undefined");
        }

        #region WriteValue methods
        /// <summary>
        /// Writes a <see cref="Object"/> value.
        /// An error will raised if the value cannot be written as a single JSON token.
        /// </summary>
        /// <param name="value">The <see cref="Object"/> value to write.</param>
        public override void WriteValue(object value)
        {
            if (value is string stringValue)
            {
                this.WriteValue(stringValue);
            }
            else
            {
                this.WriteValue((double)value);
            }
        }

        /// <summary>
        /// Writes a <see cref="String"/> value.
        /// </summary>
        /// <param name="value">The <see cref="String"/> value to write.</param>
        public override void WriteValue(string value)
        {
            base.WriteValue(value);
            this.jsonWriter.WriteStringValue(value);
        }

        /// <summary>
        /// Writes a <see cref="Int32"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Int32"/> value to write.</param>
        public override void WriteValue(int value)
        {
            this.WriteValue((long)value);
        }

        /// <summary>
        /// Writes a <see cref="UInt32"/> value.
        /// </summary>
        /// <param name="value">The <see cref="UInt32"/> value to write.</param>
        public override void WriteValue(uint value)
        {
            this.WriteValue((long)value);
        }

        /// <summary>
        /// Writes a <see cref="Int64"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Int64"/> value to write.</param>
        public override void WriteValue(long value)
        {
            base.WriteValue(value);
            this.jsonWriter.WriteNumber64Value(value);
        }

        /// <summary>
        /// Writes a <see cref="UInt64"/> value.
        /// </summary>
        /// <param name="value">The <see cref="UInt64"/> value to write.</param>
        public override void WriteValue(ulong value)
        {
            if (value <= long.MaxValue)
            {
                this.WriteValue((long)value);
            }
            else
            {
                this.WriteValue((double)value);
            }
        }

        /// <summary>
        /// Writes a <see cref="Single"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Single"/> value to write.</param>
        public override void WriteValue(float value)
        {
            this.WriteValue((double)value);
        }

        /// <summary>
        /// Writes a <see cref="Double"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Double"/> value to write.</param>
        public override void WriteValue(double value)
        {
            base.WriteValue(value);
            this.jsonWriter.WriteNumber64Value(value);
        }

        /// <summary>
        /// Writes a <see cref="Boolean"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Boolean"/> value to write.</param>
        public override void WriteValue(bool value)
        {
            base.WriteValue(value);
            this.jsonWriter.WriteBoolValue(value);
        }

        /// <summary>
        /// Writes a <see cref="Int16"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Int16"/> value to write.</param>
        public override void WriteValue(short value)
        {
            base.WriteValue((long)value);
        }

        /// <summary>
        /// Writes a <see cref="UInt16"/> value.
        /// </summary>
        /// <param name="value">The <see cref="UInt16"/> value to write.</param>
        public override void WriteValue(ushort value)
        {
            this.WriteValue((long)value);
        }

        /// <summary>
        /// Writes a <see cref="Char"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Char"/> value to write.</param>
        public override void WriteValue(char value)
        {
            base.WriteValue(value);
            this.jsonWriter.WriteStringValue(value.ToString());
        }

        /// <summary>
        /// Writes a <see cref="Byte"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Byte"/> value to write.</param>
        public override void WriteValue(byte value)
        {
            this.WriteValue((long)value);
        }

        /// <summary>
        /// Writes a <see cref="SByte"/> value.
        /// </summary>
        /// <param name="value">The <see cref="SByte"/> value to write.</param>
        public override void WriteValue(sbyte value)
        {
            this.WriteValue((long)value);
        }

        /// <summary>
        /// Writes a <see cref="Decimal"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Decimal"/> value to write.</param>
        public override void WriteValue(decimal value)
        {
            this.WriteValue((double)value);
        }

        /// <summary>
        /// Writes a <see cref="DateTime"/> value.
        /// </summary>
        /// <param name="value">The <see cref="DateTime"/> value to write.</param>
        public override void WriteValue(DateTime value)
        {
            this.WriteValue(value.ToString());
        }

        /// <summary>
        /// Writes a <see cref="Byte"/>[] value.
        /// </summary>
        /// <param name="value">The <see cref="Byte"/>[] value to write.</param>
        public override void WriteValue(byte[] value)
        {
            throw new NotSupportedException("Can not write byte arrays");
        }

        /// <summary>
        /// Writes a <see cref="Guid"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Guid"/> value to write.</param>
        public override void WriteValue(Guid value)
        {
            this.WriteValue(value.ToString());
        }

        /// <summary>
        /// Writes a <see cref="TimeSpan"/> value.
        /// </summary>
        /// <param name="value">The <see cref="TimeSpan"/> value to write.</param>
        public override void WriteValue(TimeSpan value)
        {
            this.WriteValue(value.ToString());
        }

        /// <summary>
        /// Writes a <see cref="Uri"/> value.
        /// </summary>
        /// <param name="value">The <see cref="Uri"/> value to write.</param>
        public override void WriteValue(Uri value)
        {
            if (value == null)
            {
                this.WriteNull();
            }
            else
            {
                this.WriteValue(value.ToString());
            }
        }
        #endregion

        /// <summary>
        /// Gets the result of all the tokens written so far.
        /// </summary>
        /// <returns>The result of all the tokens written so far.</returns>
        public ReadOnlyMemory<byte> GetResult()
        {
            return this.jsonWriter.GetResult();
        }
    }
}
