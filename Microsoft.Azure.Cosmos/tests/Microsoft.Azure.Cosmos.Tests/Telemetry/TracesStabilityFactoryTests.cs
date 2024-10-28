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

    [TestClass]
    public class TracesStabilityFactoryTests
    {
        private const string StabilityEnvVariableName = "OTEL_SEMCONV_STABILITY_OPT_IN";
        private const string OperationName = "operationName";
        private const string ExceptionMessage = "Exception of type 'System.Exception' was thrown";

        [TestInitialize]
        public void TestInitialize()
        {
            Environment.SetEnvironmentVariable(StabilityEnvVariableName, null);
        }

        [TestMethod]
        [DataRow(OpenTelemetryStablityModes.Database, DisplayName = "Only NEW attributes when OTEL_SEMCONV_STABILITY_OPT_IN is set to 'database'")]
        [DataRow(OpenTelemetryStablityModes.DatabaseDupe, DisplayName = "NEW and OLD attributes when OTEL_SEMCONV_STABILITY_OPT_IN is 'database/dup'")]
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
            TracesStabilityFactory.SetAttributes(
                scope: scope,
                operationName: OperationName,
                databaseName: "databaseName",
                containerName: "containerName",
                accountName: "accountName",
                userAgent: "userAgent",
                machineId: "machineId",
                clientId: "clientId",
                connectionMode: "connectionMode");

            TracesStabilityFactory.SetAttributes(
                scope: scope,
                exception: new Exception());

            TracesStabilityFactory.SetAttributes(
                scope: scope,
                operationType: "operationType",
                queryTextMode: QueryTextMode.All,
                response: new Cosmos.Telemetry.OpenTelemetryAttributes());
        }

        private static void VerifyTagCollection(DiagnosticScope scope, string stabilityMode)
        {
            ActivityTagsCollection tagCollection = GetTagCollection(scope);
            Assert.IsNotNull(tagCollection);

            if (stabilityMode == OpenTelemetryStablityModes.Database)
            {
                Assert.AreEqual(13, tagCollection.Count);
                VerifyNewAttributesOnly(tagCollection);
            }
            else if (stabilityMode == OpenTelemetryStablityModes.DatabaseDupe)
            {
                Assert.AreEqual(21, tagCollection.Count);
                VerifyNewAndOldAttributes(tagCollection);
            }
            else
            {
                Assert.AreEqual(15, tagCollection.Count);
                VerifyDefaultAttributes(tagCollection);
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
            Assert.AreEqual("Microsoft.DocumentDB", tagCollection[OpenTelemetryAttributeKeys.ResourceProviderNamespace]);
            Assert.AreEqual("operationName", tagCollection[OpenTelemetryAttributeKeys.DbOperation]);
            Assert.AreEqual("databaseName", tagCollection[OpenTelemetryAttributeKeys.DbName]);
            Assert.AreEqual("containerName", tagCollection[OpenTelemetryAttributeKeys.ContainerName]);
            Assert.AreEqual("accountName", tagCollection[OpenTelemetryAttributeKeys.ServerAddress]);
            Assert.AreEqual("userAgent", tagCollection[OpenTelemetryAttributeKeys.UserAgent]);
            Assert.AreEqual("clientId", tagCollection[OpenTelemetryAttributeKeys.ClientId]);
            Assert.AreEqual("connectionMode", tagCollection[OpenTelemetryAttributeKeys.ConnectionMode]);
            Assert.AreEqual("Exception", tagCollection[OpenTelemetryAttributeKeys.ExceptionType]);
            Assert.AreEqual("Exception of type 'System.Exception' was thrown", tagCollection[OpenTelemetryAttributeKeys.ExceptionMessage]);
            Assert.AreEqual("0", tagCollection[OpenTelemetryAttributeKeys.SubStatusCode]);
            Assert.AreEqual("0", tagCollection[OpenTelemetryAttributeKeys.StatusCode]);
            Assert.AreEqual("0", tagCollection[OpenTelemetryAttributeKeys.RequestCharge]);

        }

        private static void VerifyNewAndOldAttributes(ActivityTagsCollection tagCollection)
        {
            VerifyNewAttributesOnly(tagCollection);
            VerifyDefaultAttributes(tagCollection);
        }

        private static void VerifyDefaultAttributes(ActivityTagsCollection tagCollection)
        {
            Assert.AreEqual("Microsoft.DocumentDB", tagCollection[OpenTelemetryAttributeKeys.ResourceProviderNamespace]);
            Assert.AreEqual("operationName", tagCollection[AppInsightClassicAttributeKeys.DbOperation]);
            Assert.AreEqual("databaseName", tagCollection[AppInsightClassicAttributeKeys.DbName]);
            Assert.AreEqual("containerName", tagCollection[AppInsightClassicAttributeKeys.ContainerName]);
            Assert.AreEqual("accountName", tagCollection[AppInsightClassicAttributeKeys.ServerAddress]);
            Assert.AreEqual("userAgent", tagCollection[AppInsightClassicAttributeKeys.UserAgent]);
            Assert.AreEqual("machineId", tagCollection[AppInsightClassicAttributeKeys.MachineId]);
            Assert.AreEqual("clientId", tagCollection[AppInsightClassicAttributeKeys.ClientId]);
            Assert.AreEqual("connectionMode", tagCollection[AppInsightClassicAttributeKeys.ConnectionMode]);
            Assert.AreEqual("Exception", tagCollection[AppInsightClassicAttributeKeys.ExceptionType]);
            Assert.AreEqual("Exception of type 'System.Exception' was thrown", tagCollection[AppInsightClassicAttributeKeys.ExceptionMessage]);
            Assert.AreEqual("0", tagCollection[AppInsightClassicAttributeKeys.SubStatusCode]);
            Assert.AreEqual("0", tagCollection[AppInsightClassicAttributeKeys.StatusCode]);
            Assert.AreEqual("0", tagCollection[AppInsightClassicAttributeKeys.RequestCharge]);
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
