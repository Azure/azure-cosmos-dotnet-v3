//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.Metrics;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using static Microsoft.Azure.Cosmos.Routing.MetadataHedgingStrategy;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class MetadataHedgingTelemetryTests
    {
        private static readonly Uri PrimaryEndpoint = new Uri("https://acct-eastus.documents.azure.com/");
        private static readonly Uri HedgeEndpoint = new Uri("https://acct-westus.documents.azure.com/");
        private const string PrimaryRegion = "East US";
        private const string HedgeRegion = "West US";

        [TestMethod]
        [Owner("dkunda")]
        public async Task FiredHedge_EmitsFires_HedgeWins_HedgeFiredElapsed_DiagnosticsPopulated()
        {
            using MetricCollector metrics = new MetricCollector();
            using EventCollector events = new EventCollector();

            MetadataHedgingStrategy strategy = BuildStrategy(threshold: TimeSpan.FromMilliseconds(50));
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Collection, AuthorizationTokenType.PrimaryMasterKey);
            MetadataHedgingContext ctx = NewColdStartContext();

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                request,
                sendToEndpoint: async (req, uri, ct) =>
                {
                    if (uri.Equals(PrimaryEndpoint))
                    {
                        await Task.Delay(500, ct);
                    }

                    return NewOkResponse();
                },
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsTrue(result.HedgeFired);
            Assert.AreEqual(HedgeEndpoint, result.WinningEndpoint);

            // Metrics: fires + hedge_wins + hedge_fired_elapsed.
            Assert.AreEqual(
                1,
                metrics.GetCount(CosmosDbClientMetrics.MetadataHedgingMetrics.Name.Fires),
                "fires counter not emitted.");
            Assert.AreEqual(
                1,
                metrics.GetCount(CosmosDbClientMetrics.MetadataHedgingMetrics.Name.HedgeWins),
                "hedge_wins counter not emitted.");
            Assert.IsTrue(
                metrics.GetSum(CosmosDbClientMetrics.MetadataHedgingMetrics.Name.HedgeFiredElapsed) > 0,
                "hedge_fired_elapsed histogram empty.");

            // Events: HedgeFired (id 4) and HedgeWon (id 5) recorded.
            Assert.IsTrue(events.Contains(eventId: 4), "OnMetadataHedgeFired event not emitted.");
            Assert.IsTrue(events.Contains(eventId: 5), "OnMetadataHedgeWon event not emitted.");

            // Diagnostics block populated.
            MetadataHedgeDiagnostics diag = result.Diagnostics;
            Assert.IsTrue(diag.Eligible);
            Assert.AreEqual(ResourceType.Collection.ToString(), diag.ResourceType);
            Assert.AreEqual(PrimaryRegion, diag.PrimaryRegion);
            Assert.AreEqual(HedgeRegion, diag.HedgeRegion);
            Assert.AreEqual(HedgeRegion, diag.WinningRegion);
            Assert.IsTrue(diag.ThresholdMs > 0);
            Assert.IsTrue(diag.HedgeFiredElapsedMs.HasValue && diag.HedgeFiredElapsedMs.Value >= diag.ThresholdMs);
            Assert.AreEqual(2, diag.TotalAttempts);

            strategy.Dispose();
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task SkippedHedge_NotColdStart_EmitsSkippedEvent_DiagnosticsPopulated()
        {
            using MetricCollector metrics = new MetricCollector();
            using EventCollector events = new EventCollector();

            MetadataHedgingStrategy strategy = BuildStrategy();
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read, ResourceType.Collection, AuthorizationTokenType.PrimaryMasterKey);
            MetadataHedgingContext ctx = NewColdStartContext();
            ctx.IsColdStart = false;

            MetadataHedgingResult result = await strategy.ExecuteAsync(
                request,
                sendToEndpoint: (req, uri, ct) => Task.FromResult(NewOkResponse()),
                ctx,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsFalse(result.HedgeFired);
            Assert.AreEqual(PrimaryEndpoint, result.WinningEndpoint);

            Assert.AreEqual(0, metrics.GetCount(CosmosDbClientMetrics.MetadataHedgingMetrics.Name.Fires));
            Assert.AreEqual(0, metrics.GetCount(CosmosDbClientMetrics.MetadataHedgingMetrics.Name.HedgeWins));
            Assert.IsTrue(events.Contains(eventId: 7), "OnMetadataHedgeSkipped event not emitted.");

            MetadataHedgeDiagnostics diag = result.Diagnostics;
            Assert.IsFalse(diag.Eligible);
            Assert.AreEqual(MetadataHedgeSkipReason.NotColdStart, diag.SkipReason);
            Assert.AreEqual(ResourceType.Collection.ToString(), diag.ResourceType);
            Assert.AreEqual(PrimaryRegion, diag.PrimaryRegion);
            Assert.AreEqual(PrimaryRegion, diag.WinningRegion);
            Assert.AreEqual(1, diag.TotalAttempts);
            Assert.IsFalse(diag.HedgeFiredElapsedMs.HasValue);

            strategy.Dispose();
        }

        [TestMethod]
        [Owner("dkunda")]
        public async Task BudgetExhausted_EmitsBudgetExhaustedCounterAndSkippedEvent()
        {
            using MetricCollector metrics = new MetricCollector();
            using EventCollector events = new EventCollector();

            // Budget = 1 → first hedge consumes it; second eligible request hits BudgetExhausted
            // while the first's loser (primary) is still being awaited by BackgroundCleanupAsync.
            MetadataHedgingStrategy strategy = BuildStrategy(
                threshold: TimeSpan.FromMilliseconds(50),
                perClientConcurrencyBudget: 1);

            // Gate keeps the primary (loser) in flight, holding the budget.
            ManualResetEventSlim primaryRelease = new ManualResetEventSlim(false);
            try
            {
                MetadataHedgingResult firstResult = await strategy.ExecuteAsync(
                    DocumentServiceRequest.Create(OperationType.Read, ResourceType.Collection, AuthorizationTokenType.PrimaryMasterKey),
                    sendToEndpoint: async (req, uri, ct) =>
                    {
                        if (uri.Equals(PrimaryEndpoint))
                        {
                            // Ignore the loser CT so the primary continues to run and the
                            // budget stays held by BackgroundCleanupAsync.
                            await Task.Run(() => primaryRelease.Wait(TimeSpan.FromSeconds(10)));
                            return NewOkResponse();
                        }

                        return NewOkResponse();
                    },
                    NewColdStartContext(),
                    NoOpTrace.Singleton,
                    CancellationToken.None);

                Assert.IsTrue(firstResult.HedgeFired);

                // Second concurrent eligible request: budget == 0 → BudgetExhausted.
                MetadataHedgingResult secondResult = await strategy.ExecuteAsync(
                    DocumentServiceRequest.Create(OperationType.Read, ResourceType.Collection, AuthorizationTokenType.PrimaryMasterKey),
                    sendToEndpoint: (req, uri, ct) => Task.FromResult(NewOkResponse()),
                    NewColdStartContext(),
                    NoOpTrace.Singleton,
                    CancellationToken.None);

                Assert.AreEqual(MetadataHedgeSkipReason.BudgetExhausted, secondResult.Diagnostics.SkipReason);
                Assert.AreEqual(
                    1,
                    metrics.GetCount(CosmosDbClientMetrics.MetadataHedgingMetrics.Name.BudgetExhausted),
                    "budget_exhausted counter not emitted.");
                Assert.IsTrue(events.Contains(eventId: 7), "OnMetadataHedgeSkipped event not emitted.");
            }
            finally
            {
                primaryRelease.Set();
                strategy.Dispose();
            }
        }

        // ---------------------------------------------------------------
        // Telemetry capture helpers
        // ---------------------------------------------------------------

        private sealed class MetricCollector : IDisposable
        {
            private readonly MeterListener listener;
            private readonly ConcurrentDictionary<string, double> counts = new ConcurrentDictionary<string, double>();
            private readonly ConcurrentDictionary<string, double> sums = new ConcurrentDictionary<string, double>();

            public MetricCollector()
            {
                this.listener = new MeterListener
                {
                    InstrumentPublished = (instrument, l) =>
                    {
                        if (instrument.Meter.Name == CosmosDbClientMetrics.MetadataHedgingMetrics.MeterName)
                        {
                            l.EnableMeasurementEvents(instrument);
                        }
                    },
                };
                this.listener.SetMeasurementEventCallback<long>(this.OnLong);
                this.listener.SetMeasurementEventCallback<double>(this.OnDouble);
                this.listener.Start();
            }

            private void OnLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
            {
                this.counts.AddOrUpdate(instrument.Name, value, (_, prev) => prev + value);
                this.sums.AddOrUpdate(instrument.Name, value, (_, prev) => prev + value);
            }

            private void OnDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
            {
                this.counts.AddOrUpdate(instrument.Name, 1, (_, prev) => prev + 1);
                this.sums.AddOrUpdate(instrument.Name, value, (_, prev) => prev + value);
            }

            public double GetCount(string instrumentName) => this.counts.TryGetValue(instrumentName, out double v) ? v : 0;

            public double GetSum(string instrumentName) => this.sums.TryGetValue(instrumentName, out double v) ? v : 0;

            public void Dispose() => this.listener.Dispose();
        }

        private sealed class EventCollector : EventListener
        {
            private readonly ConcurrentBag<int> capturedEventIds = new ConcurrentBag<int>();
            private EventSource source;

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "Azure-Cosmos-Operation-Request-Diagnostics")
                {
                    this.source = eventSource;
                    this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                this.capturedEventIds.Add(eventData.EventId);
            }

            public bool Contains(int eventId) => this.capturedEventIds.Any(x => x == eventId);

            public override void Dispose()
            {
                if (this.source != null)
                {
                    this.DisableEvents(this.source);
                }

                base.Dispose();
            }
        }

        // ---------------------------------------------------------------
        // Strategy / request helpers (mirror MetadataHedgingStrategyTests)
        // ---------------------------------------------------------------

        private static MetadataHedgingStrategy BuildStrategy(
            TimeSpan? threshold = null,
            int perClientConcurrencyBudget = MetadataHedgingStrategy.DefaultPerClientConcurrencyBudget)
        {
            return new MetadataHedgingStrategy(
                globalEndpointManager: BuildEndpointManagerMock().Object,
                isHedgingDisabledByGateway: () => false,
                isPpafEnabled: () => true,
                customerOptIn: true,
                threshold: threshold ?? TimeSpan.FromMilliseconds(100),
                perClientConcurrencyBudget: perClientConcurrencyBudget);
        }

        private static Mock<IGlobalEndpointManager> BuildEndpointManagerMock()
        {
            Mock<IGlobalEndpointManager> mock = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            ReadOnlyCollection<Uri> reads = new ReadOnlyCollection<Uri>(new[] { PrimaryEndpoint, HedgeEndpoint });
            mock.Setup(g => g.ReadEndpoints).Returns(reads);
            mock.Setup(g => g.GetApplicableEndpoints(It.IsAny<DocumentServiceRequest>(), It.IsAny<bool>())).Returns(reads);
            mock.Setup(g => g.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>())).Returns(PrimaryEndpoint);
            mock.Setup(g => g.GetLocation(PrimaryEndpoint)).Returns(PrimaryRegion);
            mock.Setup(g => g.GetLocation(HedgeEndpoint)).Returns(HedgeRegion);
            return mock;
        }

        private static MetadataHedgingContext NewColdStartContext()
        {
            return new MetadataHedgingContext
            {
                IsColdStart = true,
                IsFirstReadFeedPage = true,
            };
        }

        private static DocumentServiceResponse NewOkResponse()
        {
            return new DocumentServiceResponse(Stream.Null, new StoreResponseNameValueCollection(), HttpStatusCode.OK);
        }
    }
}
