//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;

    [MemoryDiagnoser]
    public class JsonReadMicroBenchmarks : JsonMicroBenchmarksBase
    {
        [Benchmark]
        [ArgumentsSource(nameof(Arguments))]
        public void Execute(
            BenchmarkPayload payload,
            BenchmarkSerializationFormat benchmarkSerializationFormat,
            bool materializeValue)
        {
            IJsonReader jsonReader = benchmarkSerializationFormat switch
            {
                BenchmarkSerializationFormat.Text => JsonReader.Create(payload.Text),
                BenchmarkSerializationFormat.Binary => JsonReader.Create(payload.Binary),
                BenchmarkSerializationFormat.Newtonsoft => NewtonsoftToCosmosDBReader.CreateFromBuffer(payload.Newtonsoft),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(BenchmarkSerializationFormat)}: '{benchmarkSerializationFormat}'."),
            };

            Utils.DrainReader(jsonReader, materializeValue);
        }

        public IEnumerable<object[]> Arguments()
        {
            foreach (FieldInfo fieldInfo in typeof(Payloads).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                BenchmarkPayload payload = (BenchmarkPayload)fieldInfo.GetValue(null);
                foreach (BenchmarkSerializationFormat sourceFormat in Enum.GetValues(typeof(BenchmarkSerializationFormat)))
                {
                    foreach (bool materializeValue in new bool[] { false, true })
                    {
                        yield return new object[] { payload, sourceFormat, materializeValue };
                    }
                }
            }
        }
    }
}
