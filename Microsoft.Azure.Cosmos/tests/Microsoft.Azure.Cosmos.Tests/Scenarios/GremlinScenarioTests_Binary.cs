//-----------------------------------------------------------------------
// <copyright file="GremlinScenarioTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scenarios
{
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for CosmosDB Gremlin use case scenarios of CosmosElement and JsonNavigator interfaces using Text serialization.
    /// </summary>
    [TestClass]
    public sealed class GremlinScenarioTests_Binary : GremlinScenarioTests
    {
        internal override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Binary;
    }
}