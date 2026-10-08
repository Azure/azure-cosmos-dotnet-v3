//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Tracks thin client proxy connectivity per regional endpoint. For each endpoint in a probe cycle it issues
    /// a <c>POST /connectivity-probe</c> over HTTP/2; only an HTTP 200 marks the endpoint healthy. A healthy
    /// endpoint is cached for the lifetime of the client and never re-probed. Routing consults
    /// <see cref="IsEndpointHealthy"/> to use the proxy for healthy regions and Gateway V1 otherwise.
    /// </summary>
    internal sealed class EndpointProbeClient : IDisposable
    {
        private const string ConnectivityProbeOperationType = "ConnectivityProbe";
        private const string ProbePath = "/connectivity-probe";

        // HttpVersionPolicy.RequestVersionExact, set via reflection so the SDK keeps compiling on frameworks
        // (e.g. netstandard2.0) lacking HttpRequestMessage.VersionPolicy. Exact policy fails instead of
        // downgrading to HTTP/1.1 when client and proxy cannot negotiate HTTP/2, so the SDK falls back to Gateway V1.
        private const int RequestVersionExact = 2;

        // Probes must fail fast; a short fixed budget avoids tying up a cycle longer than a refresh interval.
        private static readonly TimeSpan PerProbeTimeout = TimeSpan.FromSeconds(5);

        private static readonly PropertyInfo VersionPolicyProperty = typeof(HttpRequestMessage).GetProperty("VersionPolicy");

        private readonly CosmosHttpClient httpClient;

        // Endpoints that have returned HTTP 200 at least once. Membership is permanent for the client lifetime.
        private readonly ConcurrentDictionary<Uri, byte> healthyEndpoints = new ConcurrentDictionary<Uri, byte>();

        private int closed = 0;
        private int cycleInProgress = 0;

        public EndpointProbeClient(CosmosHttpClient httpClient)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(
                nameof(httpClient),
                "EndpointProbeClient requires a non-null thin client CosmosHttpClient (HTTP/2).");
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="regionalEndpoint"/> has been successfully probed (HTTP 200)
        /// at least once. Un-probed and failed endpoints return <c>false</c>.
        /// </summary>
        public bool IsEndpointHealthy(Uri regionalEndpoint)
        {
            return regionalEndpoint != null && this.healthyEndpoints.ContainsKey(regionalEndpoint);
        }

        /// <summary>
        /// Runs one probe cycle against the supplied endpoints. Only endpoints not already cached healthy are
        /// probed; any returning HTTP 200 are added to the permanent success cache. Never throws so probe
        /// failures do not fail topology refresh.
        /// </summary>
        public async Task RunProbeCycleAsync(IReadOnlyCollection<Uri> regionalEndpoints, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref this.closed) == 1)
            {
                return;
            }

            HashSet<Uri> endpointsToProbe = regionalEndpoints == null
                ? new HashSet<Uri>()
                : new HashSet<Uri>(regionalEndpoints.Where(e => e != null && !this.healthyEndpoints.ContainsKey(e)));

            if (endpointsToProbe.Count == 0)
            {
                // Every supplied endpoint is already cached healthy (or none supplied).
                return;
            }

            // Single-flight: skip if a cycle is already running so overlapping refreshes do not double-probe.
            if (Interlocked.CompareExchange(ref this.cycleInProgress, 1, 0) != 0)
            {
                DefaultTrace.TraceVerbose("Thin client probe cycle already in progress; skipping overlapping trigger.");
                return;
            }

            try
            {
                (Uri endpoint, bool ok)[] results = await Task.WhenAll(
                    endpointsToProbe.Select(endpoint => this.ProbeEndpointAsync(endpoint, cancellationToken)));

                // Drop results if the client was closed mid-cycle so we don't mutate the cache on a dead client.
                if (Volatile.Read(ref this.closed) == 1)
                {
                    return;
                }

                foreach ((Uri endpoint, bool ok) in results)
                {
                    if (ok)
                    {
                        if (this.healthyEndpoints.TryAdd(endpoint, 0))
                        {
                            DefaultTrace.TraceInformation(
                                "Thin client probe GREEN for {0}; routing to the thin client proxy for that region.",
                                endpoint);
                        }
                    }
                    else
                    {
                        DefaultTrace.TraceVerbose(
                            "Thin client probe RED for {0}; staying on Gateway V1 for that region until a later probe succeeds.",
                            endpoint);
                    }
                }
            }
            catch (Exception ex)
            {
                // Probe issues must never fail topology refresh.
                DefaultTrace.TraceWarning(
                    "Thin client probe cycle threw; treated as no successful probes this cycle. Exception: {0}",
                    ex.Message);
            }
            finally
            {
                Volatile.Write(ref this.cycleInProgress, 0);
            }
        }

        /// <summary>
        /// Marks the probe client as closed so subsequent <see cref="RunProbeCycleAsync"/> calls short-circuit.
        /// The shared thin client <see cref="CosmosHttpClient"/> is owned by the client and is not disposed here.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this.closed, 1, 0) == 0)
            {
                DefaultTrace.TraceVerbose("EndpointProbeClient closed; no further thin client probes will be issued.");
            }
        }

        private async Task<(Uri endpoint, bool ok)> ProbeEndpointAsync(Uri regionalEndpoint, CancellationToken cancellationToken)
        {
            Uri probeUri;
            try
            {
                probeUri = EndpointProbeClient.BuildProbeUri(regionalEndpoint);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Failed to build probe URI for {0}: {1}", regionalEndpoint, ex.Message);
                return (regionalEndpoint, false);
            }

            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref this.closed) == 1)
            {
                return (regionalEndpoint, false);
            }

            // A single probe per cycle; no in-cycle retries. A red endpoint stays on Gateway V1 until a later
            // cycle (after the next topology refresh) re-probes and succeeds.
            using CancellationTokenSource perProbeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            perProbeCts.CancelAfter(EndpointProbeClient.PerProbeTimeout);

            try
            {
                using HttpResponseMessage responseMessage = await this.httpClient.SendHttpAsync(
                    createRequestMessageAsync: () => new ValueTask<HttpRequestMessage>(
                        EndpointProbeClient.CreateProbeRequest(probeUri)),
                    resourceType: ResourceType.Document,
                    timeoutPolicy: HttpTimeoutPolicyNoRetry.Instance,
                    clientSideRequestStatistics: null,
                    cancellationToken: perProbeCts.Token);

                bool ok = responseMessage.StatusCode == HttpStatusCode.OK;
                if (!ok)
                {
                    DefaultTrace.TraceVerbose(
                        "Thin client probe to {0} returned status {1}",
                        regionalEndpoint, (int)responseMessage.StatusCode);
                }

                return (regionalEndpoint, ok);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceVerbose(
                    "Thin client probe to {0} failed: {1}",
                    regionalEndpoint, ex.Message);
                return (regionalEndpoint, false);
            }
        }

        private static HttpRequestMessage CreateProbeRequest(Uri probeUri)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, probeUri)
            {
                Version = new Version(2, 0),
                Content = new ByteArrayContent(Array.Empty<byte>()),
            };

            // Require exact HTTP/2: fail rather than downgrade so an HTTP/2 mismatch falls back to Gateway V1.
            EndpointProbeClient.VersionPolicyProperty?.SetValue(
                requestMessage,
                Enum.ToObject(EndpointProbeClient.VersionPolicyProperty.PropertyType, EndpointProbeClient.RequestVersionExact));

            // Mirror thin client traffic so the proxy treats this like a real data-plane request.
            requestMessage.Headers.TryAddWithoutValidation(
                ThinClientConstants.ProxyOperationType,
                EndpointProbeClient.ConnectivityProbeOperationType);

            return requestMessage;
        }

        private static Uri BuildProbeUri(Uri regionalEndpoint)
        {
            return new UriBuilder(regionalEndpoint.Scheme, regionalEndpoint.Host, regionalEndpoint.Port, EndpointProbeClient.ProbePath).Uri;
        }
    }
}
