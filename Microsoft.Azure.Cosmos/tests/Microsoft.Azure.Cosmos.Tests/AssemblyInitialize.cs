//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class AssemblyInitialize
    {
        [AssemblyInitialize]
        public static void Init()
        {
            DefaultTrace.InitEventListener();
        }
    }
}
