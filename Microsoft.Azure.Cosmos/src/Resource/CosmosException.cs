//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Client exception
    /// </summary>
    public class CosmosException : Exception
    {
        private readonly CosmosResponseMessageHeaders Headers = null;

        internal CosmosException(
            CosmosResponseMessage cosmosResponseMessage, 
            string message,
            Error error = null) :
            base(message)
        {
            if (cosmosResponseMessage != null)
            {
                this.StatusCode = cosmosResponseMessage.StatusCode;
                this.Headers = cosmosResponseMessage.Headers;
                this.ActivityId = this.Headers?.GetHeaderValue<string>(HttpConstants.HttpHeaders.ActivityId);
                this.RequestCharge = this.Headers == null ? 0 : this.Headers.GetHeaderValue<double>(HttpConstants.HttpHeaders.RequestCharge);
                this.SubStatusCode = (int)this.Headers.SubStatusCode;
                this.Error = error;
                if (cosmosResponseMessage.Headers.ContentLengthAsLong > 0)
                {
                    using (StreamReader responseReader = new StreamReader(cosmosResponseMessage.Content))
                    {
                        this.ResponseBody = responseReader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// Create a <see cref="CosmosException"/>
        /// </summary>
        public CosmosException(
            string message,
            HttpStatusCode statusCode,
            int subStatusCode,
            string activityId,
            double requestCharge) : base(message)
        {
            this.SubStatusCode = subStatusCode;
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ActivityId = activityId;
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
        /// Gets the internal error object
        /// </summary>
        internal virtual Error Error { get; }

        /// <summary>
        /// Try to get a header from the cosmos response message
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
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
        public override string ToString()
        {
            return $"CosmosRequestException;StatusCode={this.StatusCode};SubStatusCode={this.SubStatusCode};ActivityId={this.ActivityId ?? string.Empty};RequestCharge={this.RequestCharge};Message={this.Message};";
        }
    }
}