//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EnvironmentInformationTests
    {
        [TestMethod]
        public void ClientVersionIsNotNull()
        {
            var envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.ClientVersion);

            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            Assert.AreEqual($"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}", envInfo.ClientVersion, "Version format differs");
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
        public void ClientIdIsNotNull()
        {
            var envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.ClientId);
        }
    }
}
