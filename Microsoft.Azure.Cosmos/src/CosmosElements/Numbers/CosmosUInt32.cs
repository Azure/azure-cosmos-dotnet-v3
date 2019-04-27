//-----------------------------------------------------------------------
// <copyright file="CosmosUInt32.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosUInt32 : CosmosNumber
    {
        protected CosmosUInt32()
            : base(CosmosNumberType.UInt32)
        {
        }

        public override bool IsFloatingPoint => false;

        public override bool IsInteger => true;

        public static CosmosUInt32 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosUInt32(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosUInt32 Create(uint number)
        {
            return new EagerCosmosUInt32(number);
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

            jsonWriter.WriteUInt32Value(this.GetValue());
        }

        protected abstract uint GetValue();
    }
}
