//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    [Flags]
    internal enum ApiType
    {
        None = 0x0,
        MongoDB = 0x1,
        Gremlin = 0x2,
        Cassandra = 0x4,
        Table = 0x8,
        Sql = 0x10,
        Etcd = 0x20,
        GremlinV2 = 0x40,
    }
}