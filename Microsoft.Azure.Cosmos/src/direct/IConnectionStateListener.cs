//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    using Microsoft.Azure.Documents.Rntbd;

    internal interface IConnectionStateListener
    {
        void OnConnectionEvent(ConnectionEvent connectionEvent, DateTime eventTime, ServerKey serverKey);
    }
}