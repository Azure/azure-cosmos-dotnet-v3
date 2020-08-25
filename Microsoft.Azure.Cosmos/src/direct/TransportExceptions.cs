//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    internal static class TransportExceptions
    {
        internal static GoneException GetGoneException(
            Uri targetAddress, Guid activityId, Exception inner = null)
        {
            Trace.CorrelationManager.ActivityId = activityId;

            GoneException ex;
            if (inner == null)
            {
                if (RntbdConnection.AddSourceIpAddressInNetworkExceptionMessage)
                {
                    ex = new GoneException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.Gone),
                        inner,
                            targetAddress,
                        RntbdConnection.LocalIpv4Address);
                }
                else
                {
                    ex = new GoneException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.Gone),
                        inner,
                        targetAddress);
                }
            }
            else
            {
                if (RntbdConnection.AddSourceIpAddressInNetworkExceptionMessage)
                {
                    ex = new GoneException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.Gone),
                        inner,
                        targetAddress,
                        RntbdConnection.LocalIpv4Address);
                }
                else
                {
                    ex = new GoneException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.Gone),
                        inner,
                        targetAddress);
                }
            }

            ex.Headers.Set(HttpConstants.HttpHeaders.ActivityId, activityId.ToString());
            return ex;
        }

        internal static RequestTimeoutException GetRequestTimeoutException(
            Uri targetAddress, Guid activityId, Exception inner = null)
        {
            Trace.CorrelationManager.ActivityId = activityId;
            RequestTimeoutException timeoutException;

            if (inner == null)
            {
                if (RntbdConnection.AddSourceIpAddressInNetworkExceptionMessage)
                {
                    timeoutException = new RequestTimeoutException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.RequestTimeout),
                        inner,
                            targetAddress,
                        RntbdConnection.LocalIpv4Address);
                }
                else
                {
                    timeoutException = new RequestTimeoutException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.RequestTimeout),
                        inner,
                        targetAddress);
                }
            }
            else
            {
                if (RntbdConnection.AddSourceIpAddressInNetworkExceptionMessage)
                {
                    timeoutException = new RequestTimeoutException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.RequestTimeout),
                        inner,
                            targetAddress,
                        RntbdConnection.LocalIpv4Address);
                }
                else
                {
                    timeoutException = new RequestTimeoutException(
                        string.Format(CultureInfo.CurrentUICulture,
                            RMResources.ExceptionMessage,
                            RMResources.RequestTimeout),
                        inner,
                        targetAddress);
                }
            }

            timeoutException.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
            return timeoutException;
        }

        internal static ServiceUnavailableException GetServiceUnavailableException(
            Uri targetAddress, Guid activityId, Exception inner = null)
        {
            Trace.CorrelationManager.ActivityId = activityId;
            ServiceUnavailableException serviceUnavailableException;

            if (inner == null)
            {
                serviceUnavailableException = new ServiceUnavailableException(
                    string.Format(CultureInfo.CurrentUICulture,
                        RMResources.ExceptionMessage,
                        RMResources.ChannelClosed),
                    targetAddress);
            }
            else
            {
                serviceUnavailableException = new ServiceUnavailableException(
                    string.Format(CultureInfo.CurrentUICulture,
                        RMResources.ExceptionMessage,
                        RMResources.ChannelClosed),
                    inner,
                    targetAddress);
            }

            serviceUnavailableException.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
            return serviceUnavailableException;
        }

        internal static InternalServerErrorException GetInternalServerErrorException(
            Uri targetAddress, Guid activityId, Exception inner = null)
        {
            Trace.CorrelationManager.ActivityId = activityId;
            InternalServerErrorException internalServerErrorException;

            if (inner == null)
            {
                internalServerErrorException = new InternalServerErrorException(
                    string.Format(CultureInfo.CurrentUICulture,
                        RMResources.ExceptionMessage,
                        RMResources.ChannelClosed),
                    targetAddress);
            }
            else
            {
                internalServerErrorException = new InternalServerErrorException(
                    string.Format(CultureInfo.CurrentUICulture,
                        RMResources.ExceptionMessage,
                        RMResources.ChannelClosed),
                    inner,
                    targetAddress);
            }

            internalServerErrorException.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
            return internalServerErrorException;
        }

        internal static InternalServerErrorException GetInternalServerErrorException(
            Uri targetAddress, string exceptionMessage)
        {
            InternalServerErrorException exception = new InternalServerErrorException(
                string.Format(
                    CultureInfo.CurrentUICulture,
                    RMResources.ExceptionMessage,
                    exceptionMessage),
                targetAddress);
            exception.Headers.Add(HttpConstants.HttpHeaders.RequestValidationFailure, "1");
            return exception;
        }
    }
}