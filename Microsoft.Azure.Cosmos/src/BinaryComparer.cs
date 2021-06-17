// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    internal sealed class BinaryComparer : IComparer<UInt128>
    {
        public static readonly BinaryComparer Singleton = new BinaryComparer();

        public int Compare(UInt128 x, UInt128 y)
        {
            if (x.GetLow() != y.GetLow())
            {
                if (this.ReverseBytes(x.GetLow()) < this.ReverseBytes(y.GetLow()))
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }

            if (x.GetHigh() != y.GetHigh())
            {
                if (this.ReverseBytes(x.GetHigh()) < this.ReverseBytes(y.GetHigh()))
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }

            return 0;
        }

        private ulong ReverseBytes(ulong value)
        {
            return ((value & 0x00000000000000FFUL) << 56) | ((value & 0x000000000000FF00UL) << 40) |
            ((value & 0x0000000000FF0000UL) << 24) | ((value & 0x00000000FF000000UL) << 8) |
            ((value & 0x000000FF00000000UL) >> 8) | ((value & 0x0000FF0000000000UL) >> 24) |
            ((value & 0x00FF000000000000UL) >> 40) | ((value & 0xFF00000000000000UL) >> 56);
        }
    }
}
