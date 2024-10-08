//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Threading;

    internal sealed class ChannelCommonArguments
    {
        // do not touch directly, use helpers - Interlocked operations are required
        private long packedTimeoutCodeAndPayload;

        public ChannelCommonArguments(
            Guid activityId,
            TransportErrorCode initialTimeoutCode,
            bool userPayload)
        {
            this.ActivityId = activityId;
            this.UserPayload = userPayload;
            this.SetTimeoutCode(initialTimeoutCode);
        }

        public Guid ActivityId { get; set; }

        public bool UserPayload { get; private set; }

        // Call SnapshotCallState if you want to fetch all properties at once.
        public bool PayloadSent
        {
            get
            {
                long packedSnapshot = this.GetPacked();

                ChannelCommonArguments.Unpack(packedSnapshot, out bool payloadSent, out _);

                return payloadSent;
            }
        }

        public void SnapshotCallState(
            out TransportErrorCode timeoutCode, out bool payloadSent)
        {
            long packedSnapshot = this.GetPacked();

            ChannelCommonArguments.Unpack(packedSnapshot, out payloadSent, out timeoutCode);
        }

        // The timeout code is effectively a progress tracker. Various places
        // advance it as they make asynchronous calls that might time out.
        // The timeout code is an unreliable indicator in two ways:
        // - The caller and the actual network operation run on separate tasks.
        //   Cancellation isn't currently communicated effectively on timer
        //   expiration. As such, there is an inherent race between the timer
        //   firing and capturing the timeout code - the network operation can
        //   complete and advance the timeout code before the caller can fetch it.
        // - Even if the caller captures the right timeout code, this is no
        //   guarantee that the last operation is the one that exhausted the
        //   timeout budget.
        public void SetTimeoutCode(TransportErrorCode errorCode)
        {
            Debug.Assert(Enum.GetUnderlyingType(typeof(TransportErrorCode)) == typeof(int), "We have a hard assumption that TransportErrorCode fits in an int");
            Debug.Assert(TransportException.IsTimeout(errorCode));
            if (!TransportException.IsTimeout(errorCode))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is not a timeout error code",
                        errorCode),
                    nameof(errorCode));
            }

            long old, cur, updated;
            old = Volatile.Read(ref this.packedTimeoutCodeAndPayload);
            do
            {
                cur = old;
                // preserve the sign (high bit used to indicate payload sent), and set the error code
                updated = unchecked(old & (long)0x8000_0000__0000_0000UL) | (long)errorCode;
            }
            while ((old = Interlocked.CompareExchange(ref this.packedTimeoutCodeAndPayload, updated, old)) != cur);
        }

        public void SetPayloadSent()
        {
            long old, cur, updated;
            old = Volatile.Read(ref this.packedTimeoutCodeAndPayload);

            do
            {
                Debug.Assert(old >= 0, "Payload already sent");
                if (old < 0)
                {
                    throw new InvalidOperationException(
                        "TransportException.SetPayloadSent cannot be called more than once.");
                }

                cur = old;
                // preserve lower bits (used to hold timeout code), and set high bit
                updated = unchecked(old | (long)0x8000_0000__0000_0000UL);
            }
            while ((old = Interlocked.CompareExchange(ref this.packedTimeoutCodeAndPayload, updated, old)) != cur);
        }

        /// <summary>
        /// Read <see cref="packedTimeoutCodeAndPayload"/> with this method, UNLESS you are about to update it
        /// in <see cref="SetPayloadSent"/> or <see cref="SetTimeoutCode(TransportErrorCode)"/> - in which case 
        /// Volatile is fine.
        /// 
        /// Uses Interlocked to force an ordering.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetPacked()
        => Interlocked.Read(ref this.packedTimeoutCodeAndPayload);

        /// <summary>
        /// Use to unpack value previously read via <see cref="GetPacked"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unpack(long packed, out bool payloadSent, out TransportErrorCode timeoutCode)
        {
            // timeoutCode is in the low bits of packed, but we need to cast to ulong first to avoid sign extension
            timeoutCode = (TransportErrorCode)(int)(ulong)packed;

            // payloadSent is in the high bit of packed, which is equivalent to check if it is negative
            payloadSent = packed < 0;
        }
    }
}