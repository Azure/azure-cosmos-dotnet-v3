//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Sockets;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Rntbd;

    [Serializable]
    internal sealed class TransportException : Exception
    {
        private static readonly Lazy<Dictionary<TransportErrorCode, string>> lazyMessageMap =
            new Lazy<Dictionary<TransportErrorCode, string>>(
                TransportException.GetErrorTextMap,
                LazyThreadSafetyMode.ExecutionAndPublication);

        private static TransportExceptionCounters transportExceptionCounters = new TransportExceptionCounters();

        private readonly object mutex = new object();
        private CpuLoadHistory cpuHistory;  // Guarded by mutex.

        public TransportException(
            TransportErrorCode errorCode,
            Exception innerException,
            Guid activityId,
            Uri requestUri,
            string sourceDescription,
            bool userPayload,
            bool payloadSent) :
            base(TransportException.LoadMessage(errorCode), innerException)
        {
            this.Timestamp = DateTime.UtcNow;
            this.ErrorCode = errorCode;
            this.ActivityId = activityId;
            this.RequestUri = requestUri;
            this.Source = sourceDescription;
            this.UserRequestSent = TransportException.IsUserRequestSent(
                errorCode, userPayload, payloadSent);
            TransportException.UpdateCounters(requestUri, innerException);
        }

        public override string Message
        {
            get
            {
                string baseError;
                Exception baseException = this.GetBaseException();
                {
                    SocketException socketException = baseException as SocketException;
                    if (socketException != null)
                    {
                        baseError = string.Format(
                            CultureInfo.InvariantCulture,
                            "socket error {0} [0x{1:X8}]",
                            socketException.SocketErrorCode,
                            (int)socketException.SocketErrorCode);
                    }
                    else
                    {
                        Win32Exception win32Exception = baseException as Win32Exception;
                        if (win32Exception != null)
                        {
                            baseError = string.Format(
                                CultureInfo.InvariantCulture,
                                "Windows error 0x{0:X8}",
                                win32Exception.NativeErrorCode);
                        }
                        else
                        {
                            baseError = string.Format(
                                CultureInfo.InvariantCulture,
                                "HRESULT 0x{0:X8}",
                                baseException.HResult);
                        }
                    }
                }
                string loadHistoryText = "not available";
                CpuLoadHistory loadHistory = this.CpuHistory;
                if (loadHistory != null)
                {
                    loadHistoryText = loadHistory.ToString();
                }
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} (Time: {1:o}, activity ID: {2}, error code: {3} [0x{4:X4}], " +
                    "base error: {5}, URI: {6}, connection: {7}, payload sent: {8}, " +
                    "CPU history: {9}, CPU count: {10})",
                    base.Message,
                    this.Timestamp,
                    this.ActivityId,
                    this.ErrorCode, (int)this.ErrorCode,
                    baseError,
                    this.RequestUri,
                    this.Source,
                    this.UserRequestSent,
                    loadHistoryText,
                    Environment.ProcessorCount);
            }
        }

        public DateTime Timestamp { get; private set; }

        public DateTime? RequestStartTime { get; set; }

        public DateTime? RequestEndTime { get; set; }

        public ResourceType ResourceType { get; set; }

        public OperationType OperationType { get; set; }

        public TransportErrorCode ErrorCode { get; private set; }

        public Guid ActivityId { get; private set; }

        public Uri RequestUri { get; private set; }

        public bool UserRequestSent { get; private set; }

        public bool IsClientCpuOverloaded
        {
            get
            {
                CpuLoadHistory loadHistory = this.CpuHistory;
                if (loadHistory == null)
                {
                    return false;
                }
                return loadHistory.IsCpuOverloaded;
            }
        }

        public static bool IsTimeout(TransportErrorCode errorCode)
        {
            return
                (errorCode == TransportErrorCode.ChannelOpenTimeout) ||
                (errorCode == TransportErrorCode.DnsResolutionTimeout) ||
                (errorCode == TransportErrorCode.ConnectTimeout) ||
                (errorCode == TransportErrorCode.SslNegotiationTimeout) ||
                (errorCode == TransportErrorCode.TransportNegotiationTimeout) ||
                (errorCode == TransportErrorCode.RequestTimeout) ||
                (errorCode == TransportErrorCode.SendLockTimeout) ||
                (errorCode == TransportErrorCode.SendTimeout) ||
                (errorCode == TransportErrorCode.ReceiveTimeout);
        }

        internal void SetCpuLoad(CpuLoadHistory cpuHistory)
        {
            lock (this.mutex)
            {
                // A single TransportException can get dispatched to multiple threads
                // that are awaiting a common Task. Ensure only one thread sets the
                // CPU history.
                if (this.cpuHistory == null)
                {
                    this.cpuHistory = cpuHistory;
                }
            }
        }

        private CpuLoadHistory CpuHistory
        {
            get
            {
                lock (this.mutex)
                {
                    return this.cpuHistory;
                }
            }
        }

        private static bool IsUserRequestSent(
            TransportErrorCode errorCode, bool userPayload, bool payloadSent)
        {
            // It doesn't matter what the error was, if it didn't occur while
            // handling the user's request (e.g. if it occurred while doing SSL
            // or RNTBD negotiation).
            if (!userPayload)
            {
                return false;
            }
            return payloadSent || TransportException.IsTimeout(errorCode);
        }

        private static string LoadMessage(TransportErrorCode errorCode)
        {
            return string.Format(
                CultureInfo.CurrentUICulture,
                RMResources.TransportExceptionMessage,
                TransportException.GetErrorText(errorCode));
        }

        private static string GetErrorText(TransportErrorCode errorCode)
        {
            string errorText;
            if (TransportException.lazyMessageMap.Value.TryGetValue(
                errorCode, out errorText))
            {
                return errorText;
            }
            Debug.Assert(
                false,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Error code {0} not found in the error text map. Please update {1}",
                    errorCode, nameof(GetErrorTextMap)));
            return string.Format(CultureInfo.InvariantCulture, "{0}", errorCode);
        }

        private static Dictionary<TransportErrorCode, string> GetErrorTextMap()
        {
            return new Dictionary<TransportErrorCode, string>
            {
                { TransportErrorCode.ChannelMultiplexerClosed, RMResources.ChannelMultiplexerClosedTransportError },
                { TransportErrorCode.ChannelOpenFailed, RMResources.ChannelOpenFailedTransportError },
                { TransportErrorCode.ChannelOpenTimeout, RMResources.ChannelOpenTimeoutTransportError },
                { TransportErrorCode.ConnectFailed, RMResources.ConnectFailedTransportError },
                { TransportErrorCode.ConnectTimeout, RMResources.ConnectTimeoutTransportError },
                { TransportErrorCode.ConnectionBroken, RMResources.ConnectionBrokenTransportError },
                { TransportErrorCode.DnsResolutionFailed, RMResources.DnsResolutionFailedTransportError },
                { TransportErrorCode.DnsResolutionTimeout, RMResources.DnsResolutionTimeoutTransportError },
                { TransportErrorCode.ReceiveFailed, RMResources.ReceiveFailedTransportError },
                { TransportErrorCode.ReceiveStreamClosed, RMResources.ReceiveStreamClosedTransportError },
                { TransportErrorCode.ReceiveTimeout, RMResources.ReceiveTimeoutTransportError },
                { TransportErrorCode.RequestTimeout, RMResources.RequestTimeoutTransportError },
                { TransportErrorCode.SendFailed, RMResources.SendFailedTransportError },
                { TransportErrorCode.SendLockTimeout, RMResources.SendLockTimeoutTransportError },
                { TransportErrorCode.SendTimeout, RMResources.SendTimeoutTransportError },
                { TransportErrorCode.SslNegotiationFailed, RMResources.SslNegotiationFailedTransportError },
                { TransportErrorCode.SslNegotiationTimeout, RMResources.SslNegotiationTimeoutTransportError },
                { TransportErrorCode.TransportNegotiationTimeout, RMResources.TransportNegotiationTimeoutTransportError },
                { TransportErrorCode.Unknown, RMResources.UnknownTransportError },
            };
        }

        private static void UpdateCounters(Uri requestUri, Exception innerException)
        {
            const int SEC_E_DECRYPT_FAILURE = unchecked((int)0x80090330);

            if (innerException == null)
            {
                return;
            }
            if (innerException is TransportException)
            {
                return;
            }
            innerException = innerException.GetBaseException();
            SocketException socketException = innerException as SocketException;
            if (socketException != null)
            {
                switch (socketException.SocketErrorCode)
                {
                    case SocketError.NoBufferSpaceAvailable:
                        TransportException.transportExceptionCounters.IncrementEphemeralPortExhaustion();
                        break;
                }
            }
            else
            {
                Win32Exception win32Exception = innerException as Win32Exception;
                if (win32Exception != null)
                {
                    switch (win32Exception.NativeErrorCode)
                    {
                        // This code is fragile but it works for now.
                        // It's the best I could do given the quality of our dependencies:
                        // You'd expect Win32Exception to deal with Win32 error codes.
                        // However, it also accepts HRESULT - sort of. For this exception,
                        // Win32Exception.ErrorCode and Win32Exception.HResult
                        // return E_FAIL.
                        case SEC_E_DECRYPT_FAILURE:
                            DefaultTrace.TraceWarning(
                                "Decryption failure. Exception text: {0}. Native error code: 0x{1:X8}. Remote endpoint: {2}",
                                win32Exception.Message,
                                win32Exception.NativeErrorCode,
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}:{1}",
                                    requestUri.DnsSafeHost, requestUri.Port));
                            TransportException.transportExceptionCounters.IncrementDecryptionFailures();
                            break;
                    }
                }
            }
        }

        internal static void SetCounters(TransportExceptionCounters transportExceptionCounters)
        {
            if (transportExceptionCounters == null)
            {
                throw new ArgumentNullException(nameof(transportExceptionCounters));
            }
            TransportException.transportExceptionCounters = transportExceptionCounters;
        }
    }
}
