//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Hedging availability strategy. Once threshold time is reached, 
    /// the SDK will send out an additional request to a remote region in parallel
    /// if the first hedging request or the original has not returned after the step time, 
    /// additional hedged requests will be sent out there is a response or all regions are exausted.
    /// </summary>
    internal class CrossRegionHedgingAvailabilityStrategy : AvailabilityStrategyInternal
    {
        private const string HedgeContext = "Hedge Context";
        private const string ResponseRegion = "Response Region";

        /// <summary>
        /// Latency threshold which activates the first region hedging 
        /// </summary>
        public TimeSpan Threshold { get; private set; }

        /// <summary>
        /// When the SDK will send out additional hedging requests after the initial hedging request
        /// </summary>
        public TimeSpan ThresholdStep { get; private set; }

        /// <summary>
        /// Whether hedging for write requests on accounts with multi-region writes is enabled.
        /// Note that this does come with the caveat that there will be more 409 / 412 errors thrown by the SDK.
        /// This is expected and applications that adopt this feature should be prepared to handle these exceptions.
        /// Application might not be able to be deterministic on Create vs Replace in the case of Upsert Operations
        /// </summary>
        public bool EnableMultiWriteRegionHedge { get; private set; }

        /// <summary>
        /// Constructor for hedging availability strategy
        /// </summary>
        /// <param name="threshold"></param>
        /// <param name="thresholdStep"></param>
        /// <param name="enableMultiWriteRegionHedge"></param>
        public CrossRegionHedgingAvailabilityStrategy(
            TimeSpan threshold,
            TimeSpan? thresholdStep,
            bool enableMultiWriteRegionHedge = false)
        {
            if (threshold <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold));
            }

            if (thresholdStep <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(thresholdStep));
            }

            this.Threshold = threshold;
            this.ThresholdStep = thresholdStep ?? TimeSpan.FromMilliseconds(-1);
            this.EnableMultiWriteRegionHedge = enableMultiWriteRegionHedge;
        }

        /// <inheritdoc/>
        internal override bool Enabled()
        {
            return true;
        }

        /// <summary>
        /// This method determines if the request should be sent with a hedging availability strategy.
        /// This availability strategy can only be used if the request is a read-only request on a document request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="client"></param>
        /// <returns>whether the request should be a hedging request.</returns>
        internal bool ShouldHedge(RequestMessage request, CosmosClient client)
        {
            //Only use availability strategy for document point operations
            if (request.ResourceType != ResourceType.Document)
            {
                return false;
            }

            //check to see if it is a not a read-only request/ if multimaster writes are enabled
            if (!OperationTypeExtensions.IsReadOperation(request.OperationType))
            {
                if (this.EnableMultiWriteRegionHedge
                    && client.DocumentClient.GlobalEndpointManager.CanSupportMultipleWriteLocations(request.ResourceType, request.OperationType))
                {
                    return true;
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// Execute the hedging availability strategy
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="client"></param>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The response after executing cross region hedging</returns>
        internal override async Task<ResponseMessage> ExecuteAvailabilityStrategyAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            CosmosClient client,
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!this.ShouldHedge(request, client)
                || client.DocumentClient.GlobalEndpointManager.ReadEndpoints.Count == 1)
            {
                return await sender(request, cancellationToken);
            }
            
            ITrace trace = request.Trace;

            using (CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                using (CloneableStream clonedBody = (CloneableStream)(request.Content == null
                    ? null
                    : await StreamExtension.AsClonableStreamAsync(request.Content)))
                {
                    IReadOnlyCollection<string> hedgeRegions = client.DocumentClient.GlobalEndpointManager
                    .GetApplicableRegions(
                        request.RequestOptions?.ExcludeRegions,
                        OperationTypeExtensions.IsReadOperation(request.OperationType));

                    List<Task> requestTasks = new List<Task>(hedgeRegions.Count + 1);

                    Task<HedgingResponse> primaryRequest = null;
                    HedgingResponse hedgeResponse = null;

                    //Send out hedged requests
                    for (int requestNumber = 0; requestNumber < hedgeRegions.Count; requestNumber++)
                    {
                        TimeSpan awaitTime = requestNumber == 0 ? this.Threshold : this.ThresholdStep;

                        using (CancellationTokenSource timerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            CancellationToken timerToken = timerTokenSource.Token;
                            using (Task hedgeTimer = Task.Delay(awaitTime, timerToken))
                            {
                                if (requestNumber == 0)
                                {
                                    primaryRequest = this.RequestSenderAndResultCheckAsync(
                                        sender,
                                        request,
                                        hedgeRegions.ElementAt(requestNumber),
                                        cancellationToken,
                                        cancellationTokenSource, 
                                        trace);

                                    requestTasks.Add(primaryRequest);
                                }
                                else
                                {
                                    Task<HedgingResponse> requestTask = this.CloneAndSendAsync(
                                    sender: sender,
                                    request: request,
                                    clonedBody: clonedBody,
                                    hedgeRegions: hedgeRegions,
                                    requestNumber: requestNumber,
                                    trace: trace,
                                    cancellationToken: cancellationToken,
                                    cancellationTokenSource: cancellationTokenSource);

                                    requestTasks.Add(requestTask);
                                }

                                requestTasks.Add(hedgeTimer);

                                Task completedTask = await Task.WhenAny(requestTasks);
                                requestTasks.Remove(completedTask);

                                if (completedTask == hedgeTimer)
                                {
                                    continue;
                                }

                                timerTokenSource.Cancel();
                                requestTasks.Remove(hedgeTimer);

                                if (completedTask.IsFaulted)
                                {
                                    AggregateException innerExceptions = completedTask.Exception.Flatten();
                                }

                                hedgeResponse = await (Task<HedgingResponse>)completedTask;
                                if (hedgeResponse.IsNonTransient)
                                {
                                    cancellationTokenSource.Cancel();
                                    //Take is not inclusive, so we need to add 1 to the request number which starts at 0
                                    ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                        HedgeContext,
                                        hedgeRegions.Take(requestNumber + 1));
                                    ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                        ResponseRegion,
                                        hedgeResponse.ResponseRegion);
                                    return hedgeResponse.ResponseMessage;
                                }
                            }
                        }
                    }

                    //Wait for a good response from the hedged requests/primary request
                    Exception lastException = null;
                    while (requestTasks.Any())
                    {
                        Task completedTask = await Task.WhenAny(requestTasks);
                        requestTasks.Remove(completedTask);
                        if (completedTask.IsFaulted)
                        {
                            AggregateException innerExceptions = completedTask.Exception.Flatten();
                            lastException = innerExceptions.InnerExceptions.FirstOrDefault();
                        }

                        hedgeResponse = await (Task<HedgingResponse>)completedTask;
                        if (hedgeResponse.IsNonTransient || requestTasks.Count == 0)
                        {
                            cancellationTokenSource.Cancel();
                            ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                HedgeContext,
                                hedgeRegions);
                            ((CosmosTraceDiagnostics)hedgeResponse.ResponseMessage.Diagnostics).Value.AddOrUpdateDatum(
                                        ResponseRegion,
                                        hedgeResponse.ResponseRegion);
                            return hedgeResponse.ResponseMessage;
                        }
                    }

                    if (lastException != null)
                    {
                        throw lastException;
                    }

                    Debug.Assert(hedgeResponse != null);
                    return hedgeResponse.ResponseMessage;
                }
            }
        }

        private async Task<HedgingResponse> CloneAndSendAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            CloneableStream clonedBody,
            IReadOnlyCollection<string> hedgeRegions,
            int requestNumber,
            ITrace trace,
            CancellationToken cancellationToken,
            CancellationTokenSource cancellationTokenSource)
        {
            RequestMessage clonedRequest;

            using (clonedRequest = request.Clone(
                trace,
                clonedBody))
            {
                clonedRequest.RequestOptions ??= new RequestOptions();

                List<string> excludeRegions = new List<string>(hedgeRegions);
                string region = excludeRegions[requestNumber];
                excludeRegions.RemoveAt(requestNumber);
                clonedRequest.RequestOptions.ExcludeRegions = excludeRegions;

                return await this.RequestSenderAndResultCheckAsync(
                    sender,
                    clonedRequest,
                    region,
                    cancellationToken,
                    cancellationTokenSource, 
                    trace);
            }
        }

        private async Task<HedgingResponse> RequestSenderAndResultCheckAsync(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> sender,
            RequestMessage request,
            string hedgedRegion,
            CancellationToken cancellationToken,
            CancellationTokenSource cancellationTokenSource,
            ITrace trace)
        {
            try
            {
                ResponseMessage response = await sender.Invoke(request, cancellationToken);
                if (IsFinalResult((int)response.StatusCode, (int)response.Headers.SubStatusCode))
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        cancellationTokenSource.Cancel();
                    }

                    return new HedgingResponse(true, response, hedgedRegion);
                }

                return new HedgingResponse(false, response, hedgedRegion);
            }
            catch (OperationCanceledException oce ) when (cancellationTokenSource.IsCancellationRequested)
            {
                throw new CosmosOperationCanceledException(oce, trace);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception thrown while executing cross region hedging availability strategy: {0}", ex);
                throw ex;
            }
        }

        private static bool IsFinalResult(int statusCode, int subStatusCode)
        {
            //All 1xx, 2xx, and 3xx status codes should be treated as final results
            if (statusCode < (int)HttpStatusCode.BadRequest)
            {
                return true;
            }

            //Status codes that indicate non-transient timeouts
            if (statusCode == (int)HttpStatusCode.BadRequest
                || statusCode == (int)HttpStatusCode.Conflict
                || statusCode == (int)HttpStatusCode.MethodNotAllowed
                || statusCode == (int)HttpStatusCode.PreconditionFailed
                || statusCode == (int)HttpStatusCode.RequestEntityTooLarge
                || statusCode == (int)HttpStatusCode.Unauthorized)
            {
                return true;
            }

            //404 - Not found is a final result as the document was not yet available
            //after enforcing the consistency model
            //All other errors should be treated as possibly transient errors
            return statusCode == (int)HttpStatusCode.NotFound && subStatusCode == (int)SubStatusCodes.Unknown;
        }

        private sealed class HedgingResponse
        {
            public readonly bool IsNonTransient;
            public readonly ResponseMessage ResponseMessage;
            public readonly string ResponseRegion;

            public HedgingResponse(bool isNonTransient, ResponseMessage responseMessage, string responseRegion)
            {
                this.IsNonTransient = isNonTransient;
                this.ResponseMessage = responseMessage;
                this.ResponseRegion = responseRegion;
            }
        }
    }
}
