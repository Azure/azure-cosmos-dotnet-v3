//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    internal static class MathUtils
    {
        public static int CeilingMultiple(int x, int n)
        {
            if (x <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }
            if (n <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            x--;
            return checked(x + n - x % n);
        }
    }
}