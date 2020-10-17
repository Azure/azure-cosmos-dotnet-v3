//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Security;

    /// <summary>
    /// Utility for converting string to SecureString.
    /// </summary>
    internal static class SecureStringUtility
    {
        /// <summary>
        /// Converts a unsecure string into a SecureString.
        /// </summary>
        /// <param name="unsecureStr">the string to convert.</param>
        /// <returns>the resulting SecureString</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Disposable object returned by method")]
        public static SecureString ConvertToSecureString(string unsecureStr)
        {
            if (string.IsNullOrEmpty(unsecureStr))
            {
                throw new ArgumentNullException(nameof(unsecureStr));
            }

            SecureString secureStr = new SecureString();
            foreach (char c in unsecureStr.ToCharArray())
            {
                secureStr.AppendChar(c);
            }
            return secureStr;
        }
    }
}
