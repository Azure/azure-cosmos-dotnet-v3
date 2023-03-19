//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NetworkDataRecorderTest
    {
        [TestMethod]
        public void TestRecordWithErroredAndHighLatencyRequests()
        {
            NetworkDataRecorder recorder = new NetworkDataRecorder();
            
        }
    }
}
