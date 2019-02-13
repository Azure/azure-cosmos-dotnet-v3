//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="JsonSerializable"/> class.
    /// </summary>
    [TestClass]
    public class IndexingPolicyTests
    {
        [TestMethod]
        public void Clone()
        {
            IndexingPolicy indexingPolicy = new IndexingPolicy()
            {
                Automatic = true,
                IncludedPaths = new Collection<IncludedPath>()
                {
                    new IncludedPath()
                    {
                        Path = "/*"
                    }
                },
                IndexingMode = IndexingMode.Consistent,
            };

            IndexingPolicy cloned = (IndexingPolicy)indexingPolicy.Clone();

            Assert.AreEqual(indexingPolicy.Automatic, cloned.Automatic);
            Assert.AreEqual(indexingPolicy.IndexingMode, cloned.IndexingMode);
            Assert.AreEqual(indexingPolicy.IncludedPaths[0].Path, cloned.IncludedPaths[0].Path);
        }
    }
}
