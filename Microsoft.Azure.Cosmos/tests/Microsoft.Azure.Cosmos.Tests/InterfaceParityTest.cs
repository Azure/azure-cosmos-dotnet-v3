//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Test for <see cref="IDocumentClient"/> class.
    /// </summary>
    [TestClass]
    public class InterfaceParityTest
    {
        /// <summary>
        /// Verify method parity between <see cref="IDocumentClient"/> and <see cref="DocumentClient"/>
        /// </summary>
        [TestMethod]
        public void TestAllPublicMethodsExistInIDocumentClient()
        {
            string[] excludedMethods = new string[]
            {
                "OpenAsync", // exposed public methods.
                "TryGetCachedAccountProperties", // currently only internal
                "get_PartitionResolvers", "get_ResourceTokens", // Obsolete getters.
                "ToString", "Equals", "GetHashCode", "GetType", "get_httpClient"
            };

            // Get all public methods declared in DocumentClient and verify that they are a part of Interfaces implemented.
            MethodInfo[] existingDocClientMethods = typeof(DocumentClient).GetMethods(BindingFlags.Public | BindingFlags.Instance);

            Type docClientType = typeof(DocumentClient);
            Type[] interfaces = docClientType.GetInterfaces();

            foreach (MethodInfo info in existingDocClientMethods)
            {
                bool skipValidationForCurrentMethod = false;
                foreach (string methodName in excludedMethods)
                {
                    if (info.Name.Equals(methodName))
                    {
                        skipValidationForCurrentMethod = true;
                        break;
                    }
                }

                if (!skipValidationForCurrentMethod)
                {
                    bool isObsoleteMethod = false;
                    IEnumerable<Attribute> attribs = info.GetCustomAttributes();
                    foreach (Attribute attribute in attribs)
                    {
                        if (attribute is ObsoleteAttribute)
                        {
                            isObsoleteMethod = true;
                            break;
                        }
                    }

                    if (!isObsoleteMethod)
                    {
                        bool belongsToAnInterface = false;
                        for (int i = 0; i < interfaces.Length; i++)
                        {
                            InterfaceMapping map = docClientType.GetInterfaceMap(interfaces[i]);
                            int index = Array.IndexOf(map.TargetMethods, info);
                            if (index >= 0)
                            {
                                belongsToAnInterface = true;
                                break;
                            }
                        }

                        Assert.IsTrue(belongsToAnInterface);
                    }
                }
            }
        }
    }
}