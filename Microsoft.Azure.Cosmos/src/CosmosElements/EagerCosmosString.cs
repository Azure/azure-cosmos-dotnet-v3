namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;

    internal sealed class EagerCosmosString : CosmosString
    {
        public EagerCosmosString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException($"{nameof(value)} must not be null.");
            }

            this.Value = value;
        }

        public override string Value
        {
            get;
        }
    }
}
