using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Cosmos.Json.NewtonsoftInterop
{
    internal sealed class JsonNewtonsoftInteropWriter : JsonNewtonsoftWriter
    {
        public JsonNewtonsoftInteropWriter(Newtonsoft.Json.JsonWriter writer)
            : base(writer)
        {
        }

        public override JsonSerializationFormat SerializationFormat => this.writer is Newtonsoft.Json.JsonTextWriter ? JsonSerializationFormat.Text : JsonSerializationFormat.Binary;

        public override byte[] GetResult()
        {
            throw new NotImplementedException();
        }
    }
}
