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
    public class JsonWriteMicroBenchmarks : JsonMicroBenchmarksBase
    {
        [Benchmark]
        [ArgumentsSource(nameof(Arguments))]
        public void ExecuteWriteMicroBenchmark(
            NamedWriteDelegate namedWriteDelegate,
            BenchmarkSerializationFormat benchmarkSerializationFormat)
        {
            IJsonWriter jsonWriter;
            switch (benchmarkSerializationFormat)
            {
                case BenchmarkSerializationFormat.Text:
                    jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                    break;

                case BenchmarkSerializationFormat.Binary:
                    jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
                    break;

                case BenchmarkSerializationFormat.Newtonsoft:
                    jsonWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();
                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(BenchmarkSerializationFormat)}: '{benchmarkSerializationFormat}'.");
            }

            jsonWriter.WriteArrayStart();

            for (int i = 0; i < 2000000; i++)
            {
                namedWriteDelegate.WriteDelegate(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();
        }

        public IEnumerable<object[]> Arguments()
        {
            foreach (FieldInfo fieldInfo in typeof(NamedWriteDelegates).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                NamedWriteDelegate writeDelegate = (NamedWriteDelegate)fieldInfo.GetValue(null);
                foreach (BenchmarkSerializationFormat format in Enum.GetValues(typeof(BenchmarkSerializationFormat)))
                {
                    yield return new object[] { writeDelegate, format };
                }
            }
        }
    }
}
