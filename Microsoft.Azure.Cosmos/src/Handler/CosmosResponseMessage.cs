//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a response from the Azure Cosmos DB service.
    /// </summary>
    public class CosmosResponseMessage : IDisposable
    {
        /// <summary>
        /// Create a <see cref="CosmosResponseMessage"/>
        /// </summary>
        public CosmosResponseMessage() { }

        /// <summary>
        /// Create a <see cref="CosmosResponseMessage"/>
        /// </summary>
        /// <param name="statusCode">The HttpStatusCode of the response</param>
        /// <param name="requestMessage">The <see cref="CosmosRequestMessage"/> object</param>
        /// <param name="errorMessage">The reason for failures if any.</param>
        public CosmosResponseMessage(
            HttpStatusCode statusCode,
            CosmosRequestMessage requestMessage = null,
            string errorMessage = null)
        {
            if ((statusCode < 0) || ((int)statusCode > 999))
            {
                throw new ArgumentOutOfRangeException(nameof(statusCode));
            }

            this.StatusCode = statusCode;
            this.RequestMessage = requestMessage;
            this.ErrorMessage = errorMessage;
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
            get => this._content;
            set
            {
                this.CheckDisposed();
                this._content = value;
            }
        }

        /// <summary>
        /// Gets the reason for a failure in the current response.
        /// </summary>
        public virtual string ErrorMessage { get; internal set; }

        /// <summary>
        /// Gets the current <see cref="CosmosResponseMessage"/> HTTP headers.
        /// </summary>
        public virtual CosmosResponseMessageHeaders Headers { get; } = new CosmosResponseMessageHeaders();

        /// <summary>
        /// Gets the original request message
        /// </summary>
        public virtual CosmosRequestMessage RequestMessage { get; internal set; }

        /// <summary>
        /// Gets the internal error object.
        /// </summary>
        internal virtual Error Error { private get; set; }

        private bool _disposed;

        private Stream _content;

        /// <summary>
        /// Asserts if the current <see cref="HttpStatusCode"/> is a success.
        /// </summary>
        public virtual bool IsSuccessStatusCode => ((int)this.StatusCode >= 200) && ((int)this.StatusCode <= 299);

        /// <summary>
        /// Checks if the current <see cref="CosmosResponseMessage"/> has a successful status code, otherwise, throws.
        /// </summary>
        /// <exception cref="HttpRequestException"></exception>
        /// <returns>The current <see cref="CosmosResponseMessage"/>.</returns>
        public virtual CosmosResponseMessage EnsureSuccessStatusCode()
        {
            if (!this.IsSuccessStatusCode)
            {
                string message = $"Response status code does not indicate success: {(int)this.StatusCode} Substatus: {(int)this.Headers.SubStatusCode} Reason: ({this.ErrorMessage}).";

                throw new CosmosException(
                        this,
                        message,
                        this.Error);
            }

            return this;
        }

        /// <summary>
        /// Disposes the current <see cref="CosmosResponseMessage"/>.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal string GetResourceAddress()
        {

            string resourceLink = this.RequestMessage?.RequestUri.OriginalString;
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
            if (disposing && !this._disposed)
            {
                this._disposed = true;
                if (this._content != null)
                {
                    this._content.Dispose();
                    this._content = null;
                }

                if (this.RequestMessage != null)
                {
                    this.RequestMessage.Dispose();
                    this.RequestMessage = null;
                }
            }
        }

        private void CheckDisposed()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(this.GetType().ToString());
            }
        }
    }
}