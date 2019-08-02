//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EnvironmentInformationTests
    {
        [TestMethod]
        public void ClientVersionIsNotNull()
        {
            var envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.ClientVersion);
        }

        [TestMethod]
        public void ProcessArchitectureIsNotNull()
        {
            var envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.ProcessArchitecture);
        }

        [TestMethod]
        public void FrameworkIsNotNull()
        {
            var envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.RuntimeFramework);
        }

        [TestMethod]
        public void ToStringContainsAll()
        {
            var envInfo = new EnvironmentInformation();
            var serialization = envInfo.ToString();
            Assert.IsTrue(serialization.Contains(envInfo.ClientVersion));
            Assert.IsTrue(serialization.Contains(envInfo.ProcessArchitecture));
            Assert.IsTrue(serialization.Contains(envInfo.RuntimeFramework));
        }
    }
}
