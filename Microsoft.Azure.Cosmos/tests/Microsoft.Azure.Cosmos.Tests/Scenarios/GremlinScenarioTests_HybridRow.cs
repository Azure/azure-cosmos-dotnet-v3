//-----------------------------------------------------------------------
// <copyright file="GremlinScenarioTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scenarios
{
    using System;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for CosmosDB Gremlin use case scenarios of CosmosElement and JsonNavigator interfaces using Text serialization.
    /// </summary>
    [TestClass]
    public sealed class GremlinScenarioTests_HybridRow : GremlinScenarioTests
    {
        internal override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.HybridRow;

        [TestMethod]
        public override void SerializeAndDeserializeGremlinEdgeDocument()
        {
            Assert.ThrowsException<ArgumentException>(
                () => this.SerializeAndDeserializeEdgeDocumentTest(this.SerializationFormat));
        }

        [TestMethod]
        public override void SerializeAndDeserializeGremlinVertexDocument()
        {
            Assert.ThrowsException<ArgumentException>(
                () => this.SerializeAndDeserializeVertexDocumentTest(this.SerializationFormat));
        }

        [TestMethod]
        public override void DeserializeModifyAndSerializeVertexDocument()
        {
            Assert.ThrowsException<ArgumentException>(
                () => this.DeserializeModifyAndSerializeVertexDocumentTest(this.SerializationFormat));
        }

        [TestMethod]
        public override void GetCosmosElementsFromQueryResponse()
        {
            Assert.ThrowsException<ArgumentException>(
                () => this.GetCosmosElementsFromQueryResponseTest(this.SerializationFormat));
        }

        [TestMethod]
        public override void GetDeserializedObjectsFromQueryResponse()
        {
            Assert.ThrowsException<ArgumentException>(
                () => this.GetDeserializedObjectsFromQueryResponseTest(this.SerializationFormat));
        }
    }
}