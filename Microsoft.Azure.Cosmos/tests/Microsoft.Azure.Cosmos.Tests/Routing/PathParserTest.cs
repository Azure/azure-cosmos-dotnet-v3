//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests for <see cref="PathParser"/>.
    /// </summary>
    [TestClass]
    public class PathParserTest
    {
        /// <summary>
        /// Baseline Test
        /// </summary>
        [TestMethod]
        public void BaselineTest()
        {
            foreach (byte[] test in new[]
            {
                Properties.Resources.BaselineTest_PathParser,
                Properties.Resources.BaselineTest_PathParser_Extra,
            })
            {
                using (Stream stream = new MemoryStream(test))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        List<TestData> testData = JsonConvert.DeserializeObject<List<TestData>>(reader.ReadToEnd());

                        foreach (TestData data in testData)
                        {
                            Assert.IsTrue(Enumerable.SequenceEqual(PathParser.GetPathParts(data.Path), data.Parts));
                        }
                    }
                }
            }
        }

        private struct TestData
        {
            [JsonProperty(PropertyName = "path")]
            public string Path { get; set; }

            [JsonProperty(PropertyName = "parts")]
            public List<string> Parts { get; set; }
        }
    }
}