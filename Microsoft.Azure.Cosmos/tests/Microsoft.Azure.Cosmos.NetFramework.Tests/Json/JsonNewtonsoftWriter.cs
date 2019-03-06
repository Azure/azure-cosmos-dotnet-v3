//-----------------------------------------------------------------------
// <copyright file="JsonNewtonsoftWriter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract class JsonNewtonsoftWriter : Microsoft.Azure.Cosmos.Json.JsonWriter
    {
        protected Newtonsoft.Json.JsonWriter writer;

        protected JsonNewtonsoftWriter()
            : base(true)
        {
        }

        public override long CurrentLength
        {
            get
            {
                throw new NotImplementedException();
            }
        }

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
    }
}
