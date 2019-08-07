//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Client exception
    /// </summary>
    public class CosmosException : Exception
    {
        internal CosmosException(
            HttpStatusCode statusCode,
            string message,
            Error error = null)
            : base(message)
        {
            this.StatusCode = statusCode;
            this.Error = error;
            this.Headers = new Headers();
        }

        internal CosmosException(
            ResponseMessage cosmosResponseMessage,
            string message,
            Error error = null)
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
                if (this.Headers.ContentLengthAsLong > 0)
                {
                    using (StreamReader responseReader = new StreamReader(cosmosResponseMessage.Content))
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
            return $"{nameof(CosmosException)};StatusCode={this.StatusCode};SubStatusCode={this.SubStatusCode};ActivityId={this.ActivityId ?? string.Empty};RequestCharge={this.RequestCharge};Message={this.Message};";
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
