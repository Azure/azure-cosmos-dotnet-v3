//-----------------------------------------------------------------------
// <copyright file="LinqScalarFunctionBaselineTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    internal class LinqTestRandomDataGenerator : ILinqTestDataGenerator
    {
        private readonly int count;
        private readonly Random random;

        public LinqTestRandomDataGenerator(int count)
        {
            this.count = count;
            int seed = DateTime.Now.Millisecond;
            this.random = new Random(seed);

            Debug.WriteLine("Random seed: {0}", seed);
        }

        public IEnumerable<Data> GenerateData()
        {
            for (int index = 0; index < this.count; index++)
            {
                yield return new Data()
                {
                    Id = Guid.NewGuid().ToString(),
                    Number = this.random.Next(-10000, 10000),
                    Flag = index % 2 == 0,
                    Multiples = new int[] { index, index * 2, index * 3, index * 4 },
                    Pk = "Test"
                };
            }
        }
    }
}
