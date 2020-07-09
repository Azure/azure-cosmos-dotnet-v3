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
        [TestInitialize]
        public void Reset()
        {
            EnvironmentInformation.ResetCounter();
        }

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

        [TestMethod]
        public void ClientIdIsNotNull()
        {
            EnvironmentInformation envInfo = new EnvironmentInformation();
            Assert.IsNotNull(envInfo.ClientId);
        }

        [TestMethod]
        public void ClientIdIncrementsUpToMax()
        {
            // Max is 10
            const int max = 10;
            for (int i = 0; i < max + 5; i++)
            {
                EnvironmentInformation envInfo = new EnvironmentInformation();
                Assert.AreEqual(i > max ? max : i, int.Parse(envInfo.ClientId));
            }
        }

        [TestMethod]
        public async Task ClientIdIncrementsUpToMax_Concurrent()
        {
            const int max = 10;
            const int tasks = max + 5;
            List<int> expected = new List<int>(tasks);
            for (int i = 0; i < tasks; i++)
            {
                expected.Add(i > max ? max : i);
            }

            List<Task<int>> results = new List<Task<int>>(tasks);
            for (int i = 0; i < tasks; i++)
            {
                results.Add(this.CreateAndReturnClientId());
            }

            await Task.WhenAll(results);
            List<int> resultsInts = results.Select(r => r.Result).ToList();
            resultsInts.Sort();

            CollectionAssert.AreEqual(expected, resultsInts);
        }

        private Task<int> CreateAndReturnClientId()
        {
            EnvironmentInformation envInfo = new EnvironmentInformation();
            return Task.FromResult(int.Parse(envInfo.ClientId));
        }
    }
}
