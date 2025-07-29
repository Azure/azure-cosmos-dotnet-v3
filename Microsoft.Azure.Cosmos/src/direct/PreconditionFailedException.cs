﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    [Serializable]
    internal class PreconditionFailedException : DocumentClientException
    {
        public PreconditionFailedException()
            : this(RMResources.PreconditionFailed)
        {

        }

        public PreconditionFailedException(string message, SubStatusCodes? substatusCode = null)
            : this(message, (Exception)null, null, null, substatusCode)
        {

        }

        public PreconditionFailedException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public PreconditionFailedException(Exception innerException)
            : this(RMResources.PreconditionFailed, innerException, null)
        {

        }

        public PreconditionFailedException(string message, Exception innerException)
            : this(message, innerException, null)
        {

        }

        public PreconditionFailedException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.PreconditionFailed, requestUri)
        {
            SetDescription();
        }

        public PreconditionFailedException(
            string message, 
            Exception innerException, 
            HttpResponseHeaders headers, 
            Uri requestUri = null,
            SubStatusCodes? substatusCode = null)
            : base(message, innerException, headers, HttpStatusCode.PreconditionFailed, requestUri, substatusCode)
        {
            SetDescription();
        }

        [JsonConstructor]
        private PreconditionFailedException(SerializationInfo info, StreamingContext context) 
            : base(info, context, HttpStatusCode.PreconditionFailed)
        {
            SetDescription();
        }

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.PreconditionFailed;
        }
    }
}
