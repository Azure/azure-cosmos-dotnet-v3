namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    internal readonly struct SqlLocation
    {
        public SqlLocation(ulong startIndex, ulong endIndex)
        {
            this.StartIndex = startIndex;
            this.EndIndex = endIndex;
        }

        public ulong StartIndex { get; }
        public ulong EndIndex { get; }
    }
}
