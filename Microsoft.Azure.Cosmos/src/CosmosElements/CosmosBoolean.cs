//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class CosmosBoolean : CosmosElement
    {
        private static readonly CosmosBoolean True = new CosmosBoolean(true);
        private static readonly CosmosBoolean False = new CosmosBoolean(false);

        private CosmosBoolean(bool value)
            : base(CosmosElementType.Boolean)
        {
            this.Value = value;
        }

        public bool Value { get; }

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

        public static new CosmosBoolean CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosBoolean>(buffer);
        }

        public static new CosmosBoolean Parse(string json)
        {
            return CosmosElement.Parse<CosmosBoolean>(json);
        }

        public static bool TryCreateFromBuffer(ReadOnlyMemory<byte> buffer, out CosmosBoolean cosmosBoolean)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosBoolean>(buffer, out cosmosBoolean);
        }

        public static bool TryParse(string json, out CosmosBoolean cosmosBoolean)
        {
            return CosmosElement.TryParse<CosmosBoolean>(json, out cosmosBoolean);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosBoolean> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosBoolean>(buffer);
            }

            public static TryCatch<CosmosBoolean> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosBoolean>(json);
            }
        }
    }
}
