//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    /// <summary>
    /// Types of partition key component
    /// </summary>
    /// <remarks>
    /// Some of the values might not be used, but this class to be consistent with the backend enum
    /// </remarks>
    internal enum PartitionKeyComponentType
    {
        Undefined = 0x0,
        Null = 0x1,
        False = 0x2,
        True = 0x3,
        MinNumber = 0x4,
        Number = 0x5,
        MaxNumber = 0x6,
        MinString = 0x7,
        String = 0x8,
        MaxString = 0x9,
        Int64 = 0xA,
        Int32 = 0xB,
        Int16 = 0xC,
        Int8 = 0xD,
        Uint64 = 0xE,
        Uint32 = 0xF,
        Uint16 = 0x10,
        Uint8 = 0x11,
        Binary = 0x12,
        Guid = 0x13,
        Float = 0x14,
        Infinity = 0xFF,
    }
}