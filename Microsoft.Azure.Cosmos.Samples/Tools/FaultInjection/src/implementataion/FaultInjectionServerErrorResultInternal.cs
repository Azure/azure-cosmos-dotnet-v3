﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
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
        private readonly FaultInjectionApplicationContext applicationContext;

        /// <summary>
        /// Constructor for FaultInjectionServerErrorResultInternal
        /// </summary>
        /// <param name="serverErrorType"></param>
        /// <param name="times"></param>
        /// <param name="delay"></param>
        /// <param name="applicationContext"></param>
        public FaultInjectionServerErrorResultInternal(
            FaultInjectionServerErrorType serverErrorType, 
            int times, 
            TimeSpan delay, 
            bool suppressServiceRequest,
            FaultInjectionApplicationContext applicationContext)
        {
            this.serverErrorType = serverErrorType;
            this.times = times;
            this.delay = delay;
            this.suppressServiceRequest = suppressServiceRequest;
            this.applicationContext = applicationContext;
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
        /// <param name="activityId"></param>
        /// <returns>if the rule can be applied.</returns>
        public bool IsApplicable(string ruleId, Guid activityId)
        {
            bool hasRuleExecution = this.applicationContext.TryGetRuleExecutionsByRuleId(ruleId, out List<(DateTime, Guid)>? applicationByRuleId);

            if (this.times == 0 || !hasRuleExecution)
            {
                return true;
            }
            int count = 0;
            foreach ((DateTime, Guid) application in applicationByRuleId)
            {
                if (application.Item2 == activityId)
                {
                    count++;
                }
            }
            return count < this.times;
        }

        /// <summary>
        /// Gets the FaultInjectionServerErrorType
        /// </summary>
        /// <returns></returns>
        public FaultInjectionServerErrorType GetInjectedServerErrorType()
        {
            return this.serverErrorType;
        }

        /// <summary>
        /// Get server error to be injected
        /// </summary>
        /// <param name="args"></param>
        /// <param name="ruleId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public StoreResponse GetInjectedServerError(ChannelCallArguments args, string ruleId)
        {
            StoreResponse storeResponse;

            switch (this.serverErrorType)
            {
                case FaultInjectionServerErrorType.Gone:
                    INameValueCollection goneHeaders = args.RequestHeaders;
                    goneHeaders.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ServerGenerated410).ToString(CultureInfo.InvariantCulture));
                    storeResponse = new StoreResponse()
                    {
                        Status = 410,
                        Headers = goneHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Gone, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.RetryWith:
                    storeResponse = new StoreResponse()
                    {
                        Status = 449,
                        Headers = args.RequestHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Retry With, rule: {ruleId}"))
                    };
                    
                    return storeResponse;

                case FaultInjectionServerErrorType.TooManyRequests:
                    INameValueCollection tooManyRequestsHeaders = args.RequestHeaders;
                    tooManyRequestsHeaders.Add(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, "500");
                    tooManyRequestsHeaders.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString(CultureInfo.InvariantCulture));

                    storeResponse = new StoreResponse()
                    {
                        Status = 429,
                        Headers = tooManyRequestsHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Too Many Requests, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.Timeout:

                    storeResponse = new StoreResponse()
                    {
                        Status = 408,
                        Headers = args.RequestHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Timeout, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.InternalServerEror:
                    storeResponse = new StoreResponse()
                    {
                        Status = 500,
                        Headers = args.RequestHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Internal Server Error, rule: {ruleId}"))
                    };
                    
                    return storeResponse;

                case FaultInjectionServerErrorType.ReadSessionNotAvailable:

                    const string badSesstionToken = "1:1#1#1=1#1=1";

                    INameValueCollection readSessionHeaders = args.RequestHeaders;
                    readSessionHeaders.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture));
                    readSessionHeaders.Add(HttpConstants.HttpHeaders.SessionToken, badSesstionToken);

                    storeResponse = new StoreResponse()
                    {
                        Status = 404,
                        Headers = readSessionHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Read Session Not Available, rule: {ruleId}"))
                    };
                    
                    return storeResponse;

                case FaultInjectionServerErrorType.PartitionIsMigrating:
                    INameValueCollection partitionMigrationHeaders = args.RequestHeaders;
                    partitionMigrationHeaders.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.CompletingPartitionMigration).ToString(CultureInfo.InvariantCulture));

                    storeResponse = new StoreResponse()
                    {
                        Status = 410,
                        Headers = partitionMigrationHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Partition Migrating, rule: {ruleId}"))
                    };
                    
                    return storeResponse;

                case FaultInjectionServerErrorType.PartitionIsSplitting:
                    INameValueCollection partitionSplitting = args.RequestHeaders;
                    partitionSplitting.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.CompletingSplit).ToString(CultureInfo.InvariantCulture));

                    storeResponse = new StoreResponse()
                    {
                        Status = 410,
                        Headers = partitionSplitting,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Partition Splitting, rule: {ruleId}"))
                    };

                    return storeResponse;
                case FaultInjectionServerErrorType.ServiceUnavailable:
                    storeResponse = new StoreResponse()
                    {
                        Status = 503,
                        Headers = args.RequestHeaders,
                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Fault Injection Server Error: Service Unavailable, rule: {ruleId}"))
                    };

                    return storeResponse;

                default:
                    throw new ArgumentException($"Server error type {this.serverErrorType} is not supported");
            }
        }
    }
}
