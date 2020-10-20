// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    [MemoryDiagnoser]
    public class HeaderBenchmark
    {
        public HeaderBenchmark()
        {
            
        }

        [Benchmark]
        public void StoreRequestHeaders()
        {
            new StoreRequestHeaders();
        }

        [Benchmark]
        public void DictionaryNameValueCollection()
        {
            new DictionaryNameValueCollection();
        }
    }
}