//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosVirtualUnitTest
    {
        [TestMethod]
        [Ignore]
        public void VerifyAllPublicMembersAreVirtualUnitTesting()
        {
            // All of the public properties and methods should be virtual to allow users to 
            // create unit tests by mocking the different types. Data Contracts do not support mocking so exclude all types that end with Settings.
            IEnumerable<Type> allClasses = from t in Assembly.GetAssembly(typeof(CosmosClient)).GetTypes()
                                           where t.IsClass && t.Namespace == "Microsoft.Azure.Cosmos" && t.IsPublic && !t.IsAbstract && !t.IsSealed
                                           where !t.Name.EndsWith("Properties")
                                           select t;

            // Get the entire list to prevent running the test for each method/property
            List<Tuple<string, string>> nonVirtualPublic = new List<Tuple<string, string>>();
            foreach (Type publiClass in allClasses)
            {
                // DeclaredOnly flag gets only the properties declared in the current class. 
                // This ignores inherited properties to prevent duplicate findings.
                IEnumerable<Tuple<string, string>> allProps = publiClass.GetProperties(BindingFlags.DeclaredOnly)
                    .Where(x => !x.GetGetMethod().IsVirtual && x.GetGetMethod().IsPublic && !x.CanWrite)
                    .Select(x => new Tuple<string, string>(publiClass.FullName, x.Name));
                nonVirtualPublic.AddRange(allProps);

                IEnumerable<Tuple<string, string>> allMethods = publiClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(x => !x.IsVirtual && x.IsPublic && !x.IsSpecialName)
                    .Where(x => x.GetCustomAttribute<IgnoreForUnitTest>(false) == null)
                    .Select(x => new Tuple<string, string>(publiClass.FullName, x.Name));
                nonVirtualPublic.AddRange(allMethods);
            }

            Assert.AreEqual(0, nonVirtualPublic.Count,
                "The following methods and properties should be virtual to allow unit testing:" +
                string.Join(";", nonVirtualPublic.Select(x => $"Class:{x.Item1}; Member:{x.Item2}")));
        }

        [TestMethod]
        public void VerifyAllPublicClassesCanBeMocked()
        {
            // The following classes are public, but not meant to be mocked.
            HashSet<string> nonMockableClasses = new HashSet<string>()
            {
                "ChangeFeedStartFrom",
                "ChangeFeedMode"
            };

            // All of the public classes should not contain an internal abstract method
            // create unit tests by mocking the different types. Data Contracts do not support mocking so exclude all types that end with Settings.
            IEnumerable<Type> allClasses = from t in Assembly.GetAssembly(typeof(CosmosClient)).GetTypes()
                                           where
                                                t.IsClass &&
                                                t.Namespace == "Microsoft.Azure.Cosmos" &&
                                                t.IsPublic &&
                                                !nonMockableClasses.Contains(t.Name)
                                           select t;

            // Get the entire list to prevent running the test for each method/property
            List<Tuple<string, string>> nonVirtualPublic = new List<Tuple<string, string>>();
            foreach (Type publicClass in allClasses)
            {
                // DeclaredOnly flag gets only the properties declared in the current class. 
                // This ignores inherited properties to prevent duplicate findings.
                IEnumerable<Tuple<string, string>> allProps = publicClass.GetProperties(BindingFlags.DeclaredOnly)
                    .Where(x => x.GetGetMethod().IsAbstract && !x.GetGetMethod().IsPublic)
                    .Select(x => new Tuple<string, string>(publicClass.FullName, x.Name));
                nonVirtualPublic.AddRange(allProps);

                IEnumerable<Tuple<string, string>> allMethods = publicClass.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(x => x.IsAbstract && !x.IsPublic)
                    .Select(x => new Tuple<string, string>(publicClass.FullName, x.Name));
                nonVirtualPublic.AddRange(allMethods);
            }

            Assert.AreEqual(0, nonVirtualPublic.Count,
                "The following methods and properties should be virtual to allow mocking:" +
                string.Join(";", nonVirtualPublic.Select(x => $"Class:{x.Item1}; Member:{x.Item2}")));
        }

        [TestMethod]
        public async Task VerifyClientMock()
        {
            Mock<DatabaseResponse> mockDbResponse = new Mock<DatabaseResponse>();

            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.CreateDatabaseAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(mockDbResponse.Object));

            CosmosClient client = mockClient.Object;
            DatabaseResponse response = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Assert.IsTrue(object.ReferenceEquals(mockDbResponse.Object, response));
        }

        [TestMethod]
        public void VerifyInlineOverride()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(CosmosClient));
            IEnumerable<Type> allClasses = from t in assembly.GetTypes()
                                           where t.IsClass && !t.IsPublic && !t.IsAbstract
                                           where t.Namespace != null
                                           where t.Namespace.StartsWith("Microsoft.Azure.Cosmos") && t.Name.EndsWith("InlineCore")
                                           select t;

            List<Type> inlineClasses = allClasses.ToList();
            Assert.IsTrue(inlineClasses.Count >= 7);

            foreach (Type inlineType in inlineClasses)
            {
                string contractClassName = inlineType.FullName.Replace("InlineCore", string.Empty);
                Type contractClass = assembly.GetType(contractClassName);
                Assert.IsNotNull(contractClass);
                string[] allContractMethods = contractClass.GetMethods()
                    .Where(x => x.DeclaringType == contractClass && x.Name.EndsWith("Async"))
                    .Select(x => x.ToString())
                    .ToArray();

                HashSet<string> allInlineMethods = inlineType.GetMethods()
                    .Where(x => x.Name.EndsWith("Async") && x.DeclaringType == inlineType)
                    .Select(x => x.ToString())
                    .ToHashSet<string>();

                foreach (string contractMethod in allContractMethods)
                {
                    if (!allInlineMethods.Contains(contractMethod))
                    {
                        Assert.Fail($"The class: {inlineType.FullName} does not override method \"{contractMethod}\" in the inline class");
                    }
                }
            }
        }
    }
}