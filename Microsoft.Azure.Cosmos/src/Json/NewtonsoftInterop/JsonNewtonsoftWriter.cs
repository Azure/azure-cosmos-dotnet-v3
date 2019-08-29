//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.NewtonsoftInterop
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    internal sealed class JsonNewtonsoftWriter : Microsoft.Azure.Cosmos.Json.JsonWriter
    {
        private readonly Newtonsoft.Json.JsonWriter writer;
        private readonly StringBuilder stringBuilder;

        private JsonNewtonsoftWriter(
            Newtonsoft.Json.JsonWriter writer,
            StringBuilder stringBuilder)
            : base(true)
        {
            if (writer == null)
            {
                throw new ArgumentNullException($"{nameof(writer)}");
            }

            this.writer = writer;
            this.stringBuilder = stringBuilder;
        }

        public static JsonNewtonsoftWriter Create()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Newtonsoft.Json.JsonWriter writer = new Newtonsoft.Json.JsonTextWriter(sw);
            return new JsonNewtonsoftWriter(writer, sb);
        }

        public static JsonNewtonsoftWriter Create(Newtonsoft.Json.JsonWriter writer)
        {
            return new JsonNewtonsoftWriter(writer, null);
        }

        public override long CurrentLength
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override JsonSerializationFormat SerializationFormat => this.writer is Newtonsoft.Json.JsonTextWriter ? JsonSerializationFormat.Text : JsonSerializationFormat.Binary;

        public override void WriteArrayEnd()
        {
            this.writer.WriteEndArray();
        }

        public override void WriteArrayStart()
        {
            this.writer.WriteStartArray();
        }

        public override void WriteBoolValue(bool value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteFieldName(string fieldName)
        {
            this.writer.WritePropertyName(fieldName);
        }

        public override void WriteIntValue(long value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteNullValue()
        {
            this.writer.WriteNull();
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

        public override void WriteNumberValue(double value)
        {
            // Check if the number is an integer
            double truncatedValue = Math.Floor(value);
            if ((truncatedValue == value) && (truncatedValue >= long.MinValue) && (truncatedValue <= long.MaxValue))
            {
                // The number does not have any decimals and fits in a 64-bit value
                this.WriteIntValue((long)value);
                return;
            }

            this.writer.WriteValue(value);
        }

        public override void WriteObjectEnd()
        {
            this.writer.WriteEndObject();
        }

        public override void WriteObjectStart()
        {
            this.writer.WriteStartObject();
        }

        public override void WriteStringValue(string value)
        {
            this.writer.WriteValue(value);
        }

        protected override void WriteRawJsonToken(JsonTokenType jsonTokenType, IReadOnlyList<byte> rawJsonToken)
        {
            throw new NotImplementedException();
        }

        public override byte[] GetResult()
        {
            return Encoding.UTF8.GetBytes(this.stringBuilder.ToString());
        }
    }
}
