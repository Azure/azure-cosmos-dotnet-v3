//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using System;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Interface for FaultInjectorProvider
    /// </summary>
    internal interface IFaultInjectorProvider
    {
        /// <summary>
        /// Gets the providor's RNTBD server error injector
        /// </summary>
        /// <returns>the RNTBD server error injector.</returns>
        public IRntbdServerErrorInjector GetRntbdServerErrorInjector();

        /// <summary>
        /// Registers connection error injectors
        /// </summary>
        /// <param name="channelDictionary"></param>
        public void RegisterConnectionErrorInjector(ChannelDictionary channelDictionary);
    }
}
