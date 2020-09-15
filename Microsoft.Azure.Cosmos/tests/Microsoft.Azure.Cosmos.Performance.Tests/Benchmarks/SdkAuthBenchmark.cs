// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    [MemoryDiagnoser]
    public class SdkAuthBenchmark
    {
        private readonly IComputeHash authKeyHashFunction = new StringHMACSHA256Hash("C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        private readonly INameValueCollection headers;

        public SdkAuthBenchmark()
        {
            this.headers = new StoreResponseNameValueCollection();
            this.headers.Add(HttpConstants.HttpHeaders.HttpDate, DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture));
        }

        [Benchmark]
        public void AuthEncodeOptimization()
        {
            string auth = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                "get",
                "dbs/72c6b99c-05c9-48bc-bb48-ce908e0d0dbb/colls/69058ef4-aba0-438f-b4ec-afde30a494e0/docs/9c114841-47ab-46ce-8b4c-40f7f2438c95",
                "Document",
                this.headers,
                this.authKeyHashFunction,
                out AuthorizationHelper.ArrayOwner arrayOwner);
            arrayOwner.Dispose();
        }

        [Benchmark]
        public void AuthEncodeOptimizationStack()
        {
            string auth = AuthorizationHelper.GenerateKeyAuthorizationSignatureStack(
                "get",
                "dbs/72c6b99c-05c9-48bc-bb48-ce908e0d0dbb/colls/69058ef4-aba0-438f-b4ec-afde30a494e0/docs/9c114841-47ab-46ce-8b4c-40f7f2438c95",
                "Document",
                this.headers,
                this.authKeyHashFunction,
                out AuthorizationHelper.ArrayOwner arrayOwner);
            arrayOwner.Dispose();
        }

        [Benchmark]
        public void AuthOriginal()
        {
            string auth = AuthorizationHelper.GenerateKeyAuthorizationSignatureOld(
                "get",
                "dbs/72c6b99c-05c9-48bc-bb48-ce908e0d0dbb/colls/69058ef4-aba0-438f-b4ec-afde30a494e0/docs/9c114841-47ab-46ce-8b4c-40f7f2438c95",
                "Document",
                this.headers,
                this.authKeyHashFunction,
                out AuthorizationHelper.ArrayOwner arrayOwner);
            arrayOwner.Dispose();
        }
    }

}