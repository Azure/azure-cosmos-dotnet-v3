// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;

#nullable enable
    internal abstract class TimerWheelTimer
    {
        /// <summary>
        /// Timeout of the timer.
        /// </summary>
        public abstract TimeSpan Timeout { get; }

        /// <summary>
        /// Starts the timer based on the Timeout configuration.
        /// </summary>
        public abstract Task StartTimerAsync();

        /// <summary>
        /// Cancels the timer.
        /// </summary>
        public abstract bool CancelTimer();

        /// <summary>
        /// Fire the associated timeout callback.
        /// </summary>
        public abstract bool FireTimeout();
    }
}