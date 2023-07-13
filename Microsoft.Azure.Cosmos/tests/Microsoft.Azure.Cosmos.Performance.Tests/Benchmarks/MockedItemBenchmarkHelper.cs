// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Resource.Settings;
    using System.Net;
    using System.Text;

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    public class MockedItemBenchmarkHelper
    {
        public static readonly string ExistingItemId = "lets-benchmark";
        public static readonly string NonExistingItemId = "cant-see-me";

        public static readonly PartitionKey ExistingPartitionId = new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId);
        private readonly bool IncludeDiagnosticsToString;

        internal ToDoActivity TestItem { get; }
        internal CosmosClient TestClient { get; }
        internal Container TestContainer { get; }
        
        internal byte[] TestItemBytes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockedItemBenchmark"/> class.
        /// </summary>
        public MockedItemBenchmarkHelper(
            bool useCustomSerializer = false,
            bool includeDiagnosticsToString = false,
            bool useBulk = false,
            bool? isClientTelemetryEnabled = null)
        {
            if (isClientTelemetryEnabled.HasValue && isClientTelemetryEnabled.Value)
            {
                string EndpointUrl = "http://dummy.test.com/";
                HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
                {
                    RequestCallBack = (request, cancellation) =>
                    {
                        if (request.RequestUri.AbsoluteUri.Equals(EndpointUrl))
                        {
                            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                            string jsonObject = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                            return Task.FromResult(result);
                        }
                        else if (request.RequestUri.AbsoluteUri.Contains(Documents.Paths.ClientConfigPathSegment))
                        {
                            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                            AccountClientConfigProperties clientConfigProperties = new AccountClientConfigProperties
                            {
                                ClientTelemetryConfiguration = new ClientTelemetryConfiguration
                                {
                                    IsEnabled = true,
                                    Endpoint = EndpointUrl
                                }
                            };

                            string payload = JsonConvert.SerializeObject(clientConfigProperties);
                            result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                            return Task.FromResult(result);
                        }
                        return null;
                    }
                };

                this.TestClient = MockDocumentClient.CreateMockCosmosClient(useCustomSerializer, (builder) => builder
                                                                                                                .WithBulkExecution(useBulk)
                                                                                                                .WithHttpClientFactory(() => new HttpClient(httpHandler)));
            }
            else
            {
                this.TestClient = MockDocumentClient.CreateMockCosmosClient(useCustomSerializer, (builder) => builder.WithBulkExecution(useBulk));
            }
           
            
            this.TestContainer = this.TestClient.GetDatabase("myDB").GetContainer("myColl");
            this.IncludeDiagnosticsToString = includeDiagnosticsToString;

            using (FileStream tmp = File.OpenRead("samplepayload.json"))
            using (MemoryStream ms = new MemoryStream())
            {
                tmp.CopyTo(ms);
                this.TestItemBytes = ms.ToArray();
            }

            using (MemoryStream ms = new MemoryStream(this.TestItemBytes))
            {
                string payloadContent = File.ReadAllText("samplepayload.json");
                this.TestItem = JsonConvert.DeserializeObject<ToDoActivity>(payloadContent);
            }
        }

        public void IncludeDiagnosticToStringHelper(
            CosmosDiagnostics cosmosDiagnostics)
        {
            if (!this.IncludeDiagnosticsToString)
            {
                return;
            }

            string diagnostics = cosmosDiagnostics.ToString();
            if (string.IsNullOrEmpty(diagnostics))
            {
                throw new Exception();
            }
        }

        public MemoryStream GetItemPayloadAsStream()
        {
            return new MemoryStream(
                this.TestItemBytes,
                index: 0,
                count: this.TestItemBytes.Length,
                writable: false,
                publiclyVisible: true);
        }

        private class HttpClientHandlerHelper : DelegatingHandler
        {
            public HttpClientHandlerHelper() : base(new HttpClientHandler())
            {
            }

            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> RequestCallBack { get; set; }

            public Func<HttpResponseMessage, Task<HttpResponseMessage>> ResponseIntercepter { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage httpResponse = null;
                if (this.RequestCallBack != null)
                {
                    Task<HttpResponseMessage> response = this.RequestCallBack(request, cancellationToken);
                    if (response != null)
                    {
                        httpResponse = await response;
                        if (httpResponse != null)
                        {
                            if (this.ResponseIntercepter != null)
                            {
                                httpResponse = await this.ResponseIntercepter(httpResponse);
                            }
                            return httpResponse;
                        }
                    }
                }

                httpResponse = await base.SendAsync(request, cancellationToken);
                if (this.ResponseIntercepter != null)
                {
                    httpResponse = await this.ResponseIntercepter(httpResponse);
                }

                return httpResponse;
            }
        }
    }
}
