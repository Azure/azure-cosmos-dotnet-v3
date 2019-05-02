//-----------------------------------------------------------------------
// <copyright file="CosmosFloat32.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosFloat32 : CosmosNumber
    {
        protected CosmosFloat32()
            : base(CosmosNumberType.Float32)
        {
        }

        public override bool IsFloatingPoint => true;

        public override bool IsInteger => false;

        public static CosmosFloat32 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosFloat32(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosFloat32 Create(float number)
        {
            return new EagerCosmosFloat32(number);
        }

        public override double? AsFloatingPoint()
        {
            return this.GetValue();
        }

        public override long? AsInteger()
        {
            return null;
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteFloat32Value(this.GetValue());
        }

        protected abstract float GetValue();
    }
}
