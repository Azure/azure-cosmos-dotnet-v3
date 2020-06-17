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
            _ = JObject.Parse(diagnosticsContext.ToString());
            PointDiagnosticValidatorHelper validator = new PointDiagnosticValidatorHelper();
            validator.Visit(diagnosticsContext);
            validator.Validate();
        }

        public static void ValidateChangeFeedOperationDiagnostics(CosmosDiagnosticsContext diagnosticsContext)
        {
            _ = JObject.Parse(diagnosticsContext.ToString());
            ChangeFeedDiagnosticValidatorHelper validator = new ChangeFeedDiagnosticValidatorHelper();
            validator.Visit(diagnosticsContext);
            validator.Validate();
        }


        public static void ValidateQueryDiagnostics(CosmosDiagnosticsContext diagnosticsContext, bool isFirstPage)
        {
            _ = JObject.Parse(diagnosticsContext.ToString());
            QueryDiagnosticValidatorHelper validator = new QueryDiagnosticValidatorHelper();
            validator.Visit(diagnosticsContext);
            validator.Validate(isFirstPage);
        }

        public static void ValidateQueryGatewayPlanDiagnostics(CosmosDiagnosticsContext diagnosticsContext, bool isFirstPage)
        {
            string diagnostics = diagnosticsContext.ToString();
            _ = JObject.Parse(diagnostics);
            QueryGatewayPlanDiagnosticValidatorHelper validator = new QueryGatewayPlanDiagnosticValidatorHelper();
            validator.Visit(diagnosticsContext);
            validator.Validate(isFirstPage);
        }

        internal static void ValidateCosmosDiagnosticsContext(
            CosmosDiagnosticsContext cosmosDiagnosticsContext)
        {
            Assert.IsTrue((cosmosDiagnosticsContext.StartUtc - DateTime.UtcNow) < TimeSpan.FromHours(12), $"Start Time is not valid {cosmosDiagnosticsContext.StartUtc}");
            Assert.AreNotEqual(cosmosDiagnosticsContext.UserAgent.ToString(), new UserAgentContainer().UserAgent.ToString(), "User agent not set");
            Assert.IsTrue(cosmosDiagnosticsContext.GetTotalRequestCount() > 0, "No request found");
            Assert.IsTrue(cosmosDiagnosticsContext.IsComplete(), "OverallClientRequestTime should be stopped");
            Assert.IsTrue(cosmosDiagnosticsContext.GetRunningElapsedTime() > TimeSpan.Zero, "OverallClientRequestTime should have time.");

            string info = cosmosDiagnosticsContext.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info.ToString());
            Assert.IsNotNull(jObject["DiagnosticVersion"].ToString()); 
            JToken summary = jObject["Summary"];
            Assert.IsNotNull(summary["UserAgent"].ToString());
            Assert.AreNotEqual(summary["UserAgent"].ToString(), new UserAgentContainer().UserAgent);
            Assert.IsNotNull(summary["StartUtc"].ToString());
            Assert.IsNotNull(summary["TotalElapsedTimeInMs"].ToString());
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
            string elapsedTimeFromJson = jObject["ElapsedTimeInMs"].ToString();
            Assert.IsNotNull(elapsedTimeFromJson);
            double elapsedInMs = double.Parse(elapsedTimeFromJson);
            Assert.IsTrue(elapsedInMs > 0);
        }

        private static void ValidateProcessInfo(CosmosSystemInfo processInfo)
        {
            Assert.IsNotNull(processInfo.CpuLoadHistory);
            Assert.IsNotNull(processInfo.CpuLoadHistory.ToString());

            string info = processInfo.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info);
            Assert.AreEqual("SystemInfo", jObject["Id"].ToString());
            Assert.IsNotNull(jObject["CpuHistory"].ToString());
        }

        private static void ValidateRequestHandlerScope(RequestHandlerScope scope, TimeSpan? totalElapsedTime)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(scope.Id));
            Assert.IsTrue(scope.TryGetTotalElapsedTime(out TimeSpan scopeTotalElapsedTime));
            Assert.IsTrue(scopeTotalElapsedTime > TimeSpan.Zero);

            if (totalElapsedTime.HasValue)
            {
                Assert.IsTrue(
                    scopeTotalElapsedTime <= totalElapsedTime,
                    $"RequestHandlerScope should not have larger time than the entire context. Scope: {totalElapsedTime} Total: {totalElapsedTime.Value}");
            }

            string info = scope.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info.ToString());
            Assert.IsNotNull(jObject["Id"].ToString());
            string elapsedTimeFromJson = jObject["HandlerElapsedTimeInMs"].ToString();
            Assert.IsNotNull(elapsedTimeFromJson);
            double elapsedInMs = double.Parse(elapsedTimeFromJson);
            Assert.IsTrue(elapsedInMs > 0);
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
            Assert.IsNotNull(jObject["ClientCorrelationId"].ToString());
        }

        private static void ValidateClientSideRequestStatistics(CosmosClientSideRequestStatistics stats, HttpStatusCode? statusCode)
        {
            Assert.IsNotNull(stats.ContactedReplicas);
            Assert.IsNotNull(stats.DiagnosticsContext);
            Assert.IsNotNull(stats.RegionsContacted);
            Assert.IsNotNull(stats.FailedReplicas);

            if (stats.DiagnosticsContext.GetFailedRequestCount() == 0)
            {
                Assert.AreEqual(stats.EstimatedClientDelayFromAllCauses, TimeSpan.Zero);
                Assert.AreEqual(stats.EstimatedClientDelayFromRateLimiting, TimeSpan.Zero);
            }
            else if (statusCode != null && (int)statusCode == 429)
            {
                Assert.AreNotEqual(stats.EstimatedClientDelayFromAllCauses, TimeSpan.Zero);
                Assert.AreNotEqual(stats.EstimatedClientDelayFromRateLimiting, TimeSpan.Zero);
            }

            // If all the request failed it's possible to not contact a region or replica.
            if (stats.DiagnosticsContext.GetTotalRequestCount() < stats.DiagnosticsContext.GetFailedRequestCount())
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
            Assert.IsNotNull(jObject["StartTimeUtc"].ToString());
            Assert.IsNotNull(jObject["ResponseTimeUtc"].ToString());
            Assert.IsNotNull(jObject["ElapsedTimeInMs"].ToString());
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
            Assert.IsNotNull(stats.RequestCharge);
            if (stats.StatusCode != HttpStatusCode.RequestEntityTooLarge &&
                stats.StatusCode != HttpStatusCode.RequestTimeout)
            {
                Assert.IsTrue(stats.RequestCharge > 0);
            }

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

        /// <summary>
        /// Getting the query plan from the gateway should have a point operation.
        /// The normal service interop query plan generation does not have any network calls.
        /// </summary>
        private sealed class QueryGatewayPlanDiagnosticValidatorHelper : QueryDiagnosticValidatorHelper
        {
            private bool isPointOperationStatisticsVisited = false;
            private bool isClientSideRequestStatisticsVisited = false;

            public override void Visit(PointOperationStatistics pointOperationStatistics)
            {
                Assert.IsFalse(this.isPointOperationStatisticsVisited, $"Should only be a single {nameof(PointOperationStatistics)}");
                this.isPointOperationStatisticsVisited = true;
            }

            public override void Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics)
            {
                Assert.IsFalse(this.isClientSideRequestStatisticsVisited, $"Should only be a single {nameof(CosmosClientSideRequestStatistics)}");
                this.isClientSideRequestStatisticsVisited = true;
            }

            public override void Validate(bool isFirstPage)
            {
                base.Validate(isFirstPage);
                if (isFirstPage)
                {
                    Assert.IsTrue(this.isPointOperationStatisticsVisited);
                    Assert.IsTrue(this.isClientSideRequestStatisticsVisited);
                }
            }
        }

        private class QueryDiagnosticValidatorHelper : CosmosDiagnosticsInternalVisitor
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
                this.TotalElapsedTime = cosmosDiagnosticsContext.GetRunningElapsedTime();

                // Buffered pages are normal and have 0 request. This causes most validation to fail.
                if(cosmosDiagnosticsContext.GetTotalRequestCount() > 0)
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

            public override void Visit(RequestHandlerScope requestHandlerScope)
            {
               // This will be visited if it is gateway query plan
            }

            public override void Visit(CosmosSystemInfo cpuLoadHistory)
            {
                // This will be visited if it is gateway query plan
            }

            public override void Visit(QueryPageDiagnostics queryPageDiagnostics)
            {
                this.isQueryPageVisited = true;
                ValidateQueryPageDiagnostics(queryPageDiagnostics);
            }

            public override void Visit(AddressResolutionStatistics addressResolutionStatistics)
            {
                throw new ArgumentException($"Query should not have {nameof(addressResolutionStatistics)}");
            }

            public override void Visit(StoreResponseStatistics storeResponseStatistics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(storeResponseStatistics)}");
            }

            public override void Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(clientSideRequestStatistics)}");
            }

            public override void Visit(FeedRangeStatistics feedRangeStatistics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(feedRangeStatistics)}");
            }

            public virtual void Validate(bool isFirstPage)
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
            private HttpStatusCode? StatusCode = null;
            private bool containsFailures = false;
            private bool isContextVisited = false;
            private bool isRequestHandlerScopeVisited = false;
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

                this.StatusCode = pointOperationStatistics.StatusCode;
            }

            public override void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            {
                Assert.IsFalse(this.isContextVisited, "Point operations should only have a single context");
                this.isContextVisited = true;
                this.StartTimeUtc = cosmosDiagnosticsContext.StartUtc;
                this.TotalElapsedTime = cosmosDiagnosticsContext.GetRunningElapsedTime();

                DiagnosticValidator.ValidateCosmosDiagnosticsContext(cosmosDiagnosticsContext);

                foreach (CosmosDiagnosticsInternal diagnosticsInternal in cosmosDiagnosticsContext)
                {
                    diagnosticsInternal.Accept(this);
                }
            }

            public override void Visit(RequestHandlerScope requestHandlerScope)
            {
                Assert.IsTrue(this.isContextVisited);
                this.isRequestHandlerScopeVisited = true;
                DiagnosticValidator.ValidateRequestHandlerScope(requestHandlerScope, this.TotalElapsedTime);
            }

            public override void Visit(CosmosSystemInfo cpuLoadHistory)
            {
                Assert.IsTrue(this.isContextVisited);
                DiagnosticValidator.ValidateProcessInfo(cpuLoadHistory);
            }

            public override void Visit(CosmosDiagnosticScope cosmosDiagnosticScope)
            {
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
                DiagnosticValidator.ValidateClientSideRequestStatistics(clientSideRequestStatistics, this.StatusCode);
            }

            public override void Visit(FeedRangeStatistics feedRangeStatistics)
            {
                throw new ArgumentException($"Point Operation should not have {nameof(feedRangeStatistics)}");
            }

            public void Validate()
            {
                Assert.IsTrue(this.isContextVisited);
                Assert.IsNotNull(this.StartTimeUtc);
                Assert.IsNotNull(this.TotalElapsedTime);
                Assert.IsTrue(this.isRequestHandlerScopeVisited);

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

        private sealed class ChangeFeedDiagnosticValidatorHelper : CosmosDiagnosticsInternalVisitor
        {
            private bool isFeedRangeStatisticsVisited = false;
            private bool isContextVisited = false;
            private bool isRequestHandlerVisited = false;
            private DateTime? StartTimeUtc = null;
            private TimeSpan? TotalElapsedTime = null;

            public override void Visit(PointOperationStatistics pointOperationStatistics)
            {
            }

            public override void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            {
                Assert.IsFalse(this.isContextVisited, "Point operations should only have a single context");
                this.isContextVisited = true;
                this.StartTimeUtc = cosmosDiagnosticsContext.StartUtc;
                this.TotalElapsedTime = cosmosDiagnosticsContext.GetRunningElapsedTime();

                DiagnosticValidator.ValidateCosmosDiagnosticsContext(cosmosDiagnosticsContext);

                foreach (CosmosDiagnosticsInternal diagnosticsInternal in cosmosDiagnosticsContext)
                {
                    diagnosticsInternal.Accept(this);
                }
            }

            public override void Visit(RequestHandlerScope requestHandlerScope)
            {
                Assert.IsTrue(this.isContextVisited);
                this.isRequestHandlerVisited = true;
                DiagnosticValidator.ValidateRequestHandlerScope(requestHandlerScope, this.TotalElapsedTime);
            }

            public override void Visit(CosmosSystemInfo cpuLoadHistory)
            {
            }

            public override void Visit(CosmosDiagnosticScope cosmosDiagnosticScope)
            {
            }

            public override void Visit(QueryPageDiagnostics queryPageDiagnostics)
            {
            }

            public override void Visit(AddressResolutionStatistics addressResolutionStatistics)
            {
            }

            public override void Visit(StoreResponseStatistics storeResponseStatistics)
            {
            }

            public override void Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics)
            {
            }

            public override void Visit(FeedRangeStatistics feedRangeStatistics)
            {
                this.isFeedRangeStatisticsVisited = true;
            }

            public void Validate()
            {
                Assert.IsTrue(this.isFeedRangeStatisticsVisited);
                Assert.IsTrue(this.isContextVisited);
                Assert.IsNotNull(this.StartTimeUtc);
                Assert.IsNotNull(this.TotalElapsedTime);
                Assert.IsTrue(this.isRequestHandlerVisited);
            }
        }
    }
}
