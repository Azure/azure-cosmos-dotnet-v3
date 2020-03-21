//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class CosmosNull : CosmosElement
    {
        private static readonly CosmosNull Singleton = new CosmosNull();

        private CosmosNull()
            : base(CosmosElementType.Null)
        {
        }

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            return cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            return cosmosElementVisitor.Visit(this, input);
        }

        public static CosmosNull Create()
        {
            return CosmosNull.Singleton;
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteNullValue();
        }
    }
}
