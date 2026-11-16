//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;

    internal interface IChannelDictionary
    {
        /// <summary>
        /// Creates or gets an instance of <see cref="LoadBalancingChannel"/> using the server's physical uri.
        /// </summary>
        /// <param name="requestUri">An instance of <see cref="Uri"/> containing the backend server URI.</param>
        /// <param name="localRegionRequest">A boolean flag indicating if the request is targeting the local region.</param>
        /// <returns>An instance of <see cref="IChannel"/> containing the <see cref="LoadBalancingChannel"/>.</returns>
        IChannel GetChannel(
            Uri requestUri,
            bool localRegionRequest);
    }
}