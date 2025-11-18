//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;

#if PREVIEW
    public
#else
    internal
#endif
    enum QuantizerType
    {
        [EnumMember(Value = "product")]
        Product,

        [EnumMember(Value = "spherical")]
        Spherical
    }
}