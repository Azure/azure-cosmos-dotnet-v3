//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// Event arguments on events raised after DocumentServiceResponse/HttpResponseMessage is received on ServerStoreModel or HttpRequestMessageHandler.
    /// </summary>
    internal sealed class ReceivedResponseEventArgs : EventArgs
    {
        public ReceivedResponseEventArgs(DocumentServiceRequest request, DocumentServiceResponse response)
        {
            this.DocumentServiceResponse = response;
            this.DocumentServiceRequest = request;
        }

        public ReceivedResponseEventArgs(HttpRequestMessage request, HttpResponseMessage response)
        {
            this.HttpResponse = response;
            this.HttpRequest = request;
        }

        /// <summary>
        /// The DocumentServiceResponse on which the RecievedResponse event is raised.
        /// </summary>
        public DocumentServiceResponse DocumentServiceResponse { get; }

        /// <summary>
        /// The HttpResponseMessage on which the RecievedResponse event is raised.
        /// </summary>
        public HttpResponseMessage HttpResponse { get; }

        /// <summary>
        /// The HttpRequestMessage on which corresponds to the response.
        /// </summary>
        public HttpRequestMessage HttpRequest { get; }

        /// <summary>
        /// The DocumentServiceRequest which yielded the response.
        /// </summary>
        public DocumentServiceRequest DocumentServiceRequest { get; }

        /// <summary>
        /// Checks if the SendingRequestEventArgs has HttpRespoonseMessage as its member.
        /// </summary>
        /// <remarks>Used to check if the message is HttpRespoonseMessage or DocumentServiceRequestMessage.</remarks>
        /// <returns>true if the message is HttpRespoonseMessage. otherwise, returns false.</returns>
        public bool IsHttpResponse()
        {
            return this.HttpResponse != null;
        }
    }
}