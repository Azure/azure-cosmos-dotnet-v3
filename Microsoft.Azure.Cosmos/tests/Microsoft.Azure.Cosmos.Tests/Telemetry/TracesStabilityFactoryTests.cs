//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using global::Azure.Core;
    using System.Diagnostics;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Telemetry;
    using System.Diagnostics.Tracing;
    using System.Net;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Cosmos.Tests.Tracing;

    [TestClass]
    public class TracesStabilityFactoryTests
    {
        private const string StabilityEnvVariableName = "OTEL_SEMCONV_STABILITY_OPT_IN";
        private const string OperationName = "operationName";
        private const string ExceptionMessage = "Exception of type 'System.Exception' was thrown.";

        [TestInitialize]
        public void TestInitialize()
        {
            Environment.SetEnvironmentVariable(StabilityEnvVariableName, null);
        }

        [TestMethod]
        [DataRow(OpenTelemetryStablityModes.Database, DisplayName = "Only NEW attributes when OTEL_SEMCONV_STABILITY_OPT_IN is set to 'database'")]
        [DataRow(OpenTelemetryStablityModes.DatabaseDupe, DisplayName = "NEW and OLD attributes when OTEL_SEMCONV_STABILITY_OPT_IN is 'database/dup'")]
        [DataRow(OpenTelemetryStablityModes.ClassicAppInsights, DisplayName = "NEW and OLD attributes when OTEL_SEMCONV_STABILITY_OPT_IN is 'appinsightssdk'")]
        [DataRow(null, DisplayName = "Default Scenario when OTEL_SEMCONV_STABILITY_OPT_IN is not set")]
        public void SetAttributeBasedOnStabilityModeTest(string stabilityMode)
        {
            Environment.SetEnvironmentVariable(StabilityEnvVariableName, stabilityMode);
            TracesStabilityFactory.RefreshStabilityMode();

            using NoOpListener listener = new NoOpListener();
            DiagnosticScopeFactory factory = CreateDiagnosticScopeFactory();
            using DiagnosticScope scope = factory.CreateScope("sample_activity_" + Random.Shared.Next(), ActivityKind.Client);

            SetAttributes(scope);
            VerifyTagCollection(scope, stabilityMode);
        }

        private static DiagnosticScopeFactory CreateDiagnosticScopeFactory()
        {
            return new DiagnosticScopeFactory(
                clientNamespace: OpenTelemetryAttributeKeys.DiagnosticNamespace,
                resourceProviderNamespace: OpenTelemetryAttributeKeys.ResourceProviderNamespace,
                isActivityEnabled: true,
                suppressNestedClientActivities: true,
                isStable: true);
        }

        private static void SetAttributes(DiagnosticScope scope)
        {
            ITrace trace;
            using (trace = Cosmos.Tracing.Trace.GetRootTrace("Root Trace", TraceComponent.Unknown, Cosmos.Tracing.TraceLevel.Info))
            {
                using (ITrace firstLevel = trace.StartChild("First level Node", TraceComponent.Unknown, Cosmos.Tracing.TraceLevel.Info))
                {
                    firstLevel.AddDatum("Client Side Request Stats", GetDatumObject(Regions.FranceCentral));
                }
            }

            IActivityAttributePopulator  populator = TracesStabilityFactory.GetAttributePopulator();
            populator.PopulateAttributes(
                scope: scope,
                operationName: OperationName,
                databaseName: "databaseName",
                containerName: "containerName",
                accountName: new Uri("http://accountname:443"),
                userAgent: "userAgent",
                machineId: "machineId",
                clientId: "clientId",
                connectionMode: "gateway");

            populator.PopulateAttributes(
                scope: scope,
                exception: new Exception());

            populator.PopulateAttributes(
                scope: scope,
                operationType: "operationType",
                queryTextMode: QueryTextMode.All,
                response: new Cosmos.Telemetry.OpenTelemetryAttributes
                {
                    StatusCode = HttpStatusCode.OK,
                    RequestCharge = 1.0,
                    RequestContentLength = "100",
                    ResponseContentLength = "200",
                    ItemCount = "10",
                    Diagnostics = new CosmosTraceDiagnostics(trace),
                    SubStatusCode = 0,
                    ActivityId = "dummyActivityId",
                    CorrelatedActivityId = "dummyCorrelatedActivityId",
                    OperationType = Documents.OperationType.Read,
                    ResourceType = Documents.ResourceType.Document,
                    BatchSize = 5,
                    QuerySpec = new SqlQuerySpec("SELECT * FROM c"),
                    ConsistencyLevel = "Strong"
                });
        }

        private static TraceDatum GetDatumObject(string regionName1, string regionName2 = null)
        {
            ClientSideRequestStatisticsTraceDatum datum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, Cosmos.Tracing.Trace.GetRootTrace(nameof(ContactedRegionsTests)));
            Uri uri1 = new Uri("http://someUri1.com");
            datum.RegionsContacted.Add((regionName1, uri1));
            if (regionName2 != null)
            {
                Uri uri2 = new Uri("http://someUri2.com");
                datum.RegionsContacted.Add((regionName2, uri2));
            }

            return datum;
        }

        private static void VerifyTagCollection(DiagnosticScope scope, string stabilityMode)
        {
            ActivityTagsCollection tagCollection = GetTagCollection(scope);
            Assert.IsNotNull(tagCollection);
            Console.WriteLine(tagCollection.Count);
            tagCollection.ToList().ForEach(tag => Console.WriteLine($"{tag.Key} : {tag.Value}"));
            if (stabilityMode == OpenTelemetryStablityModes.Database)
            {
                Assert.AreEqual(23, tagCollection.Count);
                VerifyNewAttributesOnly(tagCollection);
            }
            else if (stabilityMode == OpenTelemetryStablityModes.DatabaseDupe)
            {
                Assert.AreEqual(40, tagCollection.Count);
                VerifyNewAndOldAttributes(tagCollection);
            }
            else if (stabilityMode == OpenTelemetryStablityModes.ClassicAppInsights)
            {
                Assert.AreEqual(20, tagCollection.Count);
                VerifyOldAttributes(tagCollection);
            }
            else
            {
                Assert.AreEqual(23, tagCollection.Count);
                VerifyNewAttributesOnly(tagCollection);
            }
        }

        private static ActivityTagsCollection GetTagCollection(DiagnosticScope scope)
        {
            var activityAdaptor = typeof(DiagnosticScope)
                .GetField("_activityAdapter", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(scope);
            return activityAdaptor?.GetType()
                .GetField("_tagCollection", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(activityAdaptor) as ActivityTagsCollection;
        }

        private static void VerifyNewAttributesOnly(ActivityTagsCollection tagCollection)
        {
            Assert.AreEqual("Microsoft.DocumentDB", tagCollection["az.namespace"]);
            Assert.AreEqual("operationName", tagCollection[OpenTelemetryAttributeKeys.DbOperation]);
            Assert.AreEqual("databaseName", tagCollection[OpenTelemetryAttributeKeys.DbName]);
            Assert.AreEqual("containerName", tagCollection[OpenTelemetryAttributeKeys.ContainerName]);
            Assert.AreEqual("accountname", tagCollection[OpenTelemetryAttributeKeys.ServerAddress]);
            Assert.AreEqual(443, tagCollection[OpenTelemetryAttributeKeys.ServerPort]);
            Assert.AreEqual("userAgent", tagCollection[OpenTelemetryAttributeKeys.UserAgent]);
            Assert.AreEqual("clientId", tagCollection[OpenTelemetryAttributeKeys.ClientId]);
            Assert.AreEqual("gateway", tagCollection[OpenTelemetryAttributeKeys.ConnectionMode]);
            Assert.AreEqual("Exception", tagCollection[OpenTelemetryAttributeKeys.ExceptionType]);
            Assert.AreEqual(ExceptionMessage, tagCollection[OpenTelemetryAttributeKeys.ExceptionMessage]);
            Assert.AreEqual(5, tagCollection[OpenTelemetryAttributeKeys.BatchSize]);
            Assert.AreEqual(200, tagCollection[OpenTelemetryAttributeKeys.StatusCode]);
            Assert.AreEqual(0, tagCollection[OpenTelemetryAttributeKeys.SubStatusCode]);
            Assert.AreEqual(1, tagCollection[OpenTelemetryAttributeKeys.RequestCharge]);
            Assert.AreEqual("100", tagCollection[OpenTelemetryAttributeKeys.RequestContentLength]);
            Assert.AreEqual("200", tagCollection[OpenTelemetryAttributeKeys.ResponseContentLength]);
            Assert.AreEqual("10", tagCollection[OpenTelemetryAttributeKeys.ItemCount]);
            Assert.AreEqual("dummyActivityId", tagCollection[OpenTelemetryAttributeKeys.ActivityId]);
            Assert.AreEqual("dummyCorrelatedActivityId", tagCollection[OpenTelemetryAttributeKeys.CorrelatedActivityId]);
            Assert.AreEqual("Strong", tagCollection[OpenTelemetryAttributeKeys.ConsistencyLevel]);
            Assert.AreEqual("SELECT * FROM c", tagCollection[OpenTelemetryAttributeKeys.QueryText]);
            Assert.AreEqual("France Central", tagCollection[OpenTelemetryAttributeKeys.Region]);
        }

        private static void VerifyNewAndOldAttributes(ActivityTagsCollection tagCollection)
        {
            VerifyNewAttributesOnly(tagCollection);
            VerifyOldAttributes(tagCollection);
        }

        private static void VerifyOldAttributes(ActivityTagsCollection tagCollection)
        {
            Assert.AreEqual("Microsoft.DocumentDB", tagCollection["az.namespace"]);
            Assert.AreEqual("operationName", tagCollection[AppInsightClassicAttributeKeys.DbOperation]);
            Assert.AreEqual("databaseName", tagCollection[AppInsightClassicAttributeKeys.DbName]);
            Assert.AreEqual("containerName", tagCollection[AppInsightClassicAttributeKeys.ContainerName]);
            Assert.AreEqual("accountname", tagCollection[AppInsightClassicAttributeKeys.ServerAddress]);
            Assert.AreEqual("userAgent", tagCollection[AppInsightClassicAttributeKeys.UserAgent]);
            Assert.AreEqual("machineId", tagCollection[AppInsightClassicAttributeKeys.MachineId]);
            Assert.AreEqual("clientId", tagCollection[AppInsightClassicAttributeKeys.ClientId]);
            Assert.AreEqual("gateway", tagCollection[AppInsightClassicAttributeKeys.ConnectionMode]);
            Assert.AreEqual("Exception", tagCollection[AppInsightClassicAttributeKeys.ExceptionType]);
            Assert.AreEqual(ExceptionMessage, tagCollection[AppInsightClassicAttributeKeys.ExceptionMessage]);
            Assert.AreEqual(200, tagCollection[AppInsightClassicAttributeKeys.StatusCode]);
            Assert.AreEqual(0, tagCollection[AppInsightClassicAttributeKeys.SubStatusCode]);
            Assert.AreEqual(1, tagCollection[AppInsightClassicAttributeKeys.RequestCharge]);
            Assert.AreEqual("100", tagCollection[AppInsightClassicAttributeKeys.RequestContentLength]);
            Assert.AreEqual("200", tagCollection[AppInsightClassicAttributeKeys.ResponseContentLength]);
            Assert.AreEqual("10", tagCollection[AppInsightClassicAttributeKeys.ItemCount]);
            Assert.AreEqual("dummyActivityId", tagCollection[AppInsightClassicAttributeKeys.ActivityId]);
            Assert.AreEqual("France Central", tagCollection[AppInsightClassicAttributeKeys.Region]);
            Assert.AreEqual("operationType", tagCollection[AppInsightClassicAttributeKeys.OperationType]);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(StabilityEnvVariableName, null);
        }

        internal class NoOpListener :
            EventListener,
            IObserver<KeyValuePair<string, object>>,
            IObserver<DiagnosticListener>,
            IDisposable
        {
            private readonly ConcurrentBag<IDisposable> subscriptions = new();

            public NoOpListener()
            {
                DiagnosticListener.AllListeners.Subscribe(this);
            }

            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(KeyValuePair<string, object> value) { }
            public void OnNext(DiagnosticListener value)
            {
                IDisposable subscriber = value.Subscribe(this, isEnabled: _ => true);
                this.subscriptions.Add(subscriber);
            }

            public override void Dispose()
            {
                base.Dispose();
                foreach (IDisposable subscription in this.subscriptions)
                {
                    subscription.Dispose();
                }
            }
        }
    }
}
