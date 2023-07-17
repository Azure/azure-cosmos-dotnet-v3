//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal enum ClientQLLiteralKind
    {
        Undefined,
        Array,
        Binary,
        Boolean,
        CGuid,
        CNumber,
        MDateTime,
        MJavaScript,
        MNumber,
        MObjectId,
        MRegex,
        MSingleton,
        MSymbol,
        MTimestamp,
        Null,
        Number,
        Object,
        String,
    }
}