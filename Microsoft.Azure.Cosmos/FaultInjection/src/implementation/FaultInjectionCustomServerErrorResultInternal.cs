//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Internal Fault Injection Custom Server Error Result.
    /// </summary>
    internal class FaultInjectionCustomServerErrorResultInternal
    {
        private readonly int statusCode;
        private readonly int subStatusCode;
        private readonly int times;
        private readonly TimeSpan delay;
        private readonly bool suppressServiceRequest;
        private readonly double injectionRate;
        private readonly FaultInjectionApplicationContext applicationContext;

        /// <summary>
        /// Constructor for FaultInjectionCustomServerErrorResultInternal
        /// </summary>
        /// <param name="statusCode">The custom HTTP status code.</param>
        /// <param name="subStatusCode">The custom substatus code.</param>
        /// <param name="times">The number of times the rule can be applied.</param>
        /// <param name="delay">The injected delay.</param>
        /// <param name="suppressServiceRequest">Whether to suppress service request.</param>
        /// <param name="injectionRate">The injection rate.</param>
        /// <param name="applicationContext">The application context.</param>
        public FaultInjectionCustomServerErrorResultInternal(
            int statusCode,
            int subStatusCode,
            int times,
            TimeSpan delay,
            bool suppressServiceRequest,
            double injectionRate,
            FaultInjectionApplicationContext applicationContext)
        {
            this.statusCode = statusCode;
            this.subStatusCode = subStatusCode;
            this.times = times;
            this.delay = delay;
            this.suppressServiceRequest = suppressServiceRequest;
            this.injectionRate = injectionRate;
            this.applicationContext = applicationContext;
        }

        /// <summary>
        /// Gets the custom status code.
        /// </summary>
        /// <returns>The status code.</returns>
        public int GetStatusCode()
        {
            return this.statusCode;
        }

        /// <summary>
        /// Gets the custom substatus code.
        /// </summary>
        /// <returns>The substatus code.</returns>
        public int GetSubStatusCode()
        {
            return this.subStatusCode;
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
        /// </summary>
        /// <returns>A TimeSpan representing the length of the delay.</returns>
        public TimeSpan GetDelay()
        {
            return this.delay;
        }

        /// <summary>
        /// Return whether or not the service request should be suppressed.
        /// </summary>
        /// <returns>True if service request should be suppressed, false otherwise.</returns>
        public bool GetSuppressServiceRequest()
        {
            return this.suppressServiceRequest;
        }

        /// <summary>
        /// Returns the percentage of how many times the rule will be applied.
        /// </summary>
        /// <returns>The injection rate.</returns>
        public double GetInjectionRate()
        {
            return this.injectionRate;
        }

        /// <summary>
        /// Determines if the rule can be applied.
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <param name="activityId">The activity ID.</param>
        /// <returns>True if the rule can be applied.</returns>
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
        /// Get server error to be injected for Direct mode (StoreResponse).
        /// </summary>
        /// <param name="args">The channel call arguments.</param>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>The injected StoreResponse.</returns>
        public StoreResponse GetInjectedServerError(ChannelCallArguments args, string ruleId)
        {
            INameValueCollection headers = args.RequestHeaders;
            string lsn = args.RequestHeaders.Get(WFConstants.BackendHeaders.LSN) ?? "0";
            headers.Set(WFConstants.BackendHeaders.SubStatus, this.subStatusCode.ToString(CultureInfo.InvariantCulture));
            headers.Set(WFConstants.BackendHeaders.LocalLSN, lsn);

            StoreResponse storeResponse = new StoreResponse()
            {
                Status = this.statusCode,
                Headers = headers,
                ResponseBody = new MemoryStream(FaultInjectionServerErrorResultInternal.FaultInjectionResponseEncoding.GetBytes(
                    $"Fault Injection Custom Server Error: Status={this.statusCode}, SubStatus={this.subStatusCode}, rule: {ruleId}"))
            };

            return storeResponse;
        }

        /// <summary>
        /// Get server error to be injected for Gateway mode (HttpResponseMessage).
        /// </summary>
        /// <param name="dsr">The document service request.</param>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>The injected HttpResponseMessage.</returns>
        public HttpResponseMessage GetInjectedServerError(DocumentServiceRequest dsr, string ruleId)
        {
            string lsn = dsr.RequestContext.QuorumSelectedLSN.ToString(CultureInfo.InvariantCulture);
            INameValueCollection headers = dsr.Headers;
            bool isProxyCall = this.IsProxyCall(dsr);

            HttpResponseMessage httpResponse = new HttpResponseMessage
            {
                Version = isProxyCall ? new Version(2, 0) : new Version(1, 1),
                StatusCode = (HttpStatusCode)this.statusCode,
                Content = new FaultInjectionServerErrorResultInternal.FauntInjectionHttpContent(
                    new MemoryStream(
                        isProxyCall
                            ? FaultInjectionServerErrorResultInternal.FaultInjectionResponseEncoding.GetBytes(
                                GetProxyResponseMessageString(this.statusCode, this.subStatusCode, "CustomServerError", ruleId))
                            : FaultInjectionServerErrorResultInternal.FaultInjectionResponseEncoding.GetBytes(
                                $"Fault Injection Custom Server Error: Status={this.statusCode}, SubStatus={this.subStatusCode}, rule: {ruleId}"))),
            };

            this.SetHttpHeaders(httpResponse, headers, isProxyCall);

            httpResponse.Headers.Add(
                WFConstants.BackendHeaders.SubStatus,
                this.subStatusCode.ToString(CultureInfo.InvariantCulture));
            httpResponse.Headers.Add(WFConstants.BackendHeaders.LocalLSN, lsn);

            return httpResponse;
        }

        private static string GetProxyResponseMessageString(int statusCode, int subStatusCode, string errorType, string ruleId)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"code\": \"{statusCode}\",");
            sb.AppendLine($"  \"message\": \"Fault Injection Custom Server Error: {errorType}, Status={statusCode}, SubStatus={subStatusCode}, rule: {ruleId}\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private bool IsProxyCall(DocumentServiceRequest dsr)
        {
            return dsr.UseGatewayMode && !dsr.IsReadOnlyRequest;
        }

        private void SetHttpHeaders(HttpResponseMessage httpResponse, INameValueCollection headers, bool isProxyCall)
        {
            httpResponse.Headers.Add(HttpConstants.HttpHeaders.SessionToken, headers.Get(HttpConstants.HttpHeaders.SessionToken));
            httpResponse.Headers.Add(HttpConstants.HttpHeaders.ActivityId, headers.Get(HttpConstants.HttpHeaders.ActivityId));

            if (isProxyCall)
            {
                httpResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
        }
    }
}
