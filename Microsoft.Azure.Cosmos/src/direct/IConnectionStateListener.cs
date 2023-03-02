//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// blabla.
    /// </summary>
    internal interface IConnectionStateListener
    {
        /// <summary>
        /// blabla.
        /// </summary>
        /// <param name="serverUri"></param>
        void OnPrepareCallEvent(TransportAddressUri serverUri);

        /// <summary>
        /// blabla.
        /// </summary>
        /// <param name="connectionEvent"></param>
        /// <param name="eventTime"></param>
        /// <param name="serverKey"></param>
        void OnConnectionEvent(ConnectionEvent connectionEvent, DateTime eventTime, ServerKey serverKey);
    }
}