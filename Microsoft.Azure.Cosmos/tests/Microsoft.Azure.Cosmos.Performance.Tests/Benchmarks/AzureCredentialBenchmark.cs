namespace Microsoft.Azure.Cosmos.Benchmarks
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using global::Azure;
    using Microsoft.Azure.Cosmos.Authorization;

    public class AzureCredentialBenchmark
    {
        private readonly AzureKeyCredentialAuthorizationTokenProvider akcp;
        private readonly AuthorizationTokenProviderMasterKey atpMasterKey;

        public AzureCredentialBenchmark()
        {
            string authKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));

            AzureKeyCredential keyCredential = new AzureKeyCredential(authKey);
            this.akcp = new AzureKeyCredentialAuthorizationTokenProvider(keyCredential);
            this.atpMasterKey = new AuthorizationTokenProviderMasterKey(authKey);
        }

        [Benchmark]
        public async Task AzureCredential()
        {
            Documents.Collections.NameValueCollectionWrapper headers = new Documents.Collections.NameValueCollectionWrapper();
            await this.akcp.GetUserAuthorizationTokenAsync(String.Empty, "docs", "GET", headers, Documents.AuthorizationTokenType.PrimaryMasterKey, Cosmos.Tracing.NoOpTrace.Singleton);
        }

        [Benchmark]
        public async Task MasterKeyCredential()
        {
            Documents.Collections.NameValueCollectionWrapper headers = new Documents.Collections.NameValueCollectionWrapper();
            await this.akcp.GetUserAuthorizationTokenAsync(String.Empty, "docs", "GET", headers, Documents.AuthorizationTokenType.PrimaryMasterKey, Cosmos.Tracing.NoOpTrace.Singleton);
        }
    }
}
