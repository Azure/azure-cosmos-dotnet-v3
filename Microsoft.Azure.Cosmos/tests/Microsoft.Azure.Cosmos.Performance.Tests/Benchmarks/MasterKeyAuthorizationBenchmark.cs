﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Globalization;
    using System.IO;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    [Config(typeof(SdkBenchmarkConfiguration))]
    public class MasterKeyAuthorizationBenchmark
    {
        private IComputeHash authKeyHashFunction;
        private INameValueCollection testHeaders;

        public MasterKeyAuthorizationBenchmark()
        {
            this.authKeyHashFunction = new StringHMACSHA256Hash(MockDocumentClient.GenerateRandomKey());
            Headers headers = new Headers();
            headers[HttpConstants.HttpHeaders.XDate] = Rfc1123DateTimeCache.UtcNow();

            this.testHeaders = headers.CosmosMessageHeaders.INameValueCollection;
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public void CreateSignatureGeneration()
        {
            this.TestSignature("POST", "dbs/testdb/colls/testcollection/dbs", "dbs");
        }

        [Benchmark]
        [BenchmarkCategory("GateBenchmark")]
        public void ReadSignatureGeneration()
        {
            this.TestSignature("GET", "dbs/testdb/colls/testcollection/dbs/item1", "dbs");
        }

        private void TestSignature(string verb,
            string resourceId,
            string resourceType)
        {
            AuthorizationHelper.ArrayOwner payload;
            AuthorizationHelper.GenerateKeyAuthorizationSignature(verb, resourceId, resourceType, this.testHeaders, this.authKeyHashFunction, out payload);
            payload.Dispose();
        }
    }
}
