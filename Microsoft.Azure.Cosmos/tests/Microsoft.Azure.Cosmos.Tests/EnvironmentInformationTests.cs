//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EnvironmentInformationTests
    {
        [TestMethod]
        public void ClientVersionIsNotNull()
        {
            EnvironmentInformation envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.ClientVersion);

            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            Assert.AreEqual($"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}", envInfo.ClientVersion, "Version format differs");
        }

        [TestMethod]
        public void ProcessArchitectureIsNotNull()
        {
            EnvironmentInformation envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.ProcessArchitecture);
        }

        [TestMethod]
        public void FrameworkIsNotNull()
        {
            EnvironmentInformation envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.RuntimeFramework);
        }
    }
}