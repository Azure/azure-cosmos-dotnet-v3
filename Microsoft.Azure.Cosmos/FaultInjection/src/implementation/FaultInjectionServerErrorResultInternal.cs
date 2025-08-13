//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;
    using Microsoft.Azure.Cosmos.Routing;
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
        private readonly double injectionRate;
        private readonly FaultInjectionApplicationContext applicationContext;
        private readonly GlobalEndpointManager globalEndpointManager;

        /// <summary>
        /// Constructor for FaultInjectionServerErrorResultInternal
        /// </summary>
        /// <param name="serverErrorType"></param>
        /// <param name="times"></param>
        /// <param name="delay"></param>
        /// <param name="injectionRate"></param>
        /// <param name="applicationContext"></param>
        /// <param name="globalEndpointManager"></param>
        public FaultInjectionServerErrorResultInternal(
            FaultInjectionServerErrorType serverErrorType,
            int times,
            TimeSpan delay,
            bool suppressServiceRequest,
            double injectionRate,
            FaultInjectionApplicationContext applicationContext,
            GlobalEndpointManager globalEndpointManager)
        {
            this.serverErrorType = serverErrorType;
            this.times = times;
            this.delay = delay;
            this.suppressServiceRequest = suppressServiceRequest;
            this.injectionRate = injectionRate;
            this.applicationContext = applicationContext;
            this.globalEndpointManager = globalEndpointManager;
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
        /// Returns the percentage of how many times the rule will be applied.
        /// </summary>
        /// <returns></returns>
        public double GetInjectionRate()
        {
            return this.injectionRate;
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
            string lsn = args.RequestHeaders.Get(WFConstants.BackendHeaders.LSN) ?? "0";

            switch (this.serverErrorType)
            {
                case FaultInjectionServerErrorType.Gone:
                    INameValueCollection goneHeaders = args.RequestHeaders;
                    goneHeaders.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ServerGenerated410).ToString(CultureInfo.InvariantCulture));
                    goneHeaders.Set(WFConstants.BackendHeaders.LocalLSN, lsn);
                    storeResponse = new StoreResponse()
                    {
                        Status = 410,
                        Headers = goneHeaders,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Gone, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.RetryWith:
                    INameValueCollection retryWithHeaders = args.RequestHeaders;
                    retryWithHeaders.Set(WFConstants.BackendHeaders.LocalLSN, lsn);
                    storeResponse = new StoreResponse()
                    {
                        Status = 449,
                        Headers = retryWithHeaders,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Retry With, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.TooManyRequests:
                    INameValueCollection tooManyRequestsHeaders = args.RequestHeaders;
                    tooManyRequestsHeaders.Set(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, "500");
                    tooManyRequestsHeaders.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.RUBudgetExceeded).ToString(CultureInfo.InvariantCulture));
                    tooManyRequestsHeaders.Set(WFConstants.BackendHeaders.LocalLSN, lsn);

                    storeResponse = new StoreResponse()
                    {
                        Status = 429,
                        Headers = tooManyRequestsHeaders,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Too Many Requests, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.Timeout:
                    INameValueCollection timeoutHeaders = args.RequestHeaders;
                    timeoutHeaders.Set(WFConstants.BackendHeaders.LocalLSN, lsn);

                    storeResponse = new StoreResponse()
                    {
                        Status = 408,
                        Headers = timeoutHeaders,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Timeout, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.InternalServerError:
                    INameValueCollection internalServerErrorHeaders = args.RequestHeaders;
                    internalServerErrorHeaders.Set(WFConstants.BackendHeaders.LocalLSN, lsn);

                    storeResponse = new StoreResponse()
                    {
                        Status = 500,
                        Headers = internalServerErrorHeaders,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Internal Server Error, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.ReadSessionNotAvailable:

                    const string badSesstionToken = "1:1#1#1=1#1=1";

                    INameValueCollection readSessionHeaders = args.RequestHeaders;
                    readSessionHeaders.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture));
                    readSessionHeaders.Set(HttpConstants.HttpHeaders.SessionToken, badSesstionToken);
                    readSessionHeaders.Set(WFConstants.BackendHeaders.LocalLSN, lsn);

                    storeResponse = new StoreResponse()
                    {
                        Status = 404,
                        Headers = readSessionHeaders,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Read Session Not Available, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.PartitionIsMigrating:
                    INameValueCollection partitionMigrationHeaders = args.RequestHeaders;
                    partitionMigrationHeaders.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.CompletingPartitionMigration).ToString(CultureInfo.InvariantCulture));
                    partitionMigrationHeaders.Set(WFConstants.BackendHeaders.LocalLSN, lsn);

                    storeResponse = new StoreResponse()
                    {
                        Status = 410,
                        Headers = partitionMigrationHeaders,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Partition Migrating, rule: {ruleId}"))
                    };

                    return storeResponse;

                case FaultInjectionServerErrorType.PartitionIsSplitting:
                    INameValueCollection partitionSplitting = args.RequestHeaders;
                    partitionSplitting.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.CompletingSplit).ToString(CultureInfo.InvariantCulture));
                    partitionSplitting.Set(WFConstants.BackendHeaders.LocalLSN, lsn);

                    storeResponse = new StoreResponse()
                    {
                        Status = 410,
                        Headers = partitionSplitting,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Partition Splitting, rule: {ruleId}"))
                    };

                    return storeResponse;
                case FaultInjectionServerErrorType.ServiceUnavailable:
                    INameValueCollection serviceUnavailableHeaders = args.RequestHeaders;
                    serviceUnavailableHeaders.Set(WFConstants.BackendHeaders.LocalLSN, lsn);

                    storeResponse = new StoreResponse()
                    {
                        Status = 503,
                        Headers = serviceUnavailableHeaders,
                        ResponseBody = new MemoryStream(FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Service Unavailable, rule: {ruleId}"))
                    };

                    return storeResponse;

                default:
                    throw new ArgumentException($"Server error type {this.serverErrorType} is not supported");
            }
        }

        /// <summary>
        /// Get server error to be injected
        /// </summary>
        /// <param name="dsr"></param>
        /// <param name="ruleId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public HttpResponseMessage GetInjectedServerError(DocumentServiceRequest dsr, string ruleId)
        {
            HttpResponseMessage httpResponse;
            //Global or Local lsn?
            string lsn = dsr.RequestContext.QuorumSelectedLSN.ToString(CultureInfo.InvariantCulture);
            INameValueCollection headers = dsr.Headers;
            bool isProxyCall = this.IsProxyCall(dsr);

            switch (this.serverErrorType)
            {
                case FaultInjectionServerErrorType.Gone:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.Gone,
                        Content = new FauntInjectionHttpContent(
                        new MemoryStream(
                            isProxyCall
                                ? FaultInjectionResponseEncoding.GetBytes(
                                    GetProxyResponseMessageString((int)StatusCodes.Gone, (int)SubStatusCodes.ServerGenerated410, "Gone", ruleId))
                                : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Gone, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.ServerGenerated410).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);
                    return httpResponse;

                case FaultInjectionServerErrorType.TooManyRequests:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.TooManyRequests,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                    GetProxyResponseMessageString((int)StatusCodes.TooManyRequests, (int)SubStatusCodes.RUBudgetExceeded, "TooManyRequests", ruleId))
                                : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: TooManyRequests, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(500));
                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.RUBudgetExceeded).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                case FaultInjectionServerErrorType.Timeout:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.RequestTimeout,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                       GetProxyResponseMessageString((int)StatusCodes.RequestTimeout, (int)SubStatusCodes.Unknown, "Timeout", ruleId))
                                    : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: Timeout, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.Unknown).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                case FaultInjectionServerErrorType.InternalServerError:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.InternalServerError,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                        GetProxyResponseMessageString((int)StatusCodes.InternalServerError, (int)SubStatusCodes.Unknown, "InternalServerError", ruleId))
                                    : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: InternalServerError, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.Unknown).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                case FaultInjectionServerErrorType.ReadSessionNotAvailable:

                    const string badSesstionToken = "1:1#1#1=1#1=1";
                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.NotFound,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                        GetProxyResponseMessageString((int)StatusCodes.NotFound, (int)SubStatusCodes.ReadSessionNotAvailable, "ReadSessionNotAvailable", ruleId))
                                    : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: ReadSessionNotAvailable, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(HttpConstants.HttpHeaders.SessionToken, badSesstionToken);
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                case FaultInjectionServerErrorType.PartitionIsMigrating:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.Gone,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                        GetProxyResponseMessageString((int)StatusCodes.Gone, (int)SubStatusCodes.CompletingPartitionMigration, "PartitionIsMigrating", ruleId))
                                    : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: PartitionIsMigrating, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.CompletingPartitionMigration).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                case FaultInjectionServerErrorType.PartitionIsSplitting:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.Gone,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                        GetProxyResponseMessageString((int)StatusCodes.Gone, (int)SubStatusCodes.CompletingSplit, "PartitionIsSplitting", ruleId))
                                    : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: PartitionIsSplitting, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.CompletingSplit).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                case FaultInjectionServerErrorType.ServiceUnavailable:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.ServiceUnavailable,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                       GetProxyResponseMessageString((int)StatusCodes.ServiceUnavailable, (int)SubStatusCodes.Unknown, "ServiceUnavailable", ruleId))
                                    : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: ServiceUnavailable, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.Unknown).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                case FaultInjectionServerErrorType.DatabaseAccountNotFound:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.Forbidden,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                        GetProxyResponseMessageString((int)StatusCodes.Forbidden, (int)SubStatusCodes.DatabaseAccountNotFound, "DatabaseAccountNotFound", ruleId))
                                    : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: DatabaseAccountNotFound, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.DatabaseAccountNotFound).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                case FaultInjectionServerErrorType.LeaseNotFound:

                    httpResponse = new HttpResponseMessage
                    {
                        Version = isProxyCall
                            ? new Version(2, 0)
                            : new Version(1, 1),
                        StatusCode = HttpStatusCode.Gone,
                        Content = new FauntInjectionHttpContent(
                            new MemoryStream(
                                isProxyCall
                                    ? FaultInjectionResponseEncoding.GetBytes(
                                        GetProxyResponseMessageString((int)StatusCodes.Gone, (int)SubStatusCodes.LeaseNotFound, "LeaseNotFound", ruleId))
                                    : FaultInjectionResponseEncoding.GetBytes($"Fault Injection Server Error: LeaseNotFound, rule: {ruleId}"))),
                    };

                    this.SetHttpHeaders(httpResponse, headers, isProxyCall);

                    httpResponse.Headers.Add(
                        WFConstants.BackendHeaders.SubStatus,
                        ((int)SubStatusCodes.LeaseNotFound).ToString(CultureInfo.InvariantCulture));
                    httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

                    return httpResponse;

                default:
                    throw new ArgumentException($"Server error type {this.serverErrorType} is not supported");
            }
        }

        private bool IsProxyCall(DocumentServiceRequest dsr)
        {
            string gwUriString = dsr.Headers.Get("FAULTINJECTION_IS_PROXY");

            return !string.IsNullOrEmpty(gwUriString);
        }

        private void SetHttpHeaders(
            HttpResponseMessage httpResponse,
            INameValueCollection headers,
            bool isProxyCall)
        {
            foreach (string header in headers.AllKeys())
            {
                if (header != "FAULTINJECTION_IS_PROXY")
                {
                    httpResponse.Headers.Add(header, headers.Get(header));
                }
            }

            if (isProxyCall)
            {
                httpResponse.Headers.Add(ThinClientConstants.RoutedViaProxy,"1");
            }
        }

        private static string GetProxyResponseMessageString(
            int statusCode,
            int subStatusCode,
            string message, 
            string faultInjectionRuleId)
        {
            return $"{{\"code\": \"{statusCode}:{subStatusCode}\",\"message\":\"Fault Injection Server Error: {message}, rule: {faultInjectionRuleId}\"}}";
        }

        internal class FauntInjectionHttpContent : HttpContent
        {
            private readonly Stream content;

            public FauntInjectionHttpContent(Stream content)
            {
                this.content = content;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                return this.content.CopyToAsync(stream);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = this.content.Length;
                return true;
            }
        }

        internal static class FaultInjectionResponseEncoding
        {
            private static readonly UTF8Encoding Encoding = new UTF8Encoding(false);

            public static byte[] GetBytes(string value)
            {
                return Encoding.GetBytes(value);
            }

            public static byte[] GetBytesFromHexString(string hexString)
            {
                return Convert.FromHexString(hexString);
            }
        }
    }
}
