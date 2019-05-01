//-----------------------------------------------------------------------
// <copyright file="CosmosInt16.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosInt16 : CosmosNumber
    {
        protected CosmosInt16()
            : base(CosmosNumberType.Int16)
        {
        }

        public override bool IsFloatingPoint => false;

        public override bool IsInteger => true;

        public static CosmosInt16 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosInt16(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosInt16 Create(short number)
        {
            return new EagerCosmosInt16(number);
        }

        public override double? AsFloatingPoint()
        {
            return (double)this.GetValue();
        }

        public override long? AsInteger()
        {
            return this.GetValue();
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteInt16Value(this.GetValue());
        }

        protected abstract short GetValue();
    }
}
