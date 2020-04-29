namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;

    internal readonly struct BenchmarkPayload
    {
        public BenchmarkPayload(Action<IJsonWriter> writeToken)
        {
            if (writeToken == null)
            {
                throw new ArgumentNullException(nameof(writeToken));
            }

            IJsonWriter jsonTextWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            IJsonWriter jsonBinaryWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            IJsonWriter jsonNewtonsoftWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();

            jsonTextWriter.WriteArrayStart();
            jsonBinaryWriter.WriteArrayStart();
            jsonNewtonsoftWriter.WriteArrayStart();

            for (int i = 0; i < 1000000; i++)
            {
                writeToken(jsonTextWriter);
                writeToken(jsonBinaryWriter);
                writeToken(jsonNewtonsoftWriter);
            }

            jsonTextWriter.WriteArrayEnd();
            jsonBinaryWriter.WriteArrayEnd();
            jsonNewtonsoftWriter.WriteArrayEnd();

            this.Text = jsonTextWriter.GetResult();
            this.Binary = jsonTextWriter.GetResult();
            this.Newtonsoft = jsonNewtonsoftWriter.GetResult();
        }

        public ReadOnlyMemory<byte> Text { get; }
        public ReadOnlyMemory<byte> Binary { get; }
        public ReadOnlyMemory<byte> Newtonsoft { get; }
    }
}
