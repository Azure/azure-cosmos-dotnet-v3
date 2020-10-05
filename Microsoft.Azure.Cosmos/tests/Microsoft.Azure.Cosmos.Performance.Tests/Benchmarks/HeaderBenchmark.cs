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
        private readonly INameValueCollection storeResponse = new StoreResponseNameValueCollection()
        {
            {HttpConstants.HttpHeaders.Authorization, "testvalue" }
        };

        private readonly INameValueCollection requestHeaders = new OptimizedRequestHeaders()
        {
            {HttpConstants.HttpHeaders.Authorization, "testvalue" }
        };

        public HeaderBenchmark()
        {
        }

        [Benchmark]
        public void StoreResponseHeadersCreate()
        {
            new StoreResponseNameValueCollection();
        }

        [Benchmark]
        public void OptimizedHeadersCreate()
        {
            new OptimizedRequestHeaders();
        }

        [Benchmark]
        public void StoreResponseHeadersGet()
        {
            string value = this.storeResponse.Get(HttpConstants.HttpHeaders.Authorization);
            if(value == null)
            {
                throw new Exception();
            }
        }

        [Benchmark]
        public void OptimizedHeadersGet()
        {
            string value = this.requestHeaders.Get(HttpConstants.HttpHeaders.Authorization);
            if (value == null)
            {
                throw new Exception();
            }
        }

        [Benchmark]
        public void StoreResponseHeadersSet()
        {
            this.storeResponse.Set(HttpConstants.HttpHeaders.Authorization, "SomeRandomValue");
        }

        [Benchmark]
        public void OptimizedHeadersSet()
        {
            this.requestHeaders.Set(HttpConstants.HttpHeaders.Authorization, "SomeRandomValue");
        }
    }
}