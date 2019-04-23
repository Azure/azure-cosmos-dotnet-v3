//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosContainerSettingsTests
    {
        [TestMethod]
        public void DefaultSerialization()
        {
            CosmosContainerSettings containerSettings =
                new CosmosContainerSettings("TestContainer", "/partitionKey")
                .WithIncludeIndexPath("/includepath1")
                .WithIncludeIndexPath("/includepath2")
                .WithExcludeIndexPath("/excludepath1")
                .WithExcludeIndexPath("/excludepath2")
                .WithCompositeIndex("/compPath1", "/compPath2")
                .WithCompositeIndex(CompositePathDefinition.Create("/property1", CompositePathSortOrder.Descending),
                    CompositePathDefinition.Create("/property2", CompositePathSortOrder.Descending))
                .WithUniqueKey("/uniqueueKey1", "/uniqueueKey2")
                .WithSpatialIndex("/spatialPath", SpatialType.Point);
        }

        [TestMethod]
        public void V2Way()
        {
            IndexingPolicy ip = new IndexingPolicy();
            ip.IncludedPaths.Add(new IncludedPath() { Path = "/includepath1", Indexes = CosmosContainerSettings.DefaultIndexes});
            ip.ExcludedPaths.Add(new ExcludedPath() { Path = "/excludepath1" });

            Collection<CompositePathDefinition> compositePath = new Collection<CompositePathDefinition>();
            compositePath.Add(new CompositePathDefinition() { Path = "/compositepath1", Order = CompositePathSortOrder.Ascending });
            compositePath.Add(new CompositePathDefinition() { Path = "/compositepath2", Order = CompositePathSortOrder.Ascending });
            ip.CompositeIndexes.Add(compositePath);

            SpatialIndexDefinition spatialSpec = new SpatialIndexDefinition();
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

        [TestMethod]
        public void ValidationTests()
        {
            CosmosContainerSettings containerSettings =
                new CosmosContainerSettings("TestContainer", "/partitionKey");

            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithIncludeIndexPath(null));
            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithIncludeIndexPath(string.Empty));

            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithExcludeIndexPath(null));
            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithExcludeIndexPath(string.Empty));

            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => containerSettings.WithSpatialIndex(null));
            CosmosContainerSettingsTests.AssertException<ArgumentNullException>(() => CompositePathDefinition.Create("ABC", CompositePathSortOrder.Descending));

            CosmosContainerSettingsTests.AssertException<ArgumentOutOfRangeException>(() => containerSettings.WithCompositeIndex(string.Empty));
            CosmosContainerSettingsTests.AssertException<ArgumentOutOfRangeException>(() => containerSettings.WithCompositeIndex("abc", null));
            CosmosContainerSettingsTests.AssertException<ArgumentOutOfRangeException>(() => containerSettings.WithCompositeIndex(null, CompositePathDefinition.Create("ABC", CompositePathSortOrder.Descending)));
        }

        public static void AssertException<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch(T)
            {
            }
        }
    }
}
