//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EndpointProbeClientTests
    {
        private static readonly Uri Region1 = new Uri("https://region1.thinclient.cosmos.azure.com:10250/");
        private static readonly Uri Region2 = new Uri("https://region2.thinclient.cosmos.azure.com:10250/");

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_AllEndpoints200_CachesEachEndpointHealthy()
        {
            using EndpointProbeClient probeClient = BuildProbeClient(_ => HttpStatusCode.OK);

            Assert.IsFalse(probeClient.IsEndpointHealthy(Region1), "Endpoints are pessimistic (un-routable) until first green probe.");
            Assert.IsFalse(probeClient.IsEndpointHealthy(Region2));

            await probeClient.RunProbeCycleAsync(new[] { Region1, Region2 }, CancellationToken.None);

            Assert.IsTrue(probeClient.IsEndpointHealthy(Region1));
            Assert.IsTrue(probeClient.IsEndpointHealthy(Region2));
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_PerEndpointHealth_OnlyGreenEndpointsCached()
        {
            // Region1 green, Region2 red -> per-endpoint cache: only Region1 becomes routable.
            using EndpointProbeClient probeClient = BuildProbeClient(
                request => request.RequestUri.Host == Region2.Host ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);

            await probeClient.RunProbeCycleAsync(new[] { Region1, Region2 }, CancellationToken.None);

            Assert.IsTrue(probeClient.IsEndpointHealthy(Region1), "Green endpoint must be cached healthy.");
            Assert.IsFalse(probeClient.IsEndpointHealthy(Region2), "Red endpoint must stay un-cached (Gateway V1 fallback for that region).");
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_RedThenGreen_EndpointBecomesHealthyOnLaterCycle()
        {
            // A red endpoint is not cached; a later cycle that returns green caches it.
            HttpStatusCode status = HttpStatusCode.ServiceUnavailable;
            using EndpointProbeClient probeClient = BuildProbeClient(_ => status);

            await probeClient.RunProbeCycleAsync(new[] { Region1 }, CancellationToken.None);
            Assert.IsFalse(probeClient.IsEndpointHealthy(Region1), "Red probe leaves the endpoint un-cached.");

            status = HttpStatusCode.OK;
            await probeClient.RunProbeCycleAsync(new[] { Region1 }, CancellationToken.None);
            Assert.IsTrue(probeClient.IsEndpointHealthy(Region1), "A later green probe caches the endpoint healthy.");
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_CachedEndpoint_IsNotReProbed()
        {
            // Once an endpoint is cached green it is never re-probed (no overhead for known-good regions).
            int requestCount = 0;
            using EndpointProbeClient probeClient = BuildProbeClient(_ =>
            {
                Interlocked.Increment(ref requestCount);
                return HttpStatusCode.OK;
            });

            await probeClient.RunProbeCycleAsync(new[] { Region1 }, CancellationToken.None);
            Assert.AreEqual(1, requestCount, "First cycle probes the un-cached endpoint exactly once.");
            Assert.IsTrue(probeClient.IsEndpointHealthy(Region1));

            await probeClient.RunProbeCycleAsync(new[] { Region1 }, CancellationToken.None);
            Assert.AreEqual(1, requestCount, "A cached-green endpoint must not be re-probed on later cycles.");
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_RedEndpoint_RetriesUpToMaxRetriesThenStaysUnhealthy()
        {
            // A persistently red endpoint is retried 1 + maxRetries times in a single cycle, then left un-cached.
            int requestCount = 0;
            const int maxRetries = 2;
            using EndpointProbeClient probeClient = BuildProbeClient(
                _ =>
                {
                    Interlocked.Increment(ref requestCount);
                    return HttpStatusCode.ServiceUnavailable;
                },
                maxRetries: maxRetries);

            await probeClient.RunProbeCycleAsync(new[] { Region1 }, CancellationToken.None);

            Assert.AreEqual(maxRetries + 1, requestCount, "A red endpoint must be attempted 1 + maxRetries times.");
            Assert.IsFalse(probeClient.IsEndpointHealthy(Region1), "An endpoint that fails every attempt stays un-cached.");
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_RedThenGreenWithinCycle_StopsRetryingOnFirstGreen()
        {
            // With retries enabled, a transient red followed by a green within the same cycle caches the
            // endpoint and stops issuing further attempts.
            int requestCount = 0;
            using EndpointProbeClient probeClient = BuildProbeClient(
                _ => Interlocked.Increment(ref requestCount) == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
                maxRetries: 3);

            await probeClient.RunProbeCycleAsync(new[] { Region1 }, CancellationToken.None);

            Assert.AreEqual(2, requestCount, "First attempt red, second attempt green -> exactly two attempts, no more.");
            Assert.IsTrue(probeClient.IsEndpointHealthy(Region1), "A green attempt within the retry budget caches the endpoint.");
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_NullOrEmptyEndpoints_IssuesNoProbes()
        {
            int requestCount = 0;
            using EndpointProbeClient probeClient = BuildProbeClient(_ =>
            {
                Interlocked.Increment(ref requestCount);
                return HttpStatusCode.OK;
            });

            await probeClient.RunProbeCycleAsync(null, CancellationToken.None);
            await probeClient.RunProbeCycleAsync(Array.Empty<Uri>(), CancellationToken.None);

            Assert.AreEqual(0, requestCount);
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_AfterDispose_IsNoOp()
        {
            int requestCount = 0;
            EndpointProbeClient probeClient = BuildProbeClient(_ =>
            {
                Interlocked.Increment(ref requestCount);
                return HttpStatusCode.OK;
            });

            probeClient.Dispose();

            await probeClient.RunProbeCycleAsync(new[] { Region1 }, CancellationToken.None);

            Assert.AreEqual(0, requestCount);
            Assert.IsFalse(probeClient.IsEndpointHealthy(Region1));
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task RunProbeCycle_IssuesHttp2PostToConnectivityProbePath()
        {
            List<HttpRequestMessage> captured = new List<HttpRequestMessage>();
            using EndpointProbeClient probeClient = BuildProbeClient(request =>
            {
                lock (captured)
                {
                    captured.Add(request);
                }

                return HttpStatusCode.OK;
            });

            await probeClient.RunProbeCycleAsync(new[] { Region1 }, CancellationToken.None);

            Assert.AreEqual(1, captured.Count);
            HttpRequestMessage probeRequest = captured[0];
            Assert.AreEqual(HttpMethod.Post, probeRequest.Method);
            Assert.AreEqual("/connectivity-probe", probeRequest.RequestUri.AbsolutePath);
            Assert.AreEqual(Region1.Host, probeRequest.RequestUri.Host);
            Assert.AreEqual(Region1.Port, probeRequest.RequestUri.Port);
            Assert.AreEqual(new Version(2, 0), probeRequest.Version, "Probe must be issued over HTTP/2.");
        }

        private static EndpointProbeClient BuildProbeClient(
            Func<HttpRequestMessage, HttpStatusCode> statusSelector,
            int maxRetries = 0)
        {
            return new EndpointProbeClient(BuildHttpClient(statusSelector), maxRetries);
        }

        private static CosmosHttpClient BuildHttpClient(Func<HttpRequestMessage, HttpStatusCode> statusSelector)
        {
            MockProbeMessageHandler handler = new MockProbeMessageHandler(statusSelector);
            return MockCosmosUtil.CreateCosmosHttpClient(() => new HttpClient(handler));
        }

        private sealed class MockProbeMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpStatusCode> statusSelector;

            public MockProbeMessageHandler(Func<HttpRequestMessage, HttpStatusCode> statusSelector)
            {
                this.statusSelector = statusSelector;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpStatusCode status = this.statusSelector(request);
                HttpResponseMessage response = new HttpResponseMessage(status)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>()),
                    Version = request.Version,
                };

                return Task.FromResult(response);
            }
        }
    }
}
