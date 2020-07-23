using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    [MemoryDiagnoser]
    public class AuthorizationBenchmark
    {
        private readonly DictionaryNameValueCollection headers = new DictionaryNameValueCollection();
        private readonly IComputeHash hashFunction;

        public AuthorizationBenchmark()
        {
            this.headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
            this.hashFunction = new StringHMACSHA256Hash("c29tZSByYW5kb20gc3RyaW5nIHRvIGVuY29kZQ==");
        }

        [Benchmark]
        public void GenerateAuthorizationToken()
        {
            AuthorizationHelper.GenerateKeyAuthorizationSignature(
                "GET",
                "dbs/database/colls/collection/docs/document",
                "docs",
                this.headers,
                this.hashFunction,
                out MemoryStream _);
        }

    }
}
