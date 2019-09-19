//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IComparableTask : IComparable<IComparableTask>, IEquatable<IComparableTask>
    {
        Task StartAsync(CancellationToken token);
    }
}