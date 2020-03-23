// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;

    /// <summary>
    /// This class is used to assert that a region of code can only be called concurrently by a limited amount of threads.
    /// </summary>
    internal sealed class InterlockIncrementCheck
    {
        private readonly int maxConcurrentOperations;
        private int counter = 0;

        public InterlockIncrementCheck(int maxConcurrentOperations = 1)
        {
            if (maxConcurrentOperations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrentOperations), "Cannot be lower than 1.");
            }

            this.maxConcurrentOperations = maxConcurrentOperations;
        }

        /// <summary>
        /// Increments the internal lock and asserts that only the allowed
        /// </summary>
        /// <exception cref="InvalidOperationException">When more operations than those allowed try to access the context.</exception>
        /// <example>
        /// InterlockIncrementCheck interlockIncrementCheck = new InterlockIncrementCheck();
        /// using (interlockIncrementCheck.EnterLockCheck())
        /// {
        ///    // protected code
        /// }
        ///
        /// </example>
        public void EnterLockCheck()
        {
            Interlocked.Increment(ref this.counter);
            if (this.counter > this.maxConcurrentOperations)
            {
                throw new InvalidOperationException($"InterlockIncrementCheck detected {this.counter} with a maximum of {this.maxConcurrentOperations}.");
            }
        }
    }
}
