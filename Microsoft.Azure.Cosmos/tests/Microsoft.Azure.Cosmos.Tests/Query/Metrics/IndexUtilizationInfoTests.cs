//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Metrics
{
    using System;
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using System.Collections.Generic;

    [TestClass]
    public class IndexUtilizationInfoTests
    {
        private static readonly IndexUtilizationData data = new IndexUtilizationData(
                nameof(IndexUtilizationData.FilterExpression),
                nameof(IndexUtilizationData.IndexDocumentExpression),
                default,
                default);

        internal static readonly IndexUtilizationInfo MockIndexUtilizationInfo = new IndexUtilizationInfo(
            new List<IndexUtilizationData>() { data },
            new List<IndexUtilizationData>() { data });

        [TestMethod]
        public void TestParse()
        {
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("eyJVdGlsaXplZEluZGV4ZXMiOlt7IkZpbHRlckV4cHJlc3Npb24iOiIoUk9PVC5leWVDb2xvciA9IFwiYmx1ZVwiKSIsIkluZGV4U3BlYyI6IlwvZXllQ29sb3JcLz8iLCJGaWx0ZXJQcmVjaXNlU2V0Ijp0cnVlLCJJbmRleFByZWNpc2VTZXQiOnRydWV9LHsiRmlsdGVyRXhwcmVzc2lvbiI6IihST09ULmFnZSA9IDI3KSIsIkluZGV4U3BlYyI6IlwvYWdlXC8/IiwiRmlsdGVyUHJlY2lzZVNldCI6dHJ1ZSwiSW5kZXhQcmVjaXNlU2V0Ijp0cnVlfSx7IkZpbHRlckV4cHJlc3Npb24iOiIoUk9PVC5pZCA+IDApIiwiSW5kZXhTcGVjIjoiXC9pZFwvPyIsIkZpbHRlclByZWNpc2VTZXQiOnRydWUsIkluZGV4UHJlY2lzZVNldCI6dHJ1ZX0seyJGaWx0ZXJFeHByZXNzaW9uIjoiSXNEZWZpbmVkKFJPT1QuZmlyc3ROYW1lKSIsIkluZGV4U3BlYyI6IlwvZmlyc3ROYW1lXC8/IiwiRmlsdGVyUHJlY2lzZVNldCI6ZmFsc2UsIkluZGV4UHJlY2lzZVNldCI6dHJ1ZX0seyJGaWx0ZXJFeHByZXNzaW9uIjoiSXNEZWZpbmVkKFJPT1QubGFzdE5hbWUpIiwiSW5kZXhTcGVjIjoiXC9sYXN0TmFtZVwvPyIsIkZpbHRlclByZWNpc2VTZXQiOmZhbHNlLCJJbmRleFByZWNpc2VTZXQiOnRydWV9LHsiRmlsdGVyRXhwcmVzc2lvbiI6IihST09ULmdlbmRlciA9IFwiZmVtYWxlXCIpIiwiSW5kZXhTcGVjIjoiXC9nZW5kZXJcLz8iLCJGaWx0ZXJQcmVjaXNlU2V0Ijp0cnVlLCJJbmRleFByZWNpc2VTZXQiOnRydWV9LHsiRmlsdGVyRXhwcmVzc2lvbiI6IihST09ULnNhbGFyeSA+IDE4NjAwMCkiLCJJbmRleFNwZWMiOiJcL3NhbGFyeVwvPyIsIkZpbHRlclByZWNpc2VTZXQiOnRydWUsIkluZGV4UHJlY2lzZVNldCI6dHJ1ZX0seyJGaWx0ZXJFeHByZXNzaW9uIjoiKFJPT1QuY29tcGFueSA9IFwiRmFjZWJvb2tcIikiLCJJbmRleFNwZWMiOiJcL2NvbXBhbnlcLz8iLCJGaWx0ZXJQcmVjaXNlU2V0Ijp0cnVlLCJJbmRleFByZWNpc2VTZXQiOnRydWV9XSwiUG90ZW50aWFsSW5kZXhlcyI6W119",
                out IndexUtilizationInfo parsedInfo));
            Assert.IsNotNull(parsedInfo);
        }

        [TestMethod]
        public void TestParseEmptyString()
        {
            Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString(string.Empty,
                out IndexUtilizationInfo parsedInfo));
            Assert.AreEqual(IndexUtilizationInfo.Empty, parsedInfo);
        }

        [TestMethod]
        public void TestAccumulator()
        {
            IndexUtilizationInfo.Accumulator accumulator = new IndexUtilizationInfo.Accumulator();
            accumulator = accumulator.Accumulate(MockIndexUtilizationInfo);
            accumulator = accumulator.Accumulate(MockIndexUtilizationInfo);

            IndexUtilizationInfo doubleInfo = IndexUtilizationInfo.Accumulator.ToIndexUtilizationInfo(accumulator);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.PotentialIndexes.Count, doubleInfo.PotentialIndexes.Count);
            Assert.AreEqual(2 * MockIndexUtilizationInfo.UtilizedIndexes.Count, doubleInfo.UtilizedIndexes.Count);
        }
    }
}
