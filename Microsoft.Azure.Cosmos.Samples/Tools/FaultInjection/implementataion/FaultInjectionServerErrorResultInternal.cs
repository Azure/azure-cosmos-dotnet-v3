//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Fault Injection Server Error Result.
    /// </summary>
    internal class FaultInjectionServerErrorResultInternal
    {
        private readonly FaultInjectionServerErrorType serverErrorType;
        private readonly int times;
        private readonly TimeSpan delay;
        private readonly bool suppressServiceRequest;

        /// <summary>
        /// Constructor for FaultInjectionServerErrorResultInternal
        /// </summary>
        /// <param name="serverErrorType"></param>
        /// <param name="times"></param>
        /// <param name="delay"></param>
        public FaultInjectionServerErrorResultInternal(
            FaultInjectionServerErrorType serverErrorType, 
            int times, 
            TimeSpan delay, 
            bool suppressServiceRequest)
        {
            this.serverErrorType = serverErrorType;
            this.times = times;
            this.delay = delay;
            this.suppressServiceRequest = suppressServiceRequest;
        }

        /// <summary>
        /// Returns Server Error Type
        /// </summary>
        /// <returns>the <see cref="FaultInjectionServerErrorType"/></returns>
        public FaultInjectionServerErrorType GetServerErrorType()
        {
            return this.serverErrorType;
        }

        /// <summary>
        /// Gets the number of times a rule can be applied on a single operation.
        /// </summary>
        /// <returns>An int representing the number of times a rule can be applied.</returns>
        public int GetTimes()
        {
            return this.times;
        }

        /// <summary>
        /// Gets the injected delay for the server error.
        /// Required for RESPONSE_DELAY and CONNECTION_DELAY error types. 
        /// </summary>
        /// <returns>A TimeSpan represeting the lenght of the delay.</returns>
        public TimeSpan GetDelay()
        {
            return this.delay;
        }

        /// <summary>
        /// Return whether or not the service request should be suppressed.
        /// </summary>
        /// <returns></returns>
        public bool GetSuppressServiceRequest()
        {
            return this.suppressServiceRequest;
        }

        /// <summary>
        /// Determins if the rule can be applied.
        /// </summary>
        /// <param name="ruleId"></param>
        /// <param name="args"></param>
        /// <returns>if the rule can be applied.</returns>
        public bool IsApplicable(string ruleId, ChannelCallArguments args)
        {
            return args.FaultInjectionRequestContext.GetFaultInjectionRuleHitCount(ruleId) > this.times;
        }

        /// <summary>
        /// Determins if the rule can be applied for connection delay
        /// </summary>
        /// <param name="ruleId"></param>
        /// <param name="requestContext"></param>
        /// <returns>if the rule can be applied.</returns>
        public bool IsApplicable(string ruleId, DocumentServiceRequest request)
        {
            return request.FaultInjectionRequestContext.GetFaultInjectionRuleHitCount(ruleId) > this.times;
        }

        /// <summary>
        /// Set the injected server error.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="transportRequestStats"></param>
        public void SetInjectedServerError(ChannelCallArguments args, TransportRequestStats transportRequestStats)
        {
            StoreResponse storeResponse;
            transportRequestStats.FaultInjectionServerErrorType = this.serverErrorType;

            switch (this.serverErrorType)
            {
                case FaultInjectionServerErrorType.GONE:
                    storeResponse = new StoreResponse()
                    {
                        Status = 410,
                        Headers = args.RequestHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("Fault Injection Server Error: Gone"))
                    };                    
                    transportRequestStats.FaultInjectionStoreResponse = storeResponse;


                    break;

                case FaultInjectionServerErrorType.RETRY_WITH:
                    storeResponse = new StoreResponse()
                    {
                        Status = 449,
                        Headers = args.RequestHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("Fault Injection Server Error: Retry With"))
                    };
                    transportRequestStats.FaultInjectionStoreResponse = storeResponse;

                    break;

                case FaultInjectionServerErrorType.TOO_MANY_REQUESTS:
                    INameValueCollection tooManyRequestsHeaders = args.RequestHeaders;
                    tooManyRequestsHeaders.Add(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, "500");
                    storeResponse = new StoreResponse()
                    {
                        Status = 429,
                        Headers = tooManyRequestsHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("Fault Injection Server Error: Too Many Requests"))
                    };
                    transportRequestStats.FaultInjectionStoreResponse = storeResponse;

                    break;

                case FaultInjectionServerErrorType.TIMEOUT:
                    TransportException transportException = new TransportException(
                        TransportErrorCode.RequestTimeout,
                        new TimeoutException("Fault Injection Server Error: Timeout"),
                        args.CommonArguments.ActivityId,
                        args.PreparedCall.Uri,
                        "Fault Injection Server Error: Timeout",
                        args.CommonArguments.UserPayload,
                        args.CommonArguments.PayloadSent);
                    transportRequestStats.FaultInjectionException = transportException;   

                    break;

                case FaultInjectionServerErrorType.INTERNAL_SERVER_ERROR:
                    storeResponse = new StoreResponse()
                    {
                        Status = 500,
                        Headers = args.RequestHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("Fault Injection Server Error: Internal Server Error"))
                    };
                    transportRequestStats.FaultInjectionStoreResponse = storeResponse;

                    break;

                case FaultInjectionServerErrorType.READ_SESSION_NOT_AVAILABLE:
                    INameValueCollection readSessionHeaders = args.RequestHeaders;
                    readSessionHeaders.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture));
                    storeResponse = new StoreResponse()
                    {
                        Status = 404,
                        Headers = readSessionHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("Fault Injection Server Error: Read Session Not Available"))
                    };
                    transportRequestStats.FaultInjectionStoreResponse = storeResponse;

                    break;

                case FaultInjectionServerErrorType.PARTITION_IS_MIGRATING:
                    INameValueCollection partitionMigrationHeaders = args.RequestHeaders;
                    partitionMigrationHeaders.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.CompletingPartitionMigration).ToString(CultureInfo.InvariantCulture));
                    storeResponse = new StoreResponse()
                    {
                        Status = 404,
                        Headers = partitionMigrationHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("Fault Injection Server Error: Partition Migrating"))
                    };
                    transportRequestStats.FaultInjectionStoreResponse = storeResponse;

                    break;

                default:
                    throw new ArgumentException($"Server error type {this.serverErrorType} is not supported");
            }
        }
    }
}
