//-----------------------------------------------------------------------
// <copyright file="LinqScalarFunctionBaselineTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using System.Collections.Generic;

    internal interface ILinqTestDataGenerator
    {
        IEnumerable<Data> GenerateData();
    }
}
