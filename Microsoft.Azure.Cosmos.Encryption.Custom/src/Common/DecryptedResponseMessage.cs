//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal sealed class DecryptedResponseMessage : ResponseMessage
    {
        private readonly ResponseMessage responseMessage;
        private Stream decryptedContent;
        private bool isDisposed = false;

        public DecryptedResponseMessage(
            ResponseMessage responseMessage,
            Stream decryptedContent)
        {
            this.responseMessage = responseMessage;
            this.decryptedContent = decryptedContent;
        }

        public override Stream Content
        {
            get => this.decryptedContent;
            set => this.decryptedContent = value;
        }

        public override string ContinuationToken => this.responseMessage.ContinuationToken;

        public override CosmosDiagnostics Diagnostics => this.responseMessage.Diagnostics;

        public override string ErrorMessage => this.responseMessage.ErrorMessage;

        public override Headers Headers => this.responseMessage.Headers;

        public override RequestMessage RequestMessage => this.responseMessage.RequestMessage;

        public override HttpStatusCode StatusCode => this.responseMessage.StatusCode;

        public override bool IsSuccessStatusCode => this.responseMessage.IsSuccessStatusCode;

        /// <summary>
        /// Releases a response that cannot be transferred to the caller after a processing failure.
        /// Cleanup errors are traced without replacing the processing exception.
        /// </summary>
#if NET8_0_OR_GREATER
        internal static async Task DisposeOnFailureAsync(
#else
        internal static Task DisposeOnFailureAsync(
#endif
            ResponseMessage responseMessage,
            Stream content,
            Stream decryptedContent = null)
        {
            if (decryptedContent != null && !ReferenceEquals(decryptedContent, content))
            {
                try
                {
#if NET8_0_OR_GREATER
                    await decryptedContent.DisposeAsync().ConfigureAwait(false);
#else
                    decryptedContent.Dispose();
#endif
                }
                catch (Exception cleanupFailure)
                {
                    TraceCleanupFailure(cleanupFailure);
                }
            }

            try
            {
                responseMessage.Dispose();
            }
            catch (Exception cleanupFailure)
            {
                TraceCleanupFailure(cleanupFailure);
                if (content != null)
                {
                    try
                    {
#if NET8_0_OR_GREATER
                        await content.DisposeAsync().ConfigureAwait(false);
#else
                        content.Dispose();
#endif
                    }
                    catch (Exception contentCleanupFailure)
                    {
                        TraceCleanupFailure(contentCleanupFailure);
                    }
                }

#if !NET8_0_OR_GREATER
                return Task.CompletedTask;
#endif
            }
        }

        private static void TraceCleanupFailure(Exception failure)
        {
            try
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Failed to release an encrypted feed response after processing failed ({0}).",
                    failure.GetType().Name);
            }
            catch (Exception)
            {
                // A caller-supplied trace listener must not replace the processing exception.
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.isDisposed = true;
                if (this.decryptedContent != null)
                {
                    this.decryptedContent.Dispose();
                    this.decryptedContent = null;
                }

                this.responseMessage?.Dispose();
            }
        }
    }
}
