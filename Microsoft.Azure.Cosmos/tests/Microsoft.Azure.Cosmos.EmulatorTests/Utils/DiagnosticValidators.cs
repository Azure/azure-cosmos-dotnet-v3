//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    internal static class DiagnosticValidator
    {
        public static void ValidatePointOperationDiagnostics(CosmosDiagnosticsContext diagnosticsContext)
        {
            JObject jObject = JObject.Parse(diagnosticsContext.ToString());
            PointDiagnosticValidatorHelper validator = new PointDiagnosticValidatorHelper();
            validator.Visit(diagnosticsContext);
            validator.Validate();
        }

        public static void ValidateQueryDiagnostics(CosmosDiagnosticsContext diagnosticsContext, bool isFirstPage)
        {
            JObject jObject = JObject.Parse(diagnosticsContext.ToString());
            QueryDiagnosticValidatorHelper validator = new QueryDiagnosticValidatorHelper();
            validator.Visit(diagnosticsContext);
            validator.Validate(isFirstPage);
        }

        internal static void ValidateCosmosDiagnosticsContext(
            CosmosDiagnosticsContext cosmosDiagnosticsContext)
        {
            Assert.IsTrue((cosmosDiagnosticsContext.StartUtc - DateTime.UtcNow) < TimeSpan.FromHours(12), $"Start Time is not valid {cosmosDiagnosticsContext.StartUtc}");
            Assert.AreNotEqual(cosmosDiagnosticsContext.UserAgent.ToString(), new UserAgentContainer().UserAgent.ToString(), "User agent not set");
            Assert.IsTrue(cosmosDiagnosticsContext.TotalRequestCount > 0, "No request found");
            Assert.IsFalse(cosmosDiagnosticsContext.IsComplete(), "OverallClientRequestTime should be stopped");
            Assert.IsTrue(cosmosDiagnosticsContext.GetClientElapsedTime() > TimeSpan.Zero, "OverallClientRequestTime should have time.");

            string info = cosmosDiagnosticsContext.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info.ToString());
            JToken summary = jObject["Summary"];
            Assert.IsNotNull(summary["UserAgent"].ToString());
            Assert.AreNotEqual(summary["UserAgent"].ToString(), new UserAgentContainer().UserAgent);
            Assert.IsNotNull(summary["StartUtc"].ToString());
            Assert.IsNotNull(summary["TotalElapsedTime"].ToString());
        }

        private static void ValidateScope(CosmosDiagnosticScope scope, TimeSpan? totalElapsedTime)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(scope.Id));
            Assert.IsTrue(scope.TryGetElapsedTime(out TimeSpan elapsedTime));
            Assert.IsTrue(elapsedTime > TimeSpan.Zero);

            if (totalElapsedTime.HasValue)
            {
                Assert.IsTrue(elapsedTime <= totalElapsedTime, $"Scope should not have larger time than the entire context. Scope: {elapsedTime} Total: {totalElapsedTime.Value}");
            }

            string info = scope.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info.ToString());
            Assert.IsNotNull(jObject["Id"].ToString());
            string elapsedTimeFromJson = jObject["ElapsedTime"].ToString();
            Assert.IsNotNull(elapsedTimeFromJson);
            TimeSpan.Parse(elapsedTimeFromJson);
        }

        private static void ValidateAddressResolutionStatistics(AddressResolutionStatistics stats)
        {
            Assert.IsTrue(stats.EndTime.HasValue);
            Assert.AreNotEqual(stats.StartTime, stats.EndTime.Value);
            Assert.IsTrue(stats.StartTime < stats.EndTime);
            Assert.IsFalse(string.IsNullOrWhiteSpace(stats.TargetEndpoint));

            string info = stats.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info.ToString());
            Assert.IsNotNull(jObject["StartTimeUtc"].ToString());
            Assert.IsNotNull(jObject["EndTimeUtc"].ToString());
            Assert.IsNotNull(jObject["TargetEndpoint"].ToString());
        }

        private static void ValidateQueryPageDiagnostics(QueryPageDiagnostics stats)
        {
            Assert.IsNotNull(stats.PartitionKeyRangeId);
            Assert.IsNotNull(stats.DiagnosticsContext);

            PointDiagnosticValidatorHelper pointDiagnosticValidatorHelper = new PointDiagnosticValidatorHelper();
            pointDiagnosticValidatorHelper.Visit(stats.DiagnosticsContext);

            string info = stats.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info.ToString());
            Assert.IsNotNull(jObject["StartUtc"].ToString());
            Assert.IsNotNull(jObject["PKRangeId"].ToString());
            Assert.IsNotNull(jObject["QueryMetric"].ToString());
            Assert.IsNotNull(jObject["Context"].ToString());
        }

        private static void ValidateClientSideRequestStatistics(CosmosClientSideRequestStatistics stats)
        {
            Assert.IsNotNull(stats.ContactedReplicas);
            Assert.IsNotNull(stats.DiagnosticsContext);
            Assert.IsNotNull(stats.RegionsContacted);
            Assert.IsNotNull(stats.FailedReplicas);

            // If all the request failed it's possible to not contact a region or replica.
            if (stats.DiagnosticsContext.TotalRequestCount < stats.DiagnosticsContext.FailedRequestCount)
            {
                Assert.IsTrue(stats.RegionsContacted.Count > 0);
                Assert.IsTrue(stats.ContactedReplicas.Count > 0);
            }
        }

        private static void ValidateStoreResponseStatistics(StoreResponseStatistics stats, DateTime startTimeUtc)
        {
            Assert.IsNotNull(stats.StoreResult);
            Assert.IsNotNull(stats.LocationEndpoint);
            Assert.IsTrue(startTimeUtc < stats.RequestResponseTime);
            Assert.IsTrue(stats.RequestResponseTime < DateTime.UtcNow);

            string info = stats.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info.ToString());
            Assert.IsNotNull(jObject["ResponseTimeUtc"].ToString());
            Assert.IsNotNull(jObject["ResourceType"].ToString());
            Assert.IsNotNull(jObject["OperationType"].ToString());
            Assert.IsNotNull(jObject["LocationEndpoint"].ToString());
            Assert.IsNotNull(jObject["StoreResult"].ToString());
        }

        private static void ValidatePointOperationStatistics(PointOperationStatistics stats, DateTime startTimeUtc)
        {
            Assert.IsNotNull(stats.ActivityId);
            Assert.AreNotEqual(Guid.Empty.ToString(), stats.ActivityId);

            Assert.IsNotNull(stats.RequestUri);

            // Need to come up with stronger validation for request charge.
            // Exactly when will an exception have a request charge? 

            Assert.IsNotNull(stats.Method);
            Assert.IsNotNull(stats.ResponseTimeUtc);
            Assert.IsTrue(startTimeUtc < stats.ResponseTimeUtc);
            Assert.IsTrue(stats.ResponseTimeUtc < DateTime.UtcNow);

            string info = stats.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info.ToString());
            int statusCode = jObject["StatusCode"].ToObject<int>();
            Assert.IsTrue(statusCode > 0);
            Assert.IsNotNull(jObject["ActivityId"].ToString());
            Assert.IsNotNull(jObject["StatusCode"].ToString());
            Assert.IsNotNull(jObject["RequestCharge"].ToString());
            Assert.IsNotNull(jObject["RequestUri"].ToString());
        }

        private sealed class QueryDiagnosticValidatorHelper : CosmosDiagnosticsInternalVisitor
        {
            private DateTime? StartTimeUtc = null;
            private TimeSpan? TotalElapsedTime = null;
            private bool isScopeVisited = false;
            private bool isContextVisited = false;
            private bool isQueryPageVisited = false;

            public override void Visit(PointOperationStatistics pointOperationStatistics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(pointOperationStatistics)}");
            }

            public override void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            {
                this.isContextVisited = true;
                this.StartTimeUtc = cosmosDiagnosticsContext.StartUtc;
                this.TotalElapsedTime = cosmosDiagnosticsContext.GetClientElapsedTime();

                // Buffered pages are normal and have 0 request. This causes most validation to fail.
                if(cosmosDiagnosticsContext.TotalRequestCount > 0)
                {
                    DiagnosticValidator.ValidateCosmosDiagnosticsContext(cosmosDiagnosticsContext);
                }
                
                foreach (CosmosDiagnosticsInternal diagnosticsInternal in cosmosDiagnosticsContext)
                {
                    diagnosticsInternal.Accept(this);
                }
            }

            public override void Visit(CosmosDiagnosticScope cosmosDiagnosticScope)
            {
                this.isScopeVisited = true;
                ValidateScope(cosmosDiagnosticScope, this.TotalElapsedTime.Value);
            }

            public override void Visit(QueryPageDiagnostics queryPageDiagnostics)
            {
                this.isQueryPageVisited = true;
                ValidateQueryPageDiagnostics(queryPageDiagnostics);
            }

            public override void Visit(AddressResolutionStatistics addressResolutionStatistics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(addressResolutionStatistics)}");
            }

            public override void Visit(StoreResponseStatistics storeResponseStatistics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(storeResponseStatistics)}");
            }

            public override void Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(clientSideRequestStatistics)}");
            }

            public void Validate(bool isFirstPage)
            {
                Assert.IsTrue(this.isContextVisited);
                Assert.IsNotNull(this.StartTimeUtc);
                Assert.IsNotNull(this.TotalElapsedTime);

                // Only first page will have a scope for the query pipeline creation
                if (isFirstPage)
                {
                    Assert.IsTrue(this.isScopeVisited);
                    Assert.IsTrue(this.isQueryPageVisited);
                }
            }
        }

        private sealed class PointDiagnosticValidatorHelper : CosmosDiagnosticsInternalVisitor
        {
            private DateTime? StartTimeUtc = null;
            private TimeSpan? TotalElapsedTime = null;
            private bool containsFailures = false;
            private bool isContextVisited = false;
            private bool isScopeVisited = false;
            private bool isStoreResponseStatisticsVisited = false;
            private bool isCosmosClientSideRequestStatisticsVisited = false;
            private bool isPointOperationStatisticsVisited = false;

            public override void Visit(PointOperationStatistics pointOperationStatistics)
            {
                Assert.IsTrue(this.isContextVisited);
                this.isPointOperationStatisticsVisited = true;
                DiagnosticValidator.ValidatePointOperationStatistics(pointOperationStatistics, this.StartTimeUtc.Value);
                if (!pointOperationStatistics.StatusCode.IsSuccess())
                {
                    this.containsFailures = true;
                }
            }

            public override void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            {
                Assert.IsFalse(this.isContextVisited, "Point operations should only have a single context");
                this.isContextVisited = true;
                this.StartTimeUtc = cosmosDiagnosticsContext.StartUtc;
                this.TotalElapsedTime = cosmosDiagnosticsContext.GetClientElapsedTime();

                DiagnosticValidator.ValidateCosmosDiagnosticsContext(cosmosDiagnosticsContext);

                foreach (CosmosDiagnosticsInternal diagnosticsInternal in cosmosDiagnosticsContext)
                {
                    diagnosticsInternal.Accept(this);
                }
            }

            public override void Visit(CosmosDiagnosticScope cosmosDiagnosticScope)
            {
                Assert.IsTrue(this.isContextVisited);
                this.isScopeVisited = true;
                DiagnosticValidator.ValidateScope(cosmosDiagnosticScope, this.TotalElapsedTime);
            }

            public override void Visit(QueryPageDiagnostics queryPageDiagnostics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(queryPageDiagnostics)}");
            }

            // AddressResolution does not happen on every request.
            public override void Visit(AddressResolutionStatistics addressResolutionStatistics)
            {
                Assert.IsTrue(this.isContextVisited);
                DiagnosticValidator.ValidateAddressResolutionStatistics(addressResolutionStatistics);
            }

            public override void Visit(StoreResponseStatistics storeResponseStatistics)
            {
                Assert.IsTrue(this.isContextVisited);
                this.isStoreResponseStatisticsVisited = true;
                DiagnosticValidator.ValidateStoreResponseStatistics(storeResponseStatistics, this.StartTimeUtc.Value);
            }

            public override void Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics)
            {
                Assert.IsTrue(this.isContextVisited);
                this.isCosmosClientSideRequestStatisticsVisited = true;
                DiagnosticValidator.ValidateClientSideRequestStatistics(clientSideRequestStatistics);
            }

            public void Validate()
            {
                Assert.IsTrue(this.isContextVisited);
                Assert.IsNotNull(this.StartTimeUtc);
                Assert.IsNotNull(this.TotalElapsedTime);
                Assert.IsTrue(this.isScopeVisited);

                // If HA layer throws DocumentClientException then it will be recorded as a PointOperationStatistics object. 
                if (!this.containsFailures)
                {
                    Assert.AreNotEqual(
                        this.isPointOperationStatisticsVisited,
                        this.isCosmosClientSideRequestStatisticsVisited,
                        "Gateway mode should only have PointOperationStatistics. Direct mode should only have CosmosClientSideRequestStatistics");

                    if (this.isCosmosClientSideRequestStatisticsVisited)
                    {
                        Assert.IsTrue(this.isStoreResponseStatisticsVisited);
                    }
                }
            }
        }
    }
}
