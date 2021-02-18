//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a response from the Azure Cosmos DB service.
    /// </summary>
    public class ResponseMessage : IDisposable
    {
        /// <summary>
        /// Create a <see cref="ResponseMessage"/>
        /// </summary>
        public ResponseMessage()
        {
            this.Headers = new Headers();
            this.CosmosException = null;
            this.Trace = NoOpTrace.Singleton;
        }

        /// <summary>
        /// Create a <see cref="ResponseMessage"/>
        /// </summary>
        /// <param name="statusCode">The HttpStatusCode of the response</param>
        /// <param name="requestMessage">The <see cref="Cosmos.RequestMessage"/> object</param>
        /// <param name="errorMessage">The reason for failures if any.</param>
        public ResponseMessage(
            HttpStatusCode statusCode,
            RequestMessage requestMessage = null,
            string errorMessage = null)
        {
            if ((statusCode < 0) || ((int)statusCode > 999))
            {
                throw new ArgumentOutOfRangeException(nameof(statusCode));
            }

            this.StatusCode = statusCode;
            this.RequestMessage = requestMessage;
            this.Headers = new Headers();
            this.Trace = requestMessage?.Trace ?? NoOpTrace.Singleton;

            if (!string.IsNullOrEmpty(errorMessage))
            {
                this.CosmosException = CosmosExceptionFactory.Create(
                    statusCode,
                    requestMessage,
                    errorMessage);
            }
        }

        /// <summary>
        /// Create a <see cref="ResponseMessage"/>
        /// </summary>
        /// <param name="statusCode">The HttpStatusCode of the response</param>
        /// <param name="requestMessage">The <see cref="Cosmos.RequestMessage"/> object</param>
        /// <param name="headers">The headers for the response.</param>
        /// <param name="cosmosException">The exception if the response is from an error.</param>
        /// <param name="trace">The trace for the request</param>
        internal ResponseMessage(
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            Headers headers,
            CosmosException cosmosException,
            ITrace trace)
        {
            this.StatusCode = statusCode;
            this.RequestMessage = requestMessage;
            this.CosmosException = cosmosException;
            this.Headers = headers ?? new Headers();
            if (requestMessage != null && requestMessage.Trace != null)
            {
                this.Trace = requestMessage.Trace;
            }
            else
            {
                this.Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            }
        }

        /// <summary>
        /// Gets the <see cref="HttpStatusCode"/> of the current response.
        /// </summary>
        public virtual HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Gets the content as a <see cref="Stream"/>, if any, of the current response.
        /// </summary>
        public virtual Stream Content
        {
            get => this.content;
            set
            {
                this.CheckDisposed();
                this.content = value;
            }
        }

        /// <summary>
        /// Gets the reason for a failure in the current response.
        /// </summary>
        public virtual string ErrorMessage => this.CosmosException?.Message;

        /// <summary>
        /// Gets the current <see cref="ResponseMessage"/> HTTP headers.
        /// </summary>
        public virtual Headers Headers { get; }

        /// <summary>
        /// Gets the Continuation Token in the current <see cref="ResponseMessage"/>.
        /// </summary>
        /// <remarks>
        /// This is only used in feed operations like query and change feed
        /// </remarks>
        public virtual string ContinuationToken => this.Headers?.ContinuationToken;

        /// <summary>
        /// Gets the original request message
        /// </summary>
        public virtual RequestMessage RequestMessage { get; internal set; }

        /// <summary>
        /// Gets the cosmos diagnostic information for the current request to Azure Cosmos DB service
        /// </summary>
        public virtual CosmosDiagnostics Diagnostics => new CosmosTraceDiagnostics(this.Trace ?? NoOpTrace.Singleton);

        internal ITrace Trace { get; set; }

        internal CosmosException CosmosException { get; }

        private bool disposed;

        private Stream content;

        /// <summary>
        /// Asserts if the current <see cref="HttpStatusCode"/> is a success.
        /// </summary>
        public virtual bool IsSuccessStatusCode => this.StatusCode.IsSuccess();

        /// <summary>
        /// Checks if the current <see cref="ResponseMessage"/> has a successful status code, otherwise, throws.
        /// </summary>
        /// <exception cref="Cosmos.CosmosException">An instance of <see cref="Cosmos.CosmosException"/> representing the error state.</exception>
        /// <returns>The current <see cref="ResponseMessage"/>.</returns>
        public virtual ResponseMessage EnsureSuccessStatusCode()
        {
            if (!this.IsSuccessStatusCode)
            {
                throw CosmosExceptionFactory.Create(this);
            }

            return this;
        }

        /// <summary>
        /// Disposes the current <see cref="ResponseMessage"/>.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }

        internal string GetResourceAddress()
        {
            string resourceLink = this.RequestMessage?.RequestUriString;
            if (PathsHelper.TryParsePathSegments(
                resourceLink,
                out bool isFeed,
                out string resourceTypeString,
                out string resourceIdOrFullName,
                out bool isNameBased))
            {
                Debug.Assert(resourceIdOrFullName != null);
                return resourceIdOrFullName;
            }

            return null;
        }

        /// <summary>
        /// Dispose of the response message content
        /// </summary>
        /// <param name="disposing">True to dispose of content</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.disposed = true;
                if (this.content != null)
                {
                    this.content.Dispose();
                    this.content = null;
                }

                if (this.RequestMessage != null)
                {
                    this.RequestMessage.Dispose();
                    this.RequestMessage = null;
                }

                if (this.Trace != null)
                {
                    this.Trace.Dispose();
                    this.Trace = null;
                }
            }
        }

        private void CheckDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().ToString());
            }
        }
    }
}