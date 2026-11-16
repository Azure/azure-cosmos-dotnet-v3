//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    [TestClass]
    public class Rfc1123DateTimeCacheTests
    {
        /// <remarks>
        /// Tolerance for how local time can differ from the cached time.
        /// 
        /// Rfc1123 updates once a second, and is used in HTTP-y scenarios,
        /// so there is typically another network connected machine involved.
        /// Between clock drift and periodic clock syncs, a difference of a small
        /// number of seconds is possible independent of any seconds.
        /// </remarks>
        private static readonly TimeSpan ComparisonTolerance = TimeSpan.FromSeconds(2);

        [TestMethod]
        [Owner("kmontrose")]
        public async Task AdvancesAsync()
        {
            DateTime testStart = DateTime.UtcNow;

            // internal timer will start before this call happens, so first is going to be "about" now
            // either because this touch initializes it or because the timer will have recently fired
            string first = Rfc1123DateTimeCache.UtcNow();

            DateTime firstParsed = DateTime.ParseExact(
                first,
                "R",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            TimeSpan firstDelta = (testStart - firstParsed).Duration();

            Assert.IsTrue(firstDelta <= Rfc1123DateTimeCacheTests.ComparisonTolerance, $"Delta was {firstDelta}");

            // wait long enough for the timer to fire again
            await Task.Delay(Rfc1123DateTimeCacheTests.ComparisonTolerance);

            string second = Rfc1123DateTimeCache.UtcNow();

            // the result must have changed
            Assert.AreNotEqual(first, second);
        }
    }
}
