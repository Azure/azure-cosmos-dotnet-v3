//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using Azure.Core.Http;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Client exception
    /// </summary>
    public class CosmosException : RequestFailedException
    {
        internal CosmosException(
            HttpStatusCode statusCode,
            string message,
            Error error = null)
            : base((int)statusCode, message)
        {
            this.StatusCode = statusCode;
            this.Error = error;
            this.Headers = new ResponseHeaders();
        }

        internal CosmosException(
            Response response,
            string message,
            Error error = null)
            //CosmosDiagnostics diagnostics = null)
            : base(response.Status, message)
        {
            if (response != null)
            {
                this.StatusCode = (HttpStatusCode)response.Status;
                this.Headers = response.Headers;
                //this.ActivityId = this.Headers.ActivityId;
                //this.RequestCharge = this.Headers.RequestCharge;
                //this.RetryAfter = this.Headers.RetryAfter;
                //this.SubStatusCode = (int)this.Headers.SubStatusCode;
                //this.Diagnostics = diagnostics;
                if (response.ContentStream != null && response.ContentStream.Length > 0)
                {
                    using (StreamReader responseReader = new StreamReader(response.ContentStream))
                    {
                        this.ResponseBody = responseReader.ReadToEnd();
                    }
                }
            }

            this.Error = error;
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
            : base((int)statusCode, message)
        {
            this.SubStatusCode = subStatusCode;
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ActivityId = activityId;
            this.Headers = new ResponseHeaders();
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
        public virtual ResponseHeaders Headers { get; }

        /*
        /// <summary>
        /// Gets the diagnostics for the request
        /// </summary>
        public virtual CosmosDiagnostics Diagnostics { get; }
        */

        /// <summary>
        /// Gets the internal error object
        /// </summary>
        internal virtual Error Error { get; }

        /// <summary>
        /// Try to get a header from the cosmos response message
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="value"></param>
        /// <returns>A value indicating if the header was read.</returns>
        public virtual bool TryGetHeader(string headerName, out string value)
        {
            return this.Headers.TryGetValue(headerName, out value);
        }

        /// <summary>
        /// Create a custom string with all the relevant exception information
        /// </summary>
        /// <returns>A string representation of the exception.</returns>
        public override string ToString()
        {
            //string diagnostics = this.Diagnostics != null ? this.Diagnostics.ToString() : string.Empty;
            string diagnostics = string.Empty;
            return $"{nameof(CosmosException)};StatusCode={this.StatusCode};SubStatusCode={this.SubStatusCode};ActivityId={this.ActivityId ?? string.Empty};RequestCharge={this.RequestCharge};Message={this.Message};Diagnostics{diagnostics}";
        }

        internal ResponseMessage ToCosmosResponseMessage(RequestMessage request)
        {
            return new ResponseMessage(
                 headers: this.Headers,
                 requestMessage: request,
                 errorMessage: this.Message,
                 statusCode: this.StatusCode,
                 error: this.Error);
        }
    }
}
