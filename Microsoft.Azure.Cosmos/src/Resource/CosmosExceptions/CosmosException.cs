//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Client exception
    /// </summary>
    public class CosmosException : Exception
    {
        private readonly string stackTrace;

        internal CosmosException(
            HttpStatusCode statusCodes,
            string message,
            int subStatusCode,
            string stackTrace,
            string activityId,
            double requestCharge,
            TimeSpan? retryAfter,
            Headers headers,
            CosmosDiagnosticsContext diagnosticsContext,
            Error error,
            Exception innerException)
            : base(CosmosException.GetMessageHelper(
                statusCodes,
                subStatusCode,
                message,
                activityId,
                innerException), innerException)
        {
            this.ResponseBody = message;
            this.stackTrace = stackTrace;
            this.ActivityId = activityId;
            this.StatusCode = statusCodes;
            this.SubStatusCode = subStatusCode;
            this.RetryAfter = retryAfter;
            this.RequestCharge = requestCharge;
            this.Headers = headers;
            this.Error = error;

            // Always have a diagnostic context. A new diagnostic will have useful info like user agent
            this.DiagnosticsContext = diagnosticsContext ?? new CosmosDiagnosticsContextCore();
        }

        /// <summary>
        /// Create a <see cref="CosmosException"/>
        /// </summary>
        /// <param name="message">The message associated with the exception.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/> associated with the exception.</param>
        /// <param name="subStatusCode">A sub status code associated with the exception.</param>
        /// <param name="activityId">An ActivityId associated with the operation that generated the exception.</param>
        /// <param name="requestCharge">A request charge associated with the operation that generated the exception.</param>
        public CosmosException(
            string message,
            HttpStatusCode statusCode,
            int subStatusCode,
            string activityId,
            double requestCharge)
            : base(message)
        {
            this.stackTrace = null;
            this.SubStatusCode = subStatusCode;
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ActivityId = activityId;
            this.Headers = new Headers();
            this.DiagnosticsContext = new CosmosDiagnosticsContextCore();
        }

        /// <summary>
        /// The body of the cosmos response message as a string
        /// </summary>
        public virtual string ResponseBody { get; }

        /// <summary>
        /// Gets the request completion status code from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The request completion status code</value>
        public virtual HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the request completion sub status code from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The request completion status code</value>
        public virtual int SubStatusCode { get; }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge { get; }

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId { get; }

        /// <summary>
        /// Gets the retry after time. This tells how long a request should wait before doing a retry.
        /// </summary>
        public virtual TimeSpan? RetryAfter { get; }

        /// <summary>
        /// Gets the response headers
        /// </summary>
        public virtual Headers Headers { get; }

        /// <summary>
        /// Gets the diagnostics for the request
        /// </summary>
        public virtual CosmosDiagnostics Diagnostics => this.DiagnosticsContext.Diagnostics;

        /// <inheritdoc/>
        public override string StackTrace
        {
            get
            {
                if (this.stackTrace != null)
                {
                    return this.stackTrace;
                }
                else
                {
                    return base.StackTrace;
                }
            }
        }

        internal virtual CosmosDiagnosticsContext DiagnosticsContext { get; }

        /// <summary>
        /// Gets the internal error object.
        /// </summary>
        internal virtual Documents.Error Error { get; set; }

        /// <summary>
        /// Try to get a header from the cosmos response message
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="value"></param>
        /// <returns>A value indicating if the header was read.</returns>
        public virtual bool TryGetHeader(string headerName, out string value)
        {
            if (this.Headers == null)
            {
                value = null;
                return false;
            }

            return this.Headers.TryGetValue(headerName, out value);
        }

        /// <summary>
        /// Create a custom string with all the relevant exception information
        /// </summary>
        /// <returns>A string representation of the exception.</returns>
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(this.GetType().FullName);
            stringBuilder.Append(" : ");

            this.ToStringHelper(stringBuilder);

            return stringBuilder.ToString();
        }

        internal ResponseMessage ToCosmosResponseMessage(RequestMessage request)
        {
            return new ResponseMessage(
                 headers: this.Headers,
                 requestMessage: request,
                 cosmosException: this,
                 statusCode: this.StatusCode,
                 diagnostics: this.DiagnosticsContext);
        }

        private static bool TryGetTroubleshootingLink(
            HttpStatusCode statusCode,
            int subStatusCode,
            Exception innerException,
            out string tsgLink)
        {
            if (CosmosTroubleshootingLink.TryGetTroubleshootingLinks(
                (int)statusCode,
                subStatusCode,
                innerException,
                out CosmosTroubleshootingLink link))
            {
                tsgLink = link.Link;
                return true;
            }

            tsgLink = null;
            return false;
        }

        private static string GetMessageHelper(
            HttpStatusCode statusCode,
            int subStatusCode,
            string responseBody,
            string activityId,
            Exception innerException)
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append($"Response status code does not indicate success: ");
            stringBuilder.Append($"{statusCode} ({(int)statusCode})");
            stringBuilder.Append("; Substatus: ");
            stringBuilder.Append(subStatusCode);

            if (CosmosException.TryGetTroubleshootingLink(
                statusCode,
                subStatusCode,
                innerException,
                out string tsgLink))
            {
                stringBuilder.Append("; Troubleshooting: ");
                stringBuilder.Append(tsgLink);
            }

            stringBuilder.Append("; ActivityId: ");
            stringBuilder.Append(activityId ?? string.Empty);
            stringBuilder.Append("; Reason: (");
            stringBuilder.Append(responseBody ?? string.Empty);
            stringBuilder.Append(");");

            return stringBuilder.ToString();
        }

        private string ToStringHelper(
            StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
            {
                throw new ArgumentNullException(nameof(stringBuilder));
            }

            stringBuilder.Append(this.Message);
            stringBuilder.AppendLine();

            if (this.InnerException != null)
            {
                stringBuilder.Append(" ---> ");
                stringBuilder.Append(this.InnerException);
                stringBuilder.AppendLine();
                stringBuilder.Append("   ");
                stringBuilder.Append("--- End of inner exception stack trace ---");
                stringBuilder.AppendLine();
            }

            if (this.StackTrace != null)
            {
                stringBuilder.Append(this.StackTrace);
                stringBuilder.AppendLine();
            }

            if (this.Diagnostics != null)
            {
                stringBuilder.Append(this.Diagnostics);
            }

            return stringBuilder.ToString();
        }
    }
}
