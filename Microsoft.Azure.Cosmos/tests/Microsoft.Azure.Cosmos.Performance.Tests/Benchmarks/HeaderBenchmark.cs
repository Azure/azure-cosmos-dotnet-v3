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
        private static readonly string AuthValue = Guid.NewGuid().ToString();
        private static readonly string Date = Guid.NewGuid().ToString();
        private static readonly string Retry = Guid.NewGuid().ToString();
        private static readonly string Partitionkey = Guid.NewGuid().ToString();
        private static readonly string Remaining = Guid.NewGuid().ToString();
        private static readonly string Transport = Guid.NewGuid().ToString();
        private static readonly string Rid = Guid.NewGuid().ToString();

        private static readonly StoreRequestHeaders StoreRequestHeaders = new StoreRequestHeaders
        {
            { HttpConstants.HttpHeaders.Authorization, AuthValue },
            { HttpConstants.HttpHeaders.XDate, Date },
            { HttpConstants.HttpHeaders.ClientRetryAttemptCount, Retry },
            { HttpConstants.HttpHeaders.PartitionKey, Partitionkey },
            { HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, Remaining },
            { HttpConstants.HttpHeaders.TransportRequestID, Transport },
            { WFConstants.BackendHeaders.CollectionRid, Rid }
        };

        private static readonly DictionaryNameValueCollection DictionaryHeaders = new DictionaryNameValueCollection()
        {
            { HttpConstants.HttpHeaders.Authorization, AuthValue },
            { HttpConstants.HttpHeaders.XDate, Date },
            { HttpConstants.HttpHeaders.ClientRetryAttemptCount, Retry },
            { HttpConstants.HttpHeaders.PartitionKey, Partitionkey },
            { HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, Remaining },
            { HttpConstants.HttpHeaders.TransportRequestID, Transport },
            { WFConstants.BackendHeaders.CollectionRid, Rid }
        };

        public HeaderBenchmark()
        {
            
        }

        [Benchmark]
        public void StoreRequestHeadersCreate()
        {
            new StoreRequestHeaders();
        }

        [Benchmark]
        public void DictionaryNameValueCollectionCreate()
        {
            new DictionaryNameValueCollection();
        }

        [Benchmark]
        public void StoreRequestHeadersPointRead()
        {
            _ = new StoreRequestHeaders
            {
                { HttpConstants.HttpHeaders.Authorization, AuthValue },
                { HttpConstants.HttpHeaders.XDate, Date },
                { HttpConstants.HttpHeaders.ClientRetryAttemptCount, Retry },
                { HttpConstants.HttpHeaders.PartitionKey, Partitionkey },
                { HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, Remaining },
                { HttpConstants.HttpHeaders.TransportRequestID, Transport },
                { WFConstants.BackendHeaders.CollectionRid, Rid }
            };
        }

        [Benchmark]
        public void DictionaryPointRead()
        {
            _ = new DictionaryNameValueCollection()
            {
                { HttpConstants.HttpHeaders.Authorization, AuthValue },
                { HttpConstants.HttpHeaders.XDate, Date },
                { HttpConstants.HttpHeaders.ClientRetryAttemptCount, Retry },
                { HttpConstants.HttpHeaders.PartitionKey, Partitionkey },
                { HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, Remaining },
                { HttpConstants.HttpHeaders.TransportRequestID, Transport },
                { WFConstants.BackendHeaders.CollectionRid, Rid }
            };
        }

        [Benchmark]
        public void StoreRequestHeadersKeys()
        {
            foreach (string key in StoreRequestHeaders.Keys())
            {
                StoreRequestHeaders.Get(key);
            }
        }

        [Benchmark]
        public void DictionaryKeys()
        {
            foreach(string key in DictionaryHeaders.Keys)
            {
                DictionaryHeaders.Get(key);
            }
        }
    }
}