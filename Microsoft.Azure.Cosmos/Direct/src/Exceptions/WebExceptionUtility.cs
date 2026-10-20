namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    internal static class WebExceptionUtility
    {
        public static bool IsWebExceptionRetriable(Exception ex)
        {
            Exception iterator = ex;

            while (iterator != null)
            {
                if (WebExceptionUtility.IsWebExceptionRetriableInternal(iterator))
                {
                    return true;
                }

                iterator = iterator.InnerException;
            }

            return false;
        }

        private static bool IsWebExceptionRetriableInternal(Exception ex)
        {
            if (ex is WebException webEx)
            {
                return
                    webEx.Status == WebExceptionStatus.ConnectFailure ||
                    webEx.Status == WebExceptionStatus.NameResolutionFailure ||
                    webEx.Status == WebExceptionStatus.ProxyNameResolutionFailure ||
                    webEx.Status == WebExceptionStatus.SecureChannelFailure ||
                    webEx.Status == WebExceptionStatus.TrustFailure;
            }

            if (ex is SocketException socketEx)
            {
                return
                    socketEx.SocketErrorCode == SocketError.HostNotFound ||
                    socketEx.SocketErrorCode == SocketError.TimedOut ||
                    socketEx.SocketErrorCode == SocketError.TryAgain ||
                    socketEx.SocketErrorCode == SocketError.NoData;
            }

            return false;
        }
    }
}