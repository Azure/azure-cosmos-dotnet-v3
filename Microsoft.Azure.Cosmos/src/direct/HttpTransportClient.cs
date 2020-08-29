namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;

    internal sealed class HttpTransportClient : TransportClient
    {
        private readonly HttpClient httpClient;
        private readonly ICommunicationEventSource eventSource;

        public const string Match = "Match";

        public HttpTransportClient(
            int requestTimeout,
            ICommunicationEventSource eventSource,
            UserAgentContainer userAgent = null,
            int idleTimeoutInSeconds = -1,
            HttpMessageHandler messageHandler = null)
        {
#if NETFX
            if (idleTimeoutInSeconds > 0)
            {
                ServicePointManager.MaxServicePointIdleTime = idleTimeoutInSeconds;
                ServicePointManager.SetTcpKeepAlive(true, keepAliveTime: 30000, keepAliveInterval: 1000);
            }
#endif

            if (messageHandler != null)
            {
                this.httpClient = new HttpClient(messageHandler);
            }
            else
            {
                this.httpClient = new HttpClient();
            }

            this.httpClient.Timeout = TimeSpan.FromSeconds(requestTimeout);
            this.httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            // Set requested API version header for version enforcement.
            this.httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                HttpConstants.Versions.CurrentVersion);

            if (userAgent == null)
            {
                userAgent = new UserAgentContainer();
            }

            this.httpClient.AddUserAgentHeader(userAgent);

            this.httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept, RuntimeConstants.MediaTypes.Json);

            this.eventSource = eventSource;
        }

        public override void Dispose()
        {
            base.Dispose();

            if (this.httpClient != null)
            {
                this.httpClient.Dispose();
            }
        }

        private void BeforeRequest(Guid activityId, Uri uri, ResourceType resourceType, HttpRequestHeaders requestHeaders)
        {
#if NETFX

            if (PerfCounters.Counters.BackendActiveRequests != null)
            {
                PerfCounters.Counters.BackendActiveRequests.Increment();
            }

            if (PerfCounters.Counters.BackendRequestsPerSec != null)
            {
                PerfCounters.Counters.BackendRequestsPerSec.Increment();
            }
#endif

            this.eventSource.Request(
                activityId,
                Guid.Empty,
                uri.ToString(),
                resourceType.ToResourceTypeString(),
                requestHeaders);
        }

        private void AfterRequest(Guid activityId,
            HttpStatusCode statusCode,
            double durationInMilliSeconds,
            HttpResponseHeaders responseHeaders)
        {
#if NETFX
            if (PerfCounters.Counters.BackendActiveRequests != null)
            {
                PerfCounters.Counters.BackendActiveRequests.Decrement();
            }
#endif

            this.eventSource.Response(
                activityId,
                Guid.Empty,
                (short)statusCode,
                durationInMilliSeconds,
                responseHeaders);
        }

        internal override async Task<StoreResponse> InvokeStoreAsync(
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            Guid activityId = Trace.CorrelationManager.ActivityId;
            Debug.Assert(activityId != Guid.Empty);

            INameValueCollection responseHeaders = new DictionaryNameValueCollection();
            responseHeaders.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");

            if (!request.IsBodySeekableClonableAndCountable)
            {
                throw new InternalServerErrorException(RMResources.InternalServerError, responseHeaders);
            }

#if !COSMOSCLIENT
            if (resourceOperation.operationType == OperationType.Recreate)
            {
                DefaultTrace.TraceCritical("Received Recreate request on Http client");
                throw new InternalServerErrorException(RMResources.InternalServerError, responseHeaders);
            }
#endif

            using (HttpRequestMessage requestMessage = this.PrepareHttpMessage(
                activityId,
                physicalAddress,
                resourceOperation,
                request))
            {
                HttpResponseMessage responseMessage = null;
                DateTime sendTimeUtc = DateTime.UtcNow;
                try
                {
                    this.BeforeRequest(
                        activityId,
                        requestMessage.RequestUri,
                        request.ResourceType,
                        requestMessage.Headers);

                    responseMessage = await this.httpClient.SendAsync(requestMessage,
                        HttpCompletionOption.ResponseHeadersRead);
                }
                catch (Exception exception)
                {
                    Trace.CorrelationManager.ActivityId = activityId;
                    if (WebExceptionUtility.IsWebExceptionRetriable(exception))
                    {
                        DefaultTrace.TraceInformation("Received retriable exception {0} " +
                             "sending the request to {1}, will reresolve the address " +
                              "send time UTC: {2}",
                            exception,
                            physicalAddress,
                            sendTimeUtc);

                        GoneException goneException = new GoneException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                RMResources.Gone),
                            exception,
                            null,
                            physicalAddress.ToString());

                        throw goneException;
                    }
                    else if (request.IsReadOnlyRequest)
                    {
                        DefaultTrace.TraceInformation("Received exception {0} on readonly request" +
                            "sending the request to {1}, will reresolve the address " +
                             "send time UTC: {2}",
                           exception,
                           physicalAddress,
                           sendTimeUtc);

                        GoneException goneException = new GoneException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                RMResources.Gone),
                            exception,
                            null,
                            physicalAddress.ToString());

                        throw goneException;
                    }
                    else
                    {
                        // We can't throw a GoneException here because it will cause retry and we don't
                        // know if the request failed before or after the message got sent to the server.
                        // So in order to avoid duplicating the request we will not retry.
                        // TODO: a possible solution for this is to add the ability to send a request to the server
                        // to check if the previous request was received or not and act accordingly.
                        ServiceUnavailableException serviceUnavailableException = new ServiceUnavailableException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                RMResources.ServiceUnavailable),
                            exception,
                            null,
                            physicalAddress);
                        serviceUnavailableException.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                        serviceUnavailableException.Headers.Add(HttpConstants.HttpHeaders.WriteRequestTriggerAddressRefresh, "1");
                        throw serviceUnavailableException;
                    }
                }
                finally
                {
                    DateTime receivedTimeUtc = DateTime.UtcNow;
                    double durationInMilliSeconds = (receivedTimeUtc - sendTimeUtc).TotalMilliseconds;

                    this.AfterRequest(
                        activityId,
                        responseMessage != null ? responseMessage.StatusCode : 0,
                        durationInMilliSeconds,
                        responseMessage != null ? responseMessage.Headers : null);
                }

                using (responseMessage)
                {
                    return await HttpTransportClient.ProcessHttpResponse(request.ResourceAddress, activityId.ToString(), responseMessage, physicalAddress, request);
                }
            }
        }

        private static void AddHeader(HttpRequestHeaders requestHeaders, string headerName, DocumentServiceRequest request)
        {
            string headerValue = request.Headers[headerName];
            if (!string.IsNullOrEmpty(headerValue))
            {
                requestHeaders.Add(headerName, headerValue);
            }
        }

        private static void AddHeader(HttpContentHeaders requestHeaders, string headerName, DocumentServiceRequest request)
        {
            string headerValue = request.Headers[headerName];
            if (!string.IsNullOrEmpty(headerValue))
            {
                requestHeaders.Add(headerName, headerValue);
            }
        }

        private static void AddHeader(HttpRequestHeaders requestHeaders, string headerName, string headerValue)
        {
            if (!string.IsNullOrEmpty(headerValue))
            {
                requestHeaders.Add(headerName, headerValue);
            }
        }

        private string GetMatch(DocumentServiceRequest request, ResourceOperation resourceOperation)
        {
            switch (resourceOperation.operationType)
            {
                case OperationType.Delete:
                case OperationType.ExecuteJavaScript:
                case OperationType.Replace:
                case OperationType.Patch:
                case OperationType.Upsert:
                    return request.Headers[HttpConstants.HttpHeaders.IfMatch];

                case OperationType.Read:
                case OperationType.ReadFeed:
                    return request.Headers[HttpConstants.HttpHeaders.IfNoneMatch];

                default:
                    return null;
            }
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000: DisposeObjectsBeforeLosingScope", Justification = "Disposable object returned by method")]
        private HttpRequestMessage PrepareHttpMessage(
            Guid activityId,
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            DocumentServiceRequest request)
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage();

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.Version, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.UserAgent, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PageSize, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PreTriggerInclude, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PreTriggerExclude, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PostTriggerInclude, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PostTriggerExclude, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.Authorization, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IndexingDirective, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.MigrateCollectionDirective, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.ConsistencyLevel, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.SessionToken, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.Prefer, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.ResourceTokenExpiry, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.EnableScanInQuery, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.CanCharge, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.CanThrottle, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.EnableLogging, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsReadOnlyScript, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.ContentSerializationFormat, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.Continuation, request.Continuation);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.ActivityId, activityId.ToString());
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PartitionKey, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PartitionKeyRangeId, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.EnableCrossPartitionQuery, request);

            string dateHeader = Helpers.GetDateHeader(request.Headers);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.XDate, dateHeader);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpTransportClient.Match, this.GetMatch(request, resourceOperation));
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IfModifiedSince, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.A_IM, request);
            if (!request.IsNameBased)
            {
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.ResourceId, request.ResourceId);
            }
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.EntityId, request.EntityId);

            string fanoutRequestHeader = request.Headers[WFConstants.BackendHeaders.IsFanoutRequest];
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.IsFanoutRequest, fanoutRequestHeader);

            if (request.ResourceType == ResourceType.Collection)
            {
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.CollectionPartitionIndex, request.Headers[WFConstants.BackendHeaders.CollectionPartitionIndex]);
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.CollectionServiceIndex, request.Headers[WFConstants.BackendHeaders.CollectionServiceIndex]);
            }

            if (request.Headers[WFConstants.BackendHeaders.BindReplicaDirective] != null)
            {
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.BindReplicaDirective, request.Headers[WFConstants.BackendHeaders.BindReplicaDirective]);
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.PrimaryMasterKey, request.Headers[WFConstants.BackendHeaders.PrimaryMasterKey]);
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.SecondaryMasterKey, request.Headers[WFConstants.BackendHeaders.SecondaryMasterKey]);
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.PrimaryReadonlyKey, request.Headers[WFConstants.BackendHeaders.PrimaryReadonlyKey]);
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.SecondaryReadonlyKey, request.Headers[WFConstants.BackendHeaders.SecondaryReadonlyKey]);
            }

            if (request.Headers[HttpConstants.HttpHeaders.CanOfferReplaceComplete] != null)
            {
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.CanOfferReplaceComplete, request.Headers[HttpConstants.HttpHeaders.CanOfferReplaceComplete]);
            }

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsAutoScaleRequest, request);

            //Query
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsQuery, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.Query, request);

            // Upsert
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsUpsert, request);

            // SupportSpatialLegacyCoordinates
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.PartitionCount, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.CollectionRid, request);

            // Filter by schema
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.FilterBySchemaResourceId, request);

            // UsePolygonsSmallerThanAHemisphere
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.GatewaySignature, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PopulateQuotaInfo, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PopulateQueryMetrics, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.ForceQueryScan, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.RemoteStorageType, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.ShareThroughput, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PopulatePartitionStatistics, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.ClientRetryAttemptCount, request);

            // target lsn for head requests.
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.TargetLsn, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.FederationIdForAuth, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.ExcludeSystemProperties, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.FanoutOperationState, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.AllowTentativeWrites, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IncludeTentativeWrites, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.PreserveFullContent, request);

            // Max polling interval for change feed.
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, request);

            if (resourceOperation.operationType == OperationType.Batch)
            {
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsBatchRequest, request);
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.ShouldBatchContinueOnError, request);
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsBatchOrdered, request);
                HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsBatchAtomic, request);
            }

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, WFConstants.BackendHeaders.ForceSideBySideIndexMigration, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsClientEncrypted, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.MigrateOfferToAutopilot, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, request);
            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.TruncateMergeLogRequest, request);

            HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.AllowRequestWithoutInstanceId, request);

            Stream clonedStream = null;
            if (request.Body != null)
            {
                clonedStream = request.CloneableBody.Clone();
            }

            // The StreamContent created below will own and dispose its underlying stream, but we may need to reuse the stream on the
            // DocumentServiceRequest for future requests. Hence we need to clone without incurring copy cost, so that when
            // HttpRequestMessage -> StreamContent -> MemoryStream all get disposed, the original stream will be left open.
            switch (resourceOperation.operationType)
            {
                case OperationType.Create:
                case OperationType.Batch:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceFeedUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    break;

                case OperationType.ExecuteJavaScript:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    break;

                case OperationType.Delete:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Delete;
                    break;

                case OperationType.Read:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Get;
                    break;

                case OperationType.ReadFeed:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceFeedUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Get;
                    break;

                case OperationType.Replace:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Put;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    break;

                case OperationType.Patch:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = new HttpMethod("PATCH");
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    break;

                case OperationType.QueryPlan:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    HttpTransportClient.AddHeader(httpRequestMessage.Content.Headers, HttpConstants.HttpHeaders.ContentType, request);
                    break;

                case OperationType.Query:
                case OperationType.SqlQuery:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceFeedUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    HttpTransportClient.AddHeader(httpRequestMessage.Content.Headers, HttpConstants.HttpHeaders.ContentType, request);
                    break;

                case OperationType.Upsert:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceFeedUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    break;

                case OperationType.Head:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Head;
                    break;

                case OperationType.HeadFeed:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetResourceFeedUri(resourceOperation.resourceType, physicalAddress, request);
                    httpRequestMessage.Method = HttpMethod.Head;
                    break;

#if !COSMOSCLIENT
                // control operations
                case OperationType.Pause:
                case OperationType.Recycle:
                case OperationType.Resume:
                case OperationType.Stop:
                case OperationType.Crash:
                case OperationType.ForceConfigRefresh:
                case OperationType.MasterInitiatedProgressCoordination:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetRootOperationUri(physicalAddress, resourceOperation.operationType);
                    httpRequestMessage.Method = HttpMethod.Post;
                    break;

                case OperationType.ServiceReservation:
                    httpRequestMessage.RequestUri = physicalAddress;
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    break;

                case OperationType.GetDatabaseAccountConfigurations:
                    HttpTransportClient.AddHeader(httpRequestMessage.Headers, HttpConstants.HttpHeaders.RequestHopCount, request);
                    httpRequestMessage.RequestUri = physicalAddress;
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    break;

                case OperationType.ReadReplicaFromMasterPartition:
                    httpRequestMessage.RequestUri = physicalAddress;
                    httpRequestMessage.Method = HttpMethod.Get;
                    break;

                case OperationType.GetStorageAccountKey:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetRootOperationUri(physicalAddress, resourceOperation.operationType);
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);

                    break;

                case OperationType.ReportThroughputUtilization:
                case OperationType.BatchReportThroughputUtilization:
                case OperationType.ControllerBatchReportCharges:
                case OperationType.ControllerBatchGetOutput:
                    httpRequestMessage.RequestUri = HttpTransportClient.GetRootOperationUri(physicalAddress, resourceOperation.operationType);
                    httpRequestMessage.Method = HttpMethod.Post;
                    Debug.Assert(clonedStream != null);
                    httpRequestMessage.Content = new StreamContent(clonedStream);
                    break;
#endif

                default:
                    DefaultTrace.TraceError("Operation type {0} not found", resourceOperation.operationType);
                    Debug.Assert(false, "Unsupported operation type");
                    throw new NotFoundException();
            }

            return httpRequestMessage;
        }

        internal static Uri GetResourceFeedUri(ResourceType resourceType, Uri physicalAddress, DocumentServiceRequest request)
        {
            switch (resourceType)
            {
                case ResourceType.Attachment:
                    return HttpTransportClient.GetAttachmentFeedUri(physicalAddress, request);
                case ResourceType.Collection:
                    return HttpTransportClient.GetCollectionFeedUri(physicalAddress, request);
                case ResourceType.Conflict:
                    return HttpTransportClient.GetConflictFeedUri(physicalAddress, request);
                case ResourceType.Database:
                    return HttpTransportClient.GetDatabaseFeedUri(physicalAddress);
                case ResourceType.Document:
                    return HttpTransportClient.GetDocumentFeedUri(physicalAddress, request);
                case ResourceType.Permission:
                    return HttpTransportClient.GetPermissionFeedUri(physicalAddress, request);
                case ResourceType.StoredProcedure:
                    return HttpTransportClient.GetStoredProcedureFeedUri(physicalAddress, request);
                case ResourceType.Trigger:
                    return HttpTransportClient.GetTriggerFeedUri(physicalAddress, request);
                case ResourceType.User:
                    return HttpTransportClient.GetUserFeedUri(physicalAddress, request);
                case ResourceType.ClientEncryptionKey:
                    return HttpTransportClient.GetClientEncryptionKeyFeedUri(physicalAddress, request);
                case ResourceType.UserDefinedType:
                    return HttpTransportClient.GetUserDefinedTypeFeedUri(physicalAddress, request);
                case ResourceType.UserDefinedFunction:
                    return HttpTransportClient.GetUserDefinedFunctionFeedUri(physicalAddress, request);
                case ResourceType.Schema:
                    return HttpTransportClient.GetSchemaFeedUri(physicalAddress, request);
                case ResourceType.Offer:
                    return HttpTransportClient.GetOfferFeedUri(physicalAddress, request);
                case ResourceType.Snapshot:
                    return HttpTransportClient.GetSnapshotFeedUri(physicalAddress, request);
                case ResourceType.RoleDefinition:
                    return HttpTransportClient.GetRoleDefinitionFeedUri(physicalAddress, request);
                case ResourceType.RoleAssignment:
                    return HttpTransportClient.GetRoleAssignmentFeedUri(physicalAddress, request);
#if !COSMOSCLIENT
                case ResourceType.Module:
                case ResourceType.ModuleCommand:
                case ResourceType.Record:
                case ResourceType.Replica:
                    Debug.Assert(false, "Unexpected resource type: " + resourceType);
                    throw new NotFoundException();
                case ResourceType.ServiceFabricService:
                    return physicalAddress;
#endif

                default:
                    Debug.Assert(false, "Unexpected resource type: " + resourceType);
                    throw new NotFoundException();
            }
        }

        internal static Uri GetResourceEntryUri(ResourceType resourceType, Uri physicalAddress, DocumentServiceRequest request)
        {
            switch (resourceType)
            {
                case ResourceType.Attachment:
                    return HttpTransportClient.GetAttachmentEntryUri(physicalAddress, request);
                case ResourceType.Collection:
                    return HttpTransportClient.GetCollectionEntryUri(physicalAddress, request);
                case ResourceType.Conflict:
                    return HttpTransportClient.GetConflictEntryUri(physicalAddress, request);
                case ResourceType.Database:
                    return HttpTransportClient.GetDatabaseEntryUri(physicalAddress, request);
                case ResourceType.Document:
                    return HttpTransportClient.GetDocumentEntryUri(physicalAddress, request);
                case ResourceType.Permission:
                    return HttpTransportClient.GetPermissionEntryUri(physicalAddress, request);
                case ResourceType.StoredProcedure:
                    return HttpTransportClient.GetStoredProcedureEntryUri(physicalAddress, request);
                case ResourceType.Trigger:
                    return HttpTransportClient.GetTriggerEntryUri(physicalAddress, request);
                case ResourceType.User:
                    return HttpTransportClient.GetUserEntryUri(physicalAddress, request);
                case ResourceType.ClientEncryptionKey:
                    return HttpTransportClient.GetClientEncryptionKeyEntryUri(physicalAddress, request);
                case ResourceType.UserDefinedType:
                    return HttpTransportClient.GetUserDefinedTypeEntryUri(physicalAddress, request);
                case ResourceType.UserDefinedFunction:
                    return HttpTransportClient.GetUserDefinedFunctionEntryUri(physicalAddress, request);
                case ResourceType.Schema:
                    return HttpTransportClient.GetSchemaEntryUri(physicalAddress, request);
                case ResourceType.Offer:
                    return HttpTransportClient.GetOfferEntryUri(physicalAddress, request);
                case ResourceType.Snapshot:
                    return HttpTransportClient.GetSnapshotEntryUri(physicalAddress, request);
                case ResourceType.RoleDefinition:
                    return HttpTransportClient.GetRoleDefinitionEntryUri(physicalAddress, request);
                case ResourceType.RoleAssignment:
                    return HttpTransportClient.GetRoleAssignmentEntryUri(physicalAddress, request);
#if !COSMOSCLIENT
                case ResourceType.Replica:
                    return HttpTransportClient.GetRootFeedUri(physicalAddress);

                case ResourceType.Module:
                case ResourceType.ModuleCommand:
#endif
                case ResourceType.Record:

                    Debug.Assert(false, "Unexpected resource type: " + resourceType);
                    throw new NotFoundException();

                default:
                    Debug.Assert(false, "Unexpected resource type: " + resourceType);
                    throw new NotFoundException();
            }
        }

        private static Uri GetRootFeedUri(Uri baseAddress)
        {
            return baseAddress;
        }

        private static Uri GetRootOperationUri(Uri baseAddress, OperationType operationType)
        {
            return new Uri(baseAddress, PathsHelper.GenerateRootOperationPath(operationType));
        }

        private static Uri GetDatabaseFeedUri(Uri baseAddress)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Database, string.Empty, true));
        }

        private static Uri GetDatabaseEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Database, request, false));
        }

        private static Uri GetCollectionFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Collection, request, true));
        }

        private static Uri GetStoredProcedureFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.StoredProcedure, request, true));
        }

        private static Uri GetTriggerFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Trigger, request, true));
        }

        private static Uri GetUserDefinedFunctionFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.UserDefinedFunction, request, true));
        }

        private static Uri GetCollectionEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Collection, request, false));
        }

        private static Uri GetStoredProcedureEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.StoredProcedure, request, false));
        }

        private static Uri GetTriggerEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Trigger, request, false));
        }

        private static Uri GetUserDefinedFunctionEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.UserDefinedFunction, request, false));
        }

        private static Uri GetDocumentFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Document, request, true));
        }

        private static Uri GetDocumentEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Document, request, false));
        }

        private static Uri GetConflictFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Conflict, request, true));
        }

        private static Uri GetConflictEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Conflict, request, false));
        }

        private static Uri GetAttachmentFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Attachment, request, true));
        }

        private static Uri GetAttachmentEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Attachment, request, false));
        }

        private static Uri GetUserFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.User, request, true));
        }

        private static Uri GetUserEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.User, request, false));
        }

        private static Uri GetClientEncryptionKeyFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.ClientEncryptionKey, request, true));
        }

        private static Uri GetClientEncryptionKeyEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.ClientEncryptionKey, request, false));
        }

        private static Uri GetUserDefinedTypeFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.UserDefinedType, request, true));
        }

        private static Uri GetUserDefinedTypeEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.UserDefinedType, request, false));
        }

        private static Uri GetPermissionFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Permission, request, true));
        }

        private static Uri GetPermissionEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Permission, request, false));
        }

        private static Uri GetOfferFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Offer, request, true));
        }

        private static Uri GetOfferEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Offer, request, false));
        }

        private static Uri GetSchemaFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Schema, request, true));
        }

        private static Uri GetSchemaEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Schema, request, false));
        }

        private static Uri GetSnapshotFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Snapshot, request, true));
        }

        private static Uri GetSnapshotEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.Snapshot, request, false));
        }

        private static Uri GetRoleDefinitionFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.RoleDefinition, request, isFeed: true));
        }

        private static Uri GetRoleDefinitionEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.RoleDefinition, request, isFeed: false));
        }

        private static Uri GetRoleAssignmentFeedUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.RoleAssignment, request, isFeed: true));
        }

        private static Uri GetRoleAssignmentEntryUri(Uri baseAddress, DocumentServiceRequest request)
        {
            return new Uri(baseAddress, PathsHelper.GeneratePath(ResourceType.RoleAssignment, request, isFeed: false));
        }

        public static Task<StoreResponse> ProcessHttpResponse(string resourceAddress, string activityId, HttpResponseMessage response, Uri physicalAddress, DocumentServiceRequest request)
        {
            if (response == null)
            {
                InternalServerErrorException exception =
                    new InternalServerErrorException(
                    string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.InvalidBackendResponse),
                        physicalAddress);
                exception.Headers.Set(HttpConstants.HttpHeaders.ActivityId,
                    activityId);
                exception.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
                throw exception;
            }

            // If the status code is < 300 or 304 NotModified (we treat not modified as success) then it means that it's a success code and shouldn't throw.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
            {
                return HttpTransportClient.CreateStoreResponseFromHttpResponse(response);
            }
            else
            {
                return HttpTransportClient.CreateErrorResponseFromHttpResponse(resourceAddress, activityId, response, request);
            }
        }

        private static async Task<StoreResponse> CreateErrorResponseFromHttpResponse(
            string resourceAddress,
            string activityId,
            HttpResponseMessage response,
            DocumentServiceRequest request)
        {
            using (response)
            {
                HttpStatusCode statusCode = response.StatusCode;

                string errorMessage = await TransportClient.GetErrorResponseAsync(response);
                long responseLSN = -1;
                IEnumerable<string> lsnValues;
                if (response.Headers.TryGetValues(WFConstants.BackendHeaders.LSN, out lsnValues))
                {
                    string temp = lsnValues.FirstOrDefault();
                    long.TryParse(temp, NumberStyles.Integer, CultureInfo.InvariantCulture, out responseLSN);
                }

                string responsePartitionKeyRangeId = null;
                IEnumerable<string> partitionKeyRangeIdValues;
                if (response.Headers.TryGetValues(WFConstants.BackendHeaders.PartitionKeyRangeId, out partitionKeyRangeIdValues))
                {
                    responsePartitionKeyRangeId = partitionKeyRangeIdValues.FirstOrDefault();
                }

                DocumentClientException exception;

                switch (statusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        exception = new UnauthorizedException(
                                string.Format(
                                    CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    string.IsNullOrEmpty(errorMessage) ? RMResources.Unauthorized : errorMessage),
                                response.Headers,
                                response.RequestMessage.RequestUri);
                        break;

                    case HttpStatusCode.Forbidden:
                        exception = new ForbiddenException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                string.IsNullOrEmpty(errorMessage) ? RMResources.Forbidden : errorMessage),
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;

                    case HttpStatusCode.NotFound:
                        // HTTP.SYS returns NotFound (404) if the URI
                        // is not registered. This is really an indication that
                        // the replica which registered the URI is not
                        // available at the server. We detect this case by
                        // the presence of Content-Type header in the response
                        // and map it to HTTP Gone (410), which is the more
                        // appropriate response for this case.
                        if (response.Content != null && response.Content.Headers != null && response.Content.Headers.ContentType != null &&
                            !string.IsNullOrEmpty(response.Content.Headers.ContentType.MediaType) &&
                            response.Content.Headers.ContentType.MediaType.StartsWith(RuntimeConstants.MediaTypes.TextHtml, StringComparison.OrdinalIgnoreCase))
                        {
                            // Have the request URL in the exception message for debugging purposes.
                            exception = new GoneException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    RMResources.Gone),
                                response.RequestMessage.RequestUri)
                            {
                                LSN = responseLSN,
                                PartitionKeyRangeId = responsePartitionKeyRangeId
                            };
                            exception.Headers.Set(HttpConstants.HttpHeaders.ActivityId,
                                activityId);

                            break;
                        }
                        else
                        {
                            if (request.IsValidStatusCodeForExceptionlessRetry((int)statusCode))
                            {
                                return await HttpTransportClient.CreateStoreResponseFromHttpResponse(response, includeContent: false);
                            }

                            exception = new NotFoundException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    string.IsNullOrEmpty(errorMessage) ? RMResources.NotFound : errorMessage),
                                response.Headers,
                                response.RequestMessage.RequestUri);
                            break;
                        }

                    case HttpStatusCode.BadRequest:
                        exception = new BadRequestException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    string.IsNullOrEmpty(errorMessage) ? RMResources.BadRequest : errorMessage),
                                response.Headers,
                                response.RequestMessage.RequestUri);
                        break;

                    case HttpStatusCode.MethodNotAllowed:
                        exception = new MethodNotAllowedException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                string.IsNullOrEmpty(errorMessage) ? RMResources.MethodNotAllowed : errorMessage),
                            null,
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;

                    case HttpStatusCode.Gone:
                        {
#if NETFX
                            if (PerfCounters.Counters.RoutingFailures != null)
                            {
                                PerfCounters.Counters.RoutingFailures.Increment();
                            }
#endif
                            TransportClient.LogGoneException(response.RequestMessage.RequestUri, activityId);

                            uint nSubStatus = 0;
                            IEnumerable<string> valueSubStatus = null;

                            try
                            {
                                valueSubStatus = response.Headers.GetValues(WFConstants.BackendHeaders.SubStatus);
                                if (valueSubStatus != null && valueSubStatus.Any())
                                {
                                    if (!uint.TryParse(valueSubStatus.First(), NumberStyles.Integer, CultureInfo.InvariantCulture, out nSubStatus))
                                    {
                                        exception = new InternalServerErrorException(
                                            string.Format(CultureInfo.CurrentUICulture,
                                                RMResources.ExceptionMessage,
                                                RMResources.InvalidBackendResponse),
                                            response.Headers,
                                            response.RequestMessage.RequestUri);
                                        break;
                                    }
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                DefaultTrace.TraceInformation("SubStatus doesn't exist in the header");
                            }

                            if ((SubStatusCodes)nSubStatus == SubStatusCodes.NameCacheIsStale)
                            {
                                exception = new InvalidPartitionException(
                                    string.Format(CultureInfo.CurrentUICulture,
                                        RMResources.ExceptionMessage,
                                        string.IsNullOrEmpty(errorMessage) ? RMResources.Gone : errorMessage),
                                    response.Headers,
                                    response.RequestMessage.RequestUri);
                                break;
                            }
                            else if ((SubStatusCodes)nSubStatus == SubStatusCodes.PartitionKeyRangeGone)
                            {
                                exception = new PartitionKeyRangeGoneException(
                                    string.Format(CultureInfo.CurrentUICulture,
                                        RMResources.ExceptionMessage,
                                        string.IsNullOrEmpty(errorMessage) ? RMResources.Gone : errorMessage),
                                    response.Headers,
                                    response.RequestMessage.RequestUri);
                                break;
                            }
                            else if ((SubStatusCodes)nSubStatus == SubStatusCodes.CompletingSplit)
                            {
                                exception = new PartitionKeyRangeIsSplittingException(
                                    string.Format(CultureInfo.CurrentUICulture,
                                        RMResources.ExceptionMessage,
                                        string.IsNullOrEmpty(errorMessage) ? RMResources.Gone : errorMessage),
                                    response.Headers,
                                    response.RequestMessage.RequestUri);
                                break;
                            }
                            else if ((SubStatusCodes)nSubStatus == SubStatusCodes.CompletingPartitionMigration)
                            {
                                exception = new PartitionIsMigratingException(
                                    string.Format(CultureInfo.CurrentUICulture,
                                        RMResources.ExceptionMessage,
                                        string.IsNullOrEmpty(errorMessage) ? RMResources.Gone : errorMessage),
                                    response.Headers,
                                    response.RequestMessage.RequestUri);
                                break;
                            }
                            else
                            {
                                // Have the request URL in the exception message for debugging purposes.
                                exception = new GoneException(
                                    string.Format(CultureInfo.CurrentUICulture,
                                            RMResources.ExceptionMessage,
                                            RMResources.Gone),
                                        response.Headers,
                                        response.RequestMessage.RequestUri);

                                exception.Headers.Set(HttpConstants.HttpHeaders.ActivityId,
                                    activityId);
                                break;
                            }
                        }

                    case HttpStatusCode.Conflict:
                        {
                            if (request.IsValidStatusCodeForExceptionlessRetry((int)statusCode))
                            {
                                return await HttpTransportClient.CreateStoreResponseFromHttpResponse(response, includeContent: false);
                            }

                            exception = new ConflictException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    string.IsNullOrEmpty(errorMessage) ? RMResources.EntityAlreadyExists : errorMessage),
                                response.Headers,
                                response.RequestMessage.RequestUri);
                            break;
                        }

                    case HttpStatusCode.PreconditionFailed:
                        {
                            if (request.IsValidStatusCodeForExceptionlessRetry((int)statusCode))
                            {
                                return await HttpTransportClient.CreateStoreResponseFromHttpResponse(response, includeContent: false);
                            }

                            exception = new PreconditionFailedException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    string.IsNullOrEmpty(errorMessage) ? RMResources.PreconditionFailed : errorMessage),
                                response.Headers,
                                response.RequestMessage.RequestUri);
                            break;
                        }

                    case HttpStatusCode.RequestEntityTooLarge:
                        exception = new RequestEntityTooLargeException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.RequestEntityTooLarge,
                                    HttpConstants.HttpHeaders.PageSize)),
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;

                    case (HttpStatusCode)423:
                        exception = new LockedException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                string.IsNullOrEmpty(errorMessage) ? RMResources.Locked : errorMessage),
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;

                    case HttpStatusCode.ServiceUnavailable:
                        exception = new ServiceUnavailableException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                string.IsNullOrEmpty(errorMessage) ? RMResources.ServiceUnavailable : errorMessage),
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;

                    case HttpStatusCode.RequestTimeout:
                        exception = new RequestTimeoutException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                string.IsNullOrEmpty(errorMessage) ? RMResources.RequestTimeout : errorMessage),
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;

                    case (HttpStatusCode)StatusCodes.RetryWith:
                        exception = new RetryWithException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                string.IsNullOrEmpty(errorMessage) ? RMResources.RetryWith : errorMessage),
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;

                    case (HttpStatusCode)StatusCodes.TooManyRequests:
                        if (request.IsValidStatusCodeForExceptionlessRetry((int)statusCode))
                        {
                            return await HttpTransportClient.CreateStoreResponseFromHttpResponse(response, includeContent: false);
                        }

                        exception =
                            new RequestRateTooLargeException(
                                string.Format(CultureInfo.CurrentUICulture,
                                    RMResources.ExceptionMessage,
                                    string.IsNullOrEmpty(errorMessage) ? RMResources.TooManyRequests : errorMessage),
                                response.Headers,
                                response.RequestMessage.RequestUri);

                        IEnumerable<string> values = null;
                        try
                        {
                            values = response.Headers.GetValues(HttpConstants.HttpHeaders.RetryAfterInMilliseconds);
                        }
                        catch (InvalidOperationException)
                        {
                            DefaultTrace.TraceWarning("RequestRateTooLargeException being thrown without RetryAfter.");
                        }

                        if (values != null && values.Any())
                        {
                            exception.Headers.Set(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, values.First());
                        }

                        break;

                    case HttpStatusCode.InternalServerError:
                        exception = new InternalServerErrorException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                string.IsNullOrEmpty(errorMessage) ? RMResources.InternalServerError : errorMessage),
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;

                    default:
                        DefaultTrace.TraceCritical("Unrecognized status code {0} returned by backend. ActivityId {1}", statusCode, activityId);
                        TransportClient.LogException(response.RequestMessage.RequestUri, activityId);
                        exception = new InternalServerErrorException(
                            string.Format(CultureInfo.CurrentUICulture,
                                RMResources.ExceptionMessage,
                                RMResources.InvalidBackendResponse),
                            response.Headers,
                            response.RequestMessage.RequestUri);
                        break;
                }

                exception.LSN = responseLSN;
                exception.PartitionKeyRangeId = responsePartitionKeyRangeId;
                exception.ResourceAddress = resourceAddress;
                throw exception;
            }
        }

        internal static string GetHeader(string[] names, string[] values, string name)
        {
            for (int idx = 0; idx < names.Length; idx++)
            {
                if (string.Equals(names[idx], name, StringComparison.Ordinal))
                {
                    return values[idx];
                }
            }

            return null;
        }

        public async static Task<StoreResponse> CreateStoreResponseFromHttpResponse(HttpResponseMessage responseMessage, bool includeContent = true)
        {
            StoreResponse response = new StoreResponse()
            {
                Headers = new DictionaryNameValueCollection(StringComparer.OrdinalIgnoreCase)
            };

            using (responseMessage)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> kvPair in responseMessage.Headers)
                {
                    if (string.Compare(kvPair.Key, HttpConstants.HttpHeaders.OwnerFullName, StringComparison.Ordinal) == 0)
                    {
                        response.Headers[kvPair.Key] = Uri.UnescapeDataString(kvPair.Value.SingleOrDefault());
                    }
                    else
                    {
                        response.Headers[kvPair.Key] = kvPair.Value.SingleOrDefault();
                    }
                }

                response.Status = (int)responseMessage.StatusCode;

                if (includeContent && responseMessage.Content != null)
                {
                    Stream bufferredStream = new MemoryStream();

                    await responseMessage.Content.CopyToAsync(bufferredStream);
                    bufferredStream.Position = 0;

                    response.ResponseBody = bufferredStream;
                }
                return response;
            }
        }
    }
}
