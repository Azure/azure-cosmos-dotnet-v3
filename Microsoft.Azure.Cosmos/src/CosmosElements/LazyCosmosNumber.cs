namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;
    using System;

    internal sealed class LazyCosmosNumber : CosmosNumber
    {
        private readonly IJsonNavigator jsonNavigator;
        private readonly IJsonNavigatorNode jsonNavigatorNode;

        // TODO: replace this with Number64 when the time comes.
        private readonly Lazy<double> lazyNumber;

        public LazyCosmosNumber(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
        {
            LazyCosmosElementUtils.ValidateNavigatorAndNode(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.Number);

            this.jsonNavigator = jsonNavigator;
            this.jsonNavigatorNode = jsonNavigatorNode;
            this.lazyNumber = new Lazy<double>(() => 
            {
                return this.jsonNavigator.GetNumberValue(this.jsonNavigatorNode);
            });
        }

        public override bool IsDouble
        {
            get
            {
                // Until we have Number64 a LazyCosmosNumber is always a double.
                return true;
            }
        }

        public override bool IsInteger
        {
            get
            {
                // Until we have Number64 a LazyCosmosNumber is always a double.
                return false;
            }
        }

        public override double? AsDouble()
        {
            return this.lazyNumber.Value;
        }

        public override long? AsLong()
        {
            return null;
        }

        public override void WriteToWriter(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)} must not be null.");
            }

            jsonWriter.WriteJsonNode(this.jsonNavigator, jsonNavigatorNode);
        }
    }
}
