//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text;

    /// <summary>
    /// The Cosmos Client exception
    /// </summary>
    public class CosmosException : Exception
    {
        private readonly StackTrace stackTrace;

        internal CosmosException(
            HttpStatusCode statusCode,
            string message,
            Exception inner = null)
            : base(message, inner)
        {
            this.StatusCode = statusCode;
            this.Headers = new Headers();
        }

        internal CosmosException(
            ResponseMessage cosmosResponseMessage,
            string message)
            : base(message)
        {
            if (cosmosResponseMessage != null)
            {
                this.StatusCode = cosmosResponseMessage.StatusCode;
                this.Headers = cosmosResponseMessage.Headers;
                if (this.Headers == null)
                {
                    this.Headers = new Headers();
                }

                this.ActivityId = this.Headers.ActivityId;
                this.RequestCharge = this.Headers.RequestCharge;
                this.RetryAfter = this.Headers.RetryAfter;
                this.SubStatusCode = (int)this.Headers.SubStatusCode;
                this.Diagnostics = cosmosResponseMessage.Diagnostics;
                if (this.Headers.ContentLengthAsLong > 0)
                {
                    using (StreamReader responseReader = new StreamReader(cosmosResponseMessage.Content))
                    {
                        this.ResponseBody = responseReader.ReadToEnd();
                    }
                }
            }
        }

        internal CosmosException(
            HttpStatusCode statusCodes,
            int subStatusCode,
            string message,
            StackTrace stackTrace,
            string activityId,
            double requestCharge,
            TimeSpan? retryAfter,
            Headers headers,
            CosmosDiagnosticsContext diagnosticsContext,
            Exception innerException)
            : base(message, innerException)
        {
            this.stackTrace = stackTrace;
            this.ActivityId = activityId;
            this.StatusCode = statusCodes;
            this.SubStatusCode = subStatusCode;
            this.RetryAfter = retryAfter;
            this.RequestCharge = requestCharge;
            this.Diagnostics = diagnosticsContext ?? CosmosDiagnosticsContext.Create();
            this.Headers = headers;
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
            this.SubStatusCode = subStatusCode;
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ActivityId = activityId;
            this.Headers = new Headers();
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
        public virtual CosmosDiagnostics Diagnostics { get; }

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

        /// <inheritdoc/>
        public override string StackTrace => this.stackTrace.ToString();

        /// <summary>
        /// Create a custom string with all the relevant exception information
        /// </summary>
        /// <returns>A string representation of the exception.</returns>
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(this.GetType().FullName);
            if (this.Message != null)
            {
                stringBuilder.Append(" : ");
                stringBuilder.Append(this.Message);
                stringBuilder.AppendLine();
            }

            stringBuilder.AppendFormat("StatusCode = {0};", this.StatusCode);
            stringBuilder.AppendLine();

            stringBuilder.AppendFormat("SubStatusCode = {0};", this.SubStatusCode);
            stringBuilder.AppendLine();

            stringBuilder.AppendFormat("ActivityId = {0};", this.ActivityId ?? Guid.Empty.ToString());
            stringBuilder.AppendLine();

            stringBuilder.AppendFormat("RequestCharge = {0};", this.RequestCharge);
            stringBuilder.AppendLine();

            if (this.Diagnostics != null)
            {
                stringBuilder.Append(this.Diagnostics);
                stringBuilder.AppendLine();
            }

            if (this.InnerException != null)
            {
                stringBuilder.Append(" ---> ");
                stringBuilder.Append(this.InnerException);
                stringBuilder.AppendLine();
                stringBuilder.Append("   ");
                stringBuilder.Append("--- End of inner exception stack trace ---");
                stringBuilder.AppendLine();
            }

            stringBuilder.Append(this.StackTrace);

            return stringBuilder.ToString();
        }

        internal ResponseMessage ToCosmosResponseMessage(RequestMessage request)
        {
            return new ResponseMessage(
                 headers: this.Headers,
                 requestMessage: request,
                 errorMessage: this.Message,
                 statusCode: this.StatusCode,
                 diagnostics: request.DiagnosticsContext);
        }
    }
}
