//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    /// <summary>
    /// Validates <see cref="DekCacheOptions"/> for the <see cref="DekCache"/> constructor.
    /// Throws on misconfiguration; otherwise returns silently.
    /// </summary>
    internal static class DekCacheOptionsValidator
    {
        public static void Validate(DekCacheOptions options)
        {
            if (options == null)
            {
                return;
            }

            TimeSpan effectiveTtl = options.DekPropertiesTimeToLive
                ?? TimeSpan.FromMinutes(Constants.DekPropertiesDefaultTTLInMinutes);

            if (options.RefreshBeforeExpiry.HasValue)
            {
                ArgumentValidation.ThrowIfNegative(options.RefreshBeforeExpiry.Value, nameof(options.RefreshBeforeExpiry));
                ArgumentValidation.ThrowIfGreaterThanOrEqual(options.RefreshBeforeExpiry.Value, effectiveTtl, nameof(options.RefreshBeforeExpiry));
            }

            DistributedCacheOptions distributed = options.DistributedCache;
            if (distributed != null)
            {
                if (distributed.Cache == null)
                {
                    throw new ArgumentNullException(
                        $"{nameof(options.DistributedCache)}.{nameof(distributed.Cache)}",
                        $"'{nameof(distributed.Cache)}' is required when '{nameof(options.DistributedCache)}' is provided.");
                }

                ArgumentValidation.ThrowIfNullOrWhiteSpace(
                    distributed.KeyPrefix,
                    $"{nameof(options.DistributedCache)}.{nameof(distributed.KeyPrefix)}");

                TimeSpan effectiveEntryLifetime = distributed.EntryLifetime
                    ?? TimeSpan.FromTicks(effectiveTtl.Ticks * 2);

                if (effectiveEntryLifetime <= effectiveTtl)
                {
                    throw new ArgumentOutOfRangeException(
                        $"{nameof(options.DistributedCache)}.{nameof(distributed.EntryLifetime)}",
                        $"'{nameof(distributed.EntryLifetime)}' must be strictly greater than '{nameof(options.DekPropertiesTimeToLive)}' so that L2 entries outlive L1 expiry.");
                }
            }
        }
    }
}
