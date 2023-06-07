//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using System;

    internal interface IFaultInjectorProvider
    {
        IRntbdServerErrorInjector GetRntbdServerErrorInjector();
    }
}
