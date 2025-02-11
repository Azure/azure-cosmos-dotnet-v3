//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Text;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Client exception
    /// </summary>
    public class CosmosException : Exception
    {
        private readonly string stackTrace;
        private readonly Lazy<string> lazyMessage;

        internal CosmosException(
            HttpStatusCode statusCode,
            string message,
            string stackTrace,
            Headers headers,
            ITrace trace,
            Error error,
            Exception innerException)
            // The message is overridden. Base exception does not have CTOR for just innerException and the property is not virtual
            : base(string.Empty, innerException)
        {
            this.ResponseBody = message;
            this.stackTrace = stackTrace;
            this.StatusCode = statusCode;
            this.Headers = headers ?? new Headers();
            this.Error = error;
            this.Trace = trace;
            this.Diagnostics = new CosmosTraceDiagnostics(this.Trace ?? NoOpTrace.Singleton);
            this.lazyMessage = new Lazy<string>(() => CosmosException.GetMessageHelper(
                statusCode,
                this.Headers,
                this.ResponseBody,
                this.Diagnostics));
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
            this.StatusCode = statusCode;
            this.ResponseBody = message;
            this.Trace = NoOpTrace.Singleton;
            this.lazyMessage = new Lazy<string>(() => message);
            this.Headers = new Headers()
            {
                SubStatusCode = (SubStatusCodes)subStatusCode,
                RequestCharge = requestCharge,
            };

            if (!string.IsNullOrEmpty(activityId))
            {
                this.Headers.ActivityId = activityId;
            }
        }

        /// <inheritdoc/>
        public override string Message => this.lazyMessage.Value;

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
        public virtual int SubStatusCode => Headers.GetIntValueOrDefault(this.Headers.SubStatusCodeLiteral);

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge => this.Headers.RequestCharge;

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId => this.Headers.ActivityId;

        /// <summary>
        /// Gets the retry after time. This tells how long a request should wait before doing a retry.
        /// </summary>
        public virtual TimeSpan? RetryAfter => this.Headers.RetryAfter;

        /// <summary>
        /// Gets the response headers
        /// </summary>
        public virtual Headers Headers { get; }

        /// <summary>
        /// Gets the diagnostics for the request
        /// </summary>
        public virtual CosmosDiagnostics Diagnostics { get; }

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

        internal virtual ITrace Trace { get; }

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
            ResponseMessage responseMessage = new ResponseMessage(
                 headers: this.Headers,
                 requestMessage: request,
                 cosmosException: this,
                 statusCode: this.StatusCode,
                 trace: this.Trace);

            return responseMessage;
        }

        private static string GetMessageHelper(
            HttpStatusCode statusCode,
            Headers headers,
            string responseBody,
            CosmosDiagnostics diagnostics)
        {
            StringBuilder stringBuilder = new StringBuilder();

            CosmosException.AppendMessageWithoutDiagnostics(
                stringBuilder,
                statusCode,
                headers,
                responseBody);

            // Include the diagnostics for exceptions where it is critical
            // to root cause failures.
            if (statusCode == HttpStatusCode.RequestTimeout
                || statusCode == HttpStatusCode.InternalServerError
                || statusCode == HttpStatusCode.ServiceUnavailable
                || (statusCode == HttpStatusCode.NotFound && headers.SubStatusCode == SubStatusCodes.ReadSessionNotAvailable))
            {
                stringBuilder.Append("; Diagnostics:");
                stringBuilder.Append(diagnostics.ToString());
            }

            return stringBuilder.ToString();
        }

        private static void AppendMessageWithoutDiagnostics(
            StringBuilder stringBuilder,
            HttpStatusCode statusCode,
            Headers headers,
            string responseBody)
        {
            stringBuilder.Append($"Response status code does not indicate success: ");
            stringBuilder.Append($"{statusCode} ({(int)statusCode})");
            stringBuilder.Append("; Substatus: ");
            stringBuilder.Append(headers?.SubStatusCodeLiteral ?? "0");
            stringBuilder.Append("; ActivityId: ");
            stringBuilder.Append(headers?.ActivityId ?? string.Empty);
            stringBuilder.Append("; Reason: (");
            stringBuilder.Append(responseBody ?? string.Empty);
            stringBuilder.Append(");");
        }

        private string ToStringHelper(
            StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
            {
                throw new ArgumentNullException(nameof(stringBuilder));
            }

            CosmosException.AppendMessageWithoutDiagnostics(
                stringBuilder,
                this.StatusCode,
                this.Headers,
                this.ResponseBody);
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
                stringBuilder.Append("--- Cosmos Diagnostics ---");
                stringBuilder.Append(this.Diagnostics);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// RecordOtelAttributes
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="scope"></param>
        internal static void RecordOtelAttributes(CosmosException exception, DiagnosticScope scope)
        {
            scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.StatusCode, (int)exception.StatusCode);
            scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.SubStatusCode, (int)exception.SubStatusCode);
            scope.AddIntegerAttribute(OpenTelemetryAttributeKeys.RequestCharge, (int)exception.RequestCharge);
            scope.AddAttribute(OpenTelemetryAttributeKeys.Region, 
                ClientTelemetryHelper.GetContactedRegions(exception.Diagnostics?.GetContactedRegions()));
            scope.AddAttribute(OpenTelemetryAttributeKeys.ExceptionMessage, exception.Message);

            if (!DiagnosticsFilterHelper.IsSuccessfulResponse(exception.StatusCode, (int)exception.SubStatusCode))
            {
                CosmosDbEventSource.RecordDiagnosticsForExceptions(exception.Diagnostics);
            }
        }
    }
}