//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Fluent
{
    using System;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ConflictResolutionFluentDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse()
        {
            Mock<ContainerFluentDefinitionForCreate> mockContainerDefinition = new Mock<ContainerFluentDefinitionForCreate>();

            // LastWrite wins conflict resolution mode 
            {
                Action<ConflictResolutionPolicy> callback = (ConflictResolutionPolicy) =>
                {
                    Assert.IsNotNull(ConflictResolutionPolicy);
                    Assert.AreEqual(ConflictResolutionMode.LastWriterWins, ConflictResolutionPolicy.Mode);
                    Assert.AreEqual("/lww", ConflictResolutionPolicy.ConflictResolutionPath);
                };

                ConflictResolutionFluentDefinition conflictResolutionFluentDefinition = new ConflictResolutionFluentDefinition(
                    mockContainerDefinition.Object,
                    callback);

                conflictResolutionFluentDefinition
                    .WithLastWriterWinsResolution("/lww")
                    .Attach();
            }

            // Custom conflict resolution mode
            {
                Action<ConflictResolutionPolicy> callback = (ConflictResolutionPolicy) =>
                {
                    Assert.IsNotNull(ConflictResolutionPolicy);
                    Assert.AreEqual(ConflictResolutionMode.Custom, ConflictResolutionPolicy.Mode);
                    Assert.AreEqual("testsproc", ConflictResolutionPolicy.ConflictResolutionProcedure);
                };

                ConflictResolutionFluentDefinition conflictResolutionFluentDefinition = new ConflictResolutionFluentDefinition(
                    mockContainerDefinition.Object,
                    callback);

                conflictResolutionFluentDefinition
                    .WithCustomStoredProcedureResolution("testsproc")
                    .Attach();
            }
        }
    }
}
