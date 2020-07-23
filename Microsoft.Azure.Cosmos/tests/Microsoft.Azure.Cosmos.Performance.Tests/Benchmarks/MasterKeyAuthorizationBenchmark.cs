//------------------------------------------------------------
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
            this.authKeyHashFunction = new StringHMACSHA256Hash("C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
            Headers headers = new Headers();
            headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

            this.testHeaders = headers.CosmosMessageHeaders;
        }

        [Benchmark]
        public void CreateSignatureGeneration()
        {
            this.TestSignature("POST", "dbs/testdb/colls/testcollection/dbs", "dbs");
        }

        [Benchmark]
        public void ReadSignatureGeneration()
        {
            this.TestSignature("GET", "dbs/testdb/colls/testcollection/dbs/item1", "dbs");
        }

        private void TestSignature(string verb,
            string resourceId,
            string resourceType)
        {
            MemoryStream payload;
            AuthorizationHelper.GenerateKeyAuthorizationSignature(verb, resourceId, resourceType, this.testHeaders, this.authKeyHashFunction, out payload);

#pragma warning disable CS0642 // Possible mistaken empty statement
            using (payload) ;
#pragma warning restore CS0642 // Possible mistaken empty statement
        }
    }
}
