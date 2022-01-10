//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Sockets;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents.Collections;

    [Serializable]
    internal sealed class ForbiddenException : DocumentClientException
    {
        public ForbiddenException()
            : this(RMResources.Forbidden)
        {

        }

        public static ForbiddenException CreateWithClientIpAddress(IPAddress clientIpAddress, bool isPrivateIpPacket)
        {
            ForbiddenException result;

            // ipv6 is not customer-facing. do not embed it.
            if (clientIpAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                result = new ForbiddenException(isPrivateIpPacket ?
                    RMResources.ForbiddenPrivateEndpoint :
                    RMResources.ForbiddenServiceEndpoint);
            }
            else
            {
                result = new ForbiddenException(string.Format(
                    CultureInfo.InvariantCulture,
                    RMResources.ForbiddenPublicIpv4,
                    clientIpAddress.ToString()));

                result.ClientIpAddress = clientIpAddress;
            }

            return result;
        }

        public ForbiddenException(string message)
            : this(message, (Exception)null, null)
        {

        }

        public ForbiddenException(string message, HttpResponseHeaders headers, Uri requestUri = null)
            : this(message, null, headers, requestUri)
        {

        }

        public ForbiddenException(Exception innerException)
            : this(RMResources.Forbidden, innerException, null)
        {

        }

        public ForbiddenException(string message, Exception innerException)
            : this(message, innerException, null)
        {

        }

        public ForbiddenException(string message, SubStatusCodes subStatusCode)
            : base(message, HttpStatusCode.Forbidden, subStatusCode)
        {
            this.SetDescription();
        }

        public ForbiddenException(string message, INameValueCollection headers, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.Forbidden, requestUri)
        {
            this.SetDescription();
        }

        public ForbiddenException(string message, Exception innerException, HttpResponseHeaders headers, Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.Forbidden, requestUri)
        {
            this.SetDescription();
        }

        public ForbiddenException(string message, Exception innerException, INameValueCollection headers)
            : base(message, innerException, headers, HttpStatusCode.Forbidden)
        {
            this.SetDescription();
        }

        public IPAddress ClientIpAddress { get; private set; }

#if !NETSTANDARD16
        private ForbiddenException(SerializationInfo info, StreamingContext context)
            : base(info, context, HttpStatusCode.Forbidden)
        {
            this.SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.Forbidden;
        }
    }
}
