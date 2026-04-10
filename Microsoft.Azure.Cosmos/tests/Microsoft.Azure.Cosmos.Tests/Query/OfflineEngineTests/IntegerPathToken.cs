namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;

    internal sealed class IntegerPathToken : PathToken
    {
        public IntegerPathToken(int index)
        {
            this.Index = index < 0 ? throw new ArgumentOutOfRangeException(nameof(index)) : index;
        }

        public int Index { get; }

        public override string ToString()
        {
            return this.Index.ToString();
        }
    }
}