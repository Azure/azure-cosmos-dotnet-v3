//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections;
    using System.Collections.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="PartitionKey"/> class.
    /// </summary>
    [TestClass]
    public class DirectPartitionKeyDefinitionTests
    {
        [TestMethod]
        public void TestNoneHashCodeToNotThrow()
        {
            PartitionKey.None.GetHashCode();
        }



        [TestMethod]
        [Owner("vichhabr")]
        public void TestPartitionKeyDefinitionEquivalence()
        {
            {
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Version = PartitionKeyDefinitionVersion.V1,
                    Paths = new Collection<string>() { "/id" }
                };

                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Version = PartitionKeyDefinitionVersion.V1,
                    Paths = new Collection<string>() { "/id" }
                };

                PKDefinitionEquivalenceTester(partitionKeyDefinition1, partitionKeyDefinition2, true);
            }

            {
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Version = PartitionKeyDefinitionVersion.V1,
                    Paths = new Collection<string>() { "/id" }
                };

                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new Collection<string>() { "/id" }
                };

                PKDefinitionEquivalenceTester(partitionKeyDefinition1, partitionKeyDefinition2, false);
            }

            {
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Range,
                    Version = PartitionKeyDefinitionVersion.V1,
                    Paths = new Collection<string>() { "/id" }
                };

                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Version = PartitionKeyDefinitionVersion.V1,
                    Paths = new Collection<string>() { "/id" }
                };

                PKDefinitionEquivalenceTester(partitionKeyDefinition1, partitionKeyDefinition2, false);
            }

            {
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Version = PartitionKeyDefinitionVersion.V1,
                    Paths = new Collection<string>() { "/id" }
                };

                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Version = PartitionKeyDefinitionVersion.V1,
                    Paths = new Collection<string>() { "/id2" }
                };

                PKDefinitionEquivalenceTester(partitionKeyDefinition1, partitionKeyDefinition2, false);
            }

            {
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new Collection<string>() { "/id", "/id2" }
                };

                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new Collection<string>() { "/id" }
                };

                PKDefinitionEquivalenceTester(partitionKeyDefinition1, partitionKeyDefinition2, false);
            }

            {
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new Collection<string>() { "/id", "/id2" }
                };

                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new Collection<string>() { "/id2", "/id" }
                };

                PKDefinitionEquivalenceTester(partitionKeyDefinition1, partitionKeyDefinition2, false);
            }

            {
                PartitionKeyDefinition partitionKeyDefinition1 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new Collection<string>() { "/id", "/id2" }
                };

                PartitionKeyDefinition partitionKeyDefinition2 = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new Collection<string>() { "/id", "/id2" }
                };

                PKDefinitionEquivalenceTester(partitionKeyDefinition1, partitionKeyDefinition2, true);
            }
        }

        private static void PKDefinitionEquivalenceTester(
            PartitionKeyDefinition partitionKeyDefinition1,
            PartitionKeyDefinition partitionKeyDefinition2,
            Boolean isExpectedToBeEqual)
        {
            Assert.AreEqual(isExpectedToBeEqual, PartitionKeyDefinition.AreEquivalent(partitionKeyDefinition1, partitionKeyDefinition2));
        }
    }
}