//-----------------------------------------------------------------------
// <copyright file="Aggregate.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    /// <summary>
    /// Enum of all the aggregate functions.
    /// </summary>
    internal enum Aggregate
    {
        Min,
        Max,
        Count,
        Sum,
        Avg
    }
}