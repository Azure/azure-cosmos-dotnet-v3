//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosLinqAggregateFunctionTest
    {
        [TestMethod]
        public async Task LinqAggregateFunctionsTest()
        {
            //Testing aggregates on int
            IList<int> intList = new List<int>
            {
                1,2,3,4,5
            };
            IQueryable<int> intQueryable = intList.AsQueryable();

            Assert.AreEqual(intQueryable.Count(), await intQueryable.CountAsync());
            Assert.AreEqual(intQueryable.Sum(), await intQueryable.SumAsync());
            Assert.AreEqual(intQueryable.Average(), await intQueryable.AverageAsync());
            Assert.AreEqual(intQueryable.Min(), await intQueryable.MinAsync());
            Assert.AreEqual(intQueryable.Max(), await intQueryable.MaxAsync());

            //Testing aggregates on int?
            IList<int?> nullableIntlist = new List<int?>
            {
                1,2,3,4,5
            };
            IQueryable<int?> nullableIntQueryable = nullableIntlist.AsQueryable();

            Assert.AreEqual(nullableIntQueryable.Count(), await nullableIntQueryable.CountAsync());
            Assert.AreEqual(nullableIntQueryable.Sum(), await nullableIntQueryable.SumAsync());
            Assert.AreEqual(nullableIntQueryable.Average(), await nullableIntQueryable.AverageAsync());
            Assert.AreEqual(nullableIntQueryable.Min(), await nullableIntQueryable.MinAsync());
            Assert.AreEqual(nullableIntQueryable.Max(), await nullableIntQueryable.MaxAsync());

            //Testing aggregates on long
            IList<long> longlist = new List<long>
            {
                1,2,3,4,5
            };
            IQueryable<long> longQueryable = longlist.AsQueryable();

            Assert.AreEqual(longQueryable.Count(), await longQueryable.CountAsync());
            Assert.AreEqual(longQueryable.Sum(), await longQueryable.SumAsync());
            Assert.AreEqual(longQueryable.Average(), await longQueryable.AverageAsync());
            Assert.AreEqual(longQueryable.Min(), await longQueryable.MinAsync());
            Assert.AreEqual(longQueryable.Max(), await longQueryable.MaxAsync());

            //Testing aggregates on long?
            IList<long?> longNullablelist = new List<long?>
            {
                1,2,3,4,5
            };
            IQueryable<long?> longNullableQueryable = longNullablelist.AsQueryable();

            Assert.AreEqual(longNullableQueryable.Count(), await longNullableQueryable.CountAsync());
            Assert.AreEqual(longNullableQueryable.Sum(), await longNullableQueryable.SumAsync());
            Assert.AreEqual(longNullableQueryable.Average(), await longNullableQueryable.AverageAsync());
            Assert.AreEqual(longNullableQueryable.Min(), await longNullableQueryable.MinAsync());
            Assert.AreEqual(longNullableQueryable.Max(), await longNullableQueryable.MaxAsync());

            //Testing aggregates on decimal
            IList<decimal> decimalList = new List<decimal>
            {
                1,2,3,4,5
            };
            IQueryable<decimal> decimalQueryable = decimalList.AsQueryable();

            Assert.AreEqual(decimalQueryable.Count(), await decimalQueryable.CountAsync());
            Assert.AreEqual(decimalQueryable.Sum(), await decimalQueryable.SumAsync());
            Assert.AreEqual(decimalQueryable.Average(), await decimalQueryable.AverageAsync());
            Assert.AreEqual(decimalQueryable.Min(), await decimalQueryable.MinAsync());
            Assert.AreEqual(decimalQueryable.Max(), await decimalQueryable.MaxAsync());

            //Testing aggregates on decimal?
            IList<decimal?> decimalNullableList = new List<decimal?>
            {
                1,2,3,4,5
            };
            IQueryable<decimal?> decimalNullableQueryable = decimalNullableList.AsQueryable();

            Assert.AreEqual(decimalNullableQueryable.Count(), await decimalNullableQueryable.CountAsync());
            Assert.AreEqual(decimalNullableQueryable.Sum(), await decimalNullableQueryable.SumAsync());
            Assert.AreEqual(decimalNullableQueryable.Average(), await decimalNullableQueryable.AverageAsync());
            Assert.AreEqual(decimalNullableQueryable.Min(), await decimalNullableQueryable.MinAsync());
            Assert.AreEqual(decimalNullableQueryable.Max(), await decimalNullableQueryable.MaxAsync());

            //Testing aggregates on float
            IList<float> floatList = new List<float>
            {
                1,2,3,4,5
            };
            IQueryable<float> floatQueryable = floatList.AsQueryable();

            Assert.AreEqual(floatQueryable.Count(), await floatQueryable.CountAsync());
            Assert.AreEqual(floatQueryable.Sum(), await floatQueryable.SumAsync());
            Assert.AreEqual(floatQueryable.Average(), await floatQueryable.AverageAsync());
            Assert.AreEqual(floatQueryable.Min(), await floatQueryable.MinAsync());
            Assert.AreEqual(floatQueryable.Max(), await floatQueryable.MaxAsync());

            //Testing aggregates on float?
            IList<float?> floatNullableList = new List<float?>
            {
                1,2,3,4,5
            };
            IQueryable<float?> floatNullableQueryable = floatNullableList.AsQueryable();

            Assert.AreEqual(floatNullableQueryable.Count(), await floatNullableQueryable.CountAsync());
            Assert.AreEqual(floatNullableQueryable.Sum(), await floatNullableQueryable.SumAsync());
            Assert.AreEqual(floatNullableQueryable.Average(), await floatNullableQueryable.AverageAsync());
            Assert.AreEqual(floatNullableQueryable.Min(), await floatNullableQueryable.MinAsync());
            Assert.AreEqual(floatNullableQueryable.Max(), await floatNullableQueryable.MaxAsync());

            //Testing aggregates on double
            IList<double> doubleList = new List<double>
            {
                1,2,3,4,5
            };
            IQueryable<double> doubleQueryable = doubleList.AsQueryable();

            Assert.AreEqual(doubleQueryable.Count(), await doubleQueryable.CountAsync());
            Assert.AreEqual(doubleQueryable.Sum(), await doubleQueryable.SumAsync());
            Assert.AreEqual(doubleQueryable.Average(), await doubleQueryable.AverageAsync());
            Assert.AreEqual(doubleQueryable.Min(), await doubleQueryable.MinAsync());
            Assert.AreEqual(doubleQueryable.Max(), await doubleQueryable.MaxAsync());

            //Testing aggregates on double?
            IList<double?> doubleNullableList = new List<double?>
            {
                1,2,3,4,5
            };
            IQueryable<double?> doubleNullableQueryable = doubleNullableList.AsQueryable();

            Assert.AreEqual(doubleNullableQueryable.Count(), await doubleNullableQueryable.CountAsync());
            Assert.AreEqual(doubleNullableQueryable.Sum(), await doubleNullableQueryable.SumAsync());
            Assert.AreEqual(doubleNullableQueryable.Average(), await doubleNullableQueryable.AverageAsync());
            Assert.AreEqual(doubleNullableQueryable.Min(), await doubleNullableQueryable.MinAsync());
            Assert.AreEqual(doubleNullableQueryable.Max(), await doubleNullableQueryable.MaxAsync());
        }
    }
}