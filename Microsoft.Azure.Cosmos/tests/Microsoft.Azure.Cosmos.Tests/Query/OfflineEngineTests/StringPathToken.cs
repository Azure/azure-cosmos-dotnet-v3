namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;

    internal sealed class StringPathToken : PathToken
    {
        public StringPathToken(string propertyName)
        {
            this.PropertyName = string.IsNullOrWhiteSpace(propertyName) ? throw new ArgumentNullException(nameof(propertyName)) : propertyName;
        }

        public string PropertyName { get; }

        public override string ToString()
        {
            return this.PropertyName.ToString();
        }
    }
}