//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    // When adding a new code:
    // - Give it an explicit value. Do not rely on implicit values. Do not
    //   reuse values from older codes that might have been retired.
    // - Add a resource string.
    // - Update TransportException.IsTimeout if appropriate. By default, error
    //   codes are not treated as timeouts.
    // - Update TransportExceptionTests.
    //
    // When retiring a code, avoid removing it altogether. Rename "Foo" to
    // "ObsoleteFoo" instead.
    //
    // Do not re-number error codes.
    //
    // Avoid renaming error codes. Prefer adding new names with the same
    // value. This also prevents accidental indirect re-numbering.
    //
    // Keep this in sync with the documentation at
    // ${ENLISTMENT_ROOT}\docs\tsg\tsg914.md.
    internal enum TransportErrorCode
    {
        // This error code must never be used. It serves as a default
        // value. Its presence indicates that some code throws
        // TransportException but didn't set its code correctly.
        Unknown = 0,

        // Generic error code used when channel initialization fails.
        // Examine the inner exception to determine the cause of the error.
        ChannelOpenFailed = 1,

        // Generic error code that indicates creating a channel timed out.
        // It serves as a default value. Code that throws TransportException
        // must use a more specific code for connection establishment
        // timeouts.
        ChannelOpenTimeout = 2,

        DnsResolutionFailed = 3,
        DnsResolutionTimeout = 4,
        ConnectFailed = 5,
        ConnectTimeout = 6,
        SslNegotiationFailed = 7,
        SslNegotiationTimeout = 8,

        // Generic error code that indicates negotiating the RNTBD parameters
        // timed out. It serves as a default value. Code that throws
        // TransportException must use a more specific code for negotiation
        // timeouts.
        TransportNegotiationTimeout = 9,

        // Generic error code that indicates an RNTBD call timed out. It
        // serves as a default value. Code that throws TransportException
        // must use a more specific code for request timeouts.
        RequestTimeout = 10,

        // The RNTBD Dispatcher failed and no longer accepts new requests.
        ChannelMultiplexerClosed = 11,
        SendFailed = 12,
        SendLockTimeout = 13,
        SendTimeout = 14,
        ReceiveFailed = 15,
        ReceiveTimeout = 16,

        // The remote process closed the connection.
        ReceiveStreamClosed = 17,
        // The underlying connection is no longer usable.
        ConnectionBroken = 18,

        // Keep this in sync with the documentation at
        // ${ENLISTMENT_ROOT}\docs\tsg\tsg914.md.
        // Insert new values above this comment.
    }
}
