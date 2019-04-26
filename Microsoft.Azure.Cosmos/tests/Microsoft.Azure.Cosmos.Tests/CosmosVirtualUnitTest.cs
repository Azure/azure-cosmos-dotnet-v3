//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    [TestClass]
    public class CosmosVirtualUnitTest
    {
        [TestMethod]
        public void VerifyAllPublicMembersAreVirtualUnitTesting()
        {
            // All of the public properties and methods should be virtual to allow users to 
            // create unit tests by mocking the different types.
            IEnumerable<Type> allClasses = from t in Assembly.GetAssembly(typeof(CosmosClient)).GetTypes()
                                           where t.IsClass && t.Namespace == "Microsoft.Azure.Cosmos" && t.IsPublic && !t.IsAbstract && !t.IsSealed
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
    }
}
