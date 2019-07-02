//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;
    
    internal sealed class CosmosBoolean : CosmosElement
    {
        private static readonly CosmosBoolean True = new CosmosBoolean(true);
        private static readonly CosmosBoolean False = new CosmosBoolean(false);

        private CosmosBoolean(bool value)
            : base(CosmosElementType.Boolean)
        {
            this.Value = value;
        }

        public bool Value
        {
            get;
        }

        public static CosmosBoolean Create(bool boolean)
        {
            return boolean ? CosmosBoolean.True : CosmosBoolean.False;
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteBoolValue(this.Value);
        }
    }
}
