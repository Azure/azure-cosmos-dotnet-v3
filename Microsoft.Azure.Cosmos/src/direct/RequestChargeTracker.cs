//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class RequestChargeTracker
    {
        /// <summary>
        /// Total accumulated Charge that would be presented to the client in the next FeedResponse
        /// </summary>
        private long totalRUsNotServedToClient;

        private long totalRUs;

        /// <summary>
        /// 100 preserves 2 decimal points, 1000 3 and so on. This is because Interlocked operations are not supported for doubles.
        /// </summary>
        private const int numberOfDecimalPointToReserveFactor = 1000;

        public double TotalRequestCharge
        {
            get
            {
                return (double)this.totalRUs / numberOfDecimalPointToReserveFactor;
            }
        }

        public void AddCharge(double ruUsage)
        {
            Interlocked.Add(ref this.totalRUsNotServedToClient, (long)(ruUsage * numberOfDecimalPointToReserveFactor));
            Interlocked.Add(ref this.totalRUs, (long)(ruUsage * numberOfDecimalPointToReserveFactor));
        }

        /// <summary>
        /// Gets the Charge incurred so far in a thread-safe manner, and resets the value to zero. The function effectively returns
        /// all charges accumulated so far, which will be returned to client as a part of the feedResponse. And the value is reset to 0 
        /// so that we can keep on accumulating any new charges incurred by any new backend calls which returened after the current feedreposnse 
        /// is served to the user. 
        /// </summary>
        /// <returns></returns>
        public double GetAndResetCharge()
        {
            long rusSoFar = Interlocked.Exchange(ref this.totalRUsNotServedToClient, 0);
            return ((double)rusSoFar / numberOfDecimalPointToReserveFactor);
        }
    }
}