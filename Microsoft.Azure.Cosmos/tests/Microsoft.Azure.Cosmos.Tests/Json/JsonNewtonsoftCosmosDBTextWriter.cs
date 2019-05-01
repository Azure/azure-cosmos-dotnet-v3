//-----------------------------------------------------------------------
// <copyright file="JsonNewtonsoftCosmosDBTextWriter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Class that implements IJsonWriter, but is really just a wrapper around our Newtonsoft wrapper for text.
    /// </summary>
    internal sealed class JsonNewtonsoftCosmosDBTextWriter : JsonNewtonsoftWriter
    {
        /// <summary>
        /// Initializes a new instance of the JsonNewtonsoftCosmosDBTextWriter class.
        /// </summary>
        public JsonNewtonsoftCosmosDBTextWriter()
        {
            this.writer = new JsonCosmosDBWriter(JsonSerializationFormat.Text);
        }

        /// <summary>
        /// Gets the JsonSerializationFormat.
        /// </summary>
        public override JsonSerializationFormat SerializationFormat
        {
            get
            {
                return JsonSerializationFormat.Text;
            }
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
        /// Gets the result of writing all the tokens.
        /// </summary>
        /// <returns>The result of writing all the tokens.</returns>
        public override byte[] GetResult()
        {
            return ((Microsoft.Azure.Cosmos.Json.JsonCosmosDBWriter)this.writer).GetResult();
        }
    }
}
