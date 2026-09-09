// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Threading;

    internal sealed class DecompressionBudget
    {
        public const long DefaultLimitBytes = 64L * 1024 * 1024;

        private readonly long limitBytes;
        private long chargedBytes;

        public DecompressionBudget()
            : this(DefaultLimitBytes)
        {
        }

        public DecompressionBudget(long limitBytes)
        {
            if (limitBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limitBytes));
            }

            this.limitBytes = limitBytes;
        }

        public long RemainingBytes
        {
            get
            {
                long remaining = this.limitBytes - Interlocked.Read(ref this.chargedBytes);
                return remaining > 0 ? remaining : 0;
            }
        }

        public bool TryCharge(long bytes)
        {
            if (bytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes));
            }

            while (true)
            {
                long current = Interlocked.Read(ref this.chargedBytes);
                if (bytes > this.limitBytes - current)
                {
                    return false;
                }

                long next = current + bytes;
                if (next < current)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref this.chargedBytes, next, current) == current)
                {
                    return true;
                }
            }
        }
    }
}
#endif
