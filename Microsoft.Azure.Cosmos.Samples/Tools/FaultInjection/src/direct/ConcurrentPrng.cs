namespace Microsoft.Azure.Documents
{
    using System;

    // The class is thread safe.
    internal sealed class ConcurrentPrng
    {
        private readonly object mutex = new object();
        private readonly Random rng = new Random();

        // Returns a non-negative random integer that is less than the specified
        // maximum. 
        // The function has the same semantics as System.Random.Next(int).
        public int Next(int maxValue)
        {
            lock (this.mutex)
            {
                return this.rng.Next(maxValue);
            }
        }
    }
}