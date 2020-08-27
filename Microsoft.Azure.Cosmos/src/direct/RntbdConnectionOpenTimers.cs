//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    internal struct RntbdConnectionOpenTimers
    {
        public DateTimeOffset CreationTimestamp;
        public DateTimeOffset TcpConnectCompleteTimestamp;
        public DateTimeOffset SslHandshakeCompleteTimestamp;
        public DateTimeOffset RntbdHandshakeCompleteTimestamp;
    }
}
