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
            this.authKeyHashFunction = new StringHMACSHA256Hash(MasterKeyAuthorizationBenchmark.GenerateRandomKey());
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
            AuthorizationHelper.ArrayOwner payload;
            AuthorizationHelper.GenerateKeyAuthorizationSignature(verb, resourceId, resourceType, this.testHeaders, this.authKeyHashFunction, out payload);
            payload.Dispose();
        }

        public static string GenerateRandomKey()
        {
            int keyLength = 64;
            byte[] randomEntries = new byte[keyLength];

            Random r = new Random((int)DateTime.Now.Ticks);
            r.NextBytes(randomEntries);

            return Convert.ToBase64String(randomEntries);
        }
    }
}
