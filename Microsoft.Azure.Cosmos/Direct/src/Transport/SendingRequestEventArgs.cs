//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net.Http;

    internal sealed class SendingRequestEventArgs : EventArgs
    {
        public SendingRequestEventArgs(DocumentServiceRequest request)
        {
            this.DocumentServiceRequest = request;
        }

        public SendingRequestEventArgs(HttpRequestMessage request)
        {
            this.HttpRequest = request;
        }

        /// <summary>
        /// The HttpRequestMessage on which the SendingRequest event is raised.
        /// </summary>
        public HttpRequestMessage HttpRequest { get; }

        /// <summary>
        /// The DocumentServiceRequest on which the SendingRequest event is raised.
        /// </summary>
        public DocumentServiceRequest DocumentServiceRequest { get; }

        /// <summary>
        /// Checks if the SendingRequestEventArgs has HttpRequestMessage as its member.
        /// </summary>
        /// <remarks>Used to check if the message is HttpRequestMessage or DocumentServiceRequestMessage.</remarks>
        /// <returns>true if the message is HttpRequestMessage. otherwise, returns false.</returns>
        public bool IsHttpRequest()
        {
            return this.HttpRequest != null;
        }
    }
}