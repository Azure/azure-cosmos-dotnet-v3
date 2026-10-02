//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading;

    /// <summary>
    /// Tracks remaining timespan.
    /// </summary>
    internal sealed class TimeoutHelper
    {
        private readonly DateTime startTime;
        private readonly TimeSpan timeOut;
        private readonly CancellationToken cancellationToken;

        public TimeoutHelper(TimeSpan timeOut, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.startTime = DateTime.UtcNow;
            this.timeOut = timeOut;
            this.cancellationToken = cancellationToken;
        }

        public bool IsElapsed()
        {
            TimeSpan elapsed = DateTime.UtcNow.Subtract(this.startTime);
            return elapsed >= this.timeOut;
        }

        public TimeSpan GetRemainingTime()
        {
            TimeSpan elapsed = DateTime.UtcNow.Subtract(this.startTime);
            return this.timeOut.Subtract(elapsed);
        }

        public void ThrowTimeoutIfElapsed(SubStatusCodes subStatusCode = SubStatusCodes.Unknown)
        {
            if(this.IsElapsed())
            {
                throw new RequestTimeoutException(RMResources.RequestTimeout, subStatusCode);
            }
        }

        public void ThrowGoneIfElapsed(SubStatusCodes subStatusCode = SubStatusCodes.TimeoutGenerated410)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            if (this.IsElapsed())
            {
                throw new GoneException(RMResources.Gone, subStatusCode);
            }
        }
    }
}
