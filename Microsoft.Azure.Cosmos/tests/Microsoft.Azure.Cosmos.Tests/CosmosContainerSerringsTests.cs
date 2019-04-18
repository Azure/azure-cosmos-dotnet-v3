//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;

    [TestClass]
    public class CosmosContainerSerringsTests
    {
        [TestMethod]
        public void DefaultSerialization()
        {
            CosmosContainerSettings containerSettings =
                new CosmosContainerSettings("TestContainer", "/partitionKey")
                .IncludIndexPath("/includepath1")
                .IncludIndexPath("/includepath2")
                .ExcludeIndexPath("/excludepath1")
                .ExcludeIndexPath("/excludepath2")
                .IncludeCompositeIndex("/compPath1", "/compPath2")
                .IncludeUniqueKey("/uniqueueKey1", "/uniqueueKey2")
                .IncludeSpatialIndex("/spatialPath", SpatialType.Point);
        }

        [TestMethod]
        public void V2Way()
        {
            IndexingPolicy ip = new IndexingPolicy();
            ip.IncludedPaths.Add(new IncludedPath() { Path = "/includepath1", Indexes = CosmosContainerSettings.DefaultIndexes});
            ip.ExcludedPaths.Add(new ExcludedPath() { Path = "/excludepath1" });

            Collection<CompositePath> compositePath = new Collection<CompositePath>();
            compositePath.Add(new CompositePath() { Path = "/compositepath1", Order = CompositePathSortOrder.Ascending });
            compositePath.Add(new CompositePath() { Path = "/compositepath2", Order = CompositePathSortOrder.Ascending });
            ip.CompositeIndexes.Add(compositePath);

            SpatialSpec spatialSpec = new SpatialSpec();
            spatialSpec.Path = "/spatialpath1";
            spatialSpec.SpatialTypes = new Collection<SpatialType>();
            spatialSpec.SpatialTypes.Add(SpatialType.Point);
            ip.SpatialIndexes.Add(spatialSpec);

            UniqueKeyPolicy uniqueKeyPolicy = new UniqueKeyPolicy();
            UniqueKey uniqueKey = new UniqueKey();
            uniqueKey.Paths = new Collection<string>();
            uniqueKey.Paths.Add("/uniqueuekey1");
            uniqueKey.Paths.Add("/uniqueuekey2");
            uniqueKeyPolicy.UniqueKeys.Add(uniqueKey);

            CosmosContainerSettings testContainerSettings = new CosmosContainerSettings("TestContainer", "/partitionKey");
            testContainerSettings.IndexingPolicy = ip;
            testContainerSettings.UniqueKeyPolicy = uniqueKeyPolicy;
        }
    }
}
