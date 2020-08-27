//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Collections;
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;
    using System.Net.Sockets;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;

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
                string trafficType = isPrivateIpPacket ? "private endpoint" : "service endpoint";
                string clientInfo = string.Format(CultureInfo.InvariantCulture, RMResources.ClientVnetInfo, trafficType);
                result = new ForbiddenException(string.Format(CultureInfo.InvariantCulture, RMResources.ForbiddenClientIpAddress, clientInfo));
            }
            else
            {
                string clientInfo = string.Format(CultureInfo.InvariantCulture, RMResources.ClientPublicIpInfo, clientIpAddress.ToString());
                result = new ForbiddenException(string.Format(CultureInfo.InvariantCulture, RMResources.ForbiddenClientIpAddress, clientInfo));
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
