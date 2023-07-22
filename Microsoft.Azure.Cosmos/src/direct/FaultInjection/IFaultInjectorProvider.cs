//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using System;
    using Microsoft.Azure.Documents.Rntbd;

    internal interface IFaultInjectorProvider
    {
        IRntbdServerErrorInjector GetRntbdServerErrorInjector();

        void RegisterConnectionErrorInjector(ChannelDictionary channelDictionary);
    }
}
