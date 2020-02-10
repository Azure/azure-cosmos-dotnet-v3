//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
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
            this.DiagnosticsContext = CosmosDiagnosticsContext.Create();
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
            this.ErrorMessage = errorMessage;
            this.Headers = new Headers();
            this.DiagnosticsContext = requestMessage?.DiagnosticsContext ?? CosmosDiagnosticsContext.Create();
        }

        /// <summary>
        /// Create a <see cref="ResponseMessage"/>
        /// </summary>
        /// <param name="statusCode">The HttpStatusCode of the response</param>
        /// <param name="requestMessage">The <see cref="Cosmos.RequestMessage"/> object</param>
        /// <param name="errorMessage">The reason for failures if any.</param>
        /// <param name="error">The inner error object</param>
        /// <param name="headers">The headers for the response.</param>
        /// <param name="diagnostics">The diagnostics for the request</param>
        internal ResponseMessage(
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            string errorMessage,
            Error error,
            Headers headers,
            CosmosDiagnosticsContext diagnostics)
        {
            this.StatusCode = statusCode;
            this.RequestMessage = requestMessage;
            this.ErrorMessage = errorMessage;
            this.Error = error;
            this.Headers = headers ?? new Headers();
            this.DiagnosticsContext = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
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
        public virtual string ErrorMessage { get; internal set; }

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
        public virtual CosmosDiagnostics Diagnostics => this.DiagnosticsContext;

        internal CosmosDiagnosticsContext DiagnosticsContext { get; }

        /// <summary>
        /// Gets the internal error object.
        /// </summary>
        internal virtual Error Error { get; set; }

        private bool disposed;

        private Stream content;

        /// <summary>
        /// Asserts if the current <see cref="HttpStatusCode"/> is a success.
        /// </summary>
        public virtual bool IsSuccessStatusCode => ((int)this.StatusCode >= 200) && ((int)this.StatusCode <= 299);

        /// <summary>
        /// Checks if the current <see cref="ResponseMessage"/> has a successful status code, otherwise, throws.
        /// </summary>
        /// <exception cref="CosmosException">An instance of <see cref="CosmosException"/> representing the error state.</exception>
        /// <returns>The current <see cref="ResponseMessage"/>.</returns>
        public virtual ResponseMessage EnsureSuccessStatusCode()
        {
            if (!this.IsSuccessStatusCode)
            {
                this.EnsureErrorMessage();
                string message = $"Response status code does not indicate success: {(int)this.StatusCode} Substatus: {(int)this.Headers.SubStatusCode} Reason: ({this.ErrorMessage}).";

                throw new CosmosException(
                        this,
                        message,
                        this.Error);
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
            }
        }

        private void CheckDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().ToString());
            }
        }

        private void EnsureErrorMessage()
        {
            if (this.Error != null
                || !string.IsNullOrEmpty(this.ErrorMessage))
            {
                return;
            }

            if (this.content != null
                && this.content.CanRead)
            {
                try
                {
                    Error error = Documents.Resource.LoadFrom<Error>(this.content);
                    if (error != null)
                    {
                        // Error format is not consistent across modes
                        if (!string.IsNullOrEmpty(error.Message))
                        {
                            this.ErrorMessage = error.Message;
                        }
                        else
                        {
                            this.ErrorMessage = error.ToString();
                        }
                    }
                }
                catch (Newtonsoft.Json.JsonReaderException)
                {
                    // Content is not Json
                    this.content.Position = 0;
                    using (StreamReader streamReader = new StreamReader(this.content))
                    {
                        this.ErrorMessage = streamReader.ReadToEnd();
                    }
                }
            }
        }
    }
}