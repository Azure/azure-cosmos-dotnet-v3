//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Globalization;

    internal sealed class ChannelCommonArguments
    {
        private readonly object mutex = new object();
        private TransportErrorCode timeoutCode;  // Guarded by mutex
        private bool payloadSent = false;  // Guarded by mutex

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
                lock (this.mutex)
                {
                    return this.payloadSent;
                }
            }
        }

        public void SnapshotCallState(
            out TransportErrorCode timeoutCode, out bool payloadSent)
        {
            lock (this.mutex)
            {
                timeoutCode = this.timeoutCode;
                payloadSent = this.payloadSent;
            }
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
            lock (this.mutex)
            {
                this.timeoutCode = errorCode;
            }
        }

        public void SetPayloadSent()
        {
            lock (this.mutex)
            {
                Debug.Assert(!this.payloadSent);
                if (this.payloadSent)
                {
                    throw new InvalidOperationException(
                        "TransportException.SetPayloadSent cannot be called more than once.");
                }
                this.payloadSent = true;
            }
        }
    }
}