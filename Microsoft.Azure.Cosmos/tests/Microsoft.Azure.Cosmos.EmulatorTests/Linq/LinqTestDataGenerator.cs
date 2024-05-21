//-----------------------------------------------------------------------
// <copyright file="LinqScalarFunctionBaselineTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using System.Collections.Generic;

    internal class LinqTestDataGenerator : ILinqTestDataGenerator
    {
        private readonly int count;

        public LinqTestDataGenerator(int count)
        {
            this.count = count;
        }

        public IEnumerable<Data> GenerateData()
        {
            for (int index = 0; index < this.count; index++)
            {
                yield return new Data()
                {
                    Id = index.ToString(),
                    Number = index * 1000,
                    Flag = index % 2 == 0,
                    Multiples = new int[] { index, index * 2, index * 3, index * 4 },
                    Pk = "Test"
                };
            }
        }
    }
}
