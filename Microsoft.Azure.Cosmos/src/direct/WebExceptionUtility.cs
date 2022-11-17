namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net;

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
            WebException webEx = ex as WebException;
            if (webEx == null)
            {
                return false;
            }

            if (webEx.Status == WebExceptionStatus.ConnectFailure ||
                webEx.Status == WebExceptionStatus.NameResolutionFailure ||
                webEx.Status == WebExceptionStatus.ProxyNameResolutionFailure ||
                webEx.Status == WebExceptionStatus.SecureChannelFailure ||
                webEx.Status == WebExceptionStatus.TrustFailure)
            {
                return true;
            }

            return false;
        }
    }
}