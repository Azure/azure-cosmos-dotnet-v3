﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class CosmosNumber : CosmosElement, IEquatable<CosmosNumber>, IComparable<CosmosNumber>
    {
        protected CosmosNumber()
            : base()
        {
        }

        public abstract Number64 Value { get; }

        public abstract void Accept(ICosmosNumberVisitor cosmosNumberVisitor);

        public abstract TResult Accept<TResult>(ICosmosNumberVisitor<TResult> cosmosNumberVisitor);

        public abstract TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input);

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
        {
            cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
        {
            return cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
        {
            return cosmosElementVisitor.Visit(this, input);
        }

        public override bool Equals(CosmosElement cosmosElement)
        {
            return cosmosElement is CosmosNumber cosmosNumber && this.Equals(cosmosNumber);
        }

        public abstract bool Equals(CosmosNumber cosmosNumber);

        public int CompareTo(CosmosNumber other)
        {
            int thisTypeOrder = this.Accept(CosmosNumberToTypeOrder.Singleton);
            int otherTypeOrder = other.Accept(CosmosNumberToTypeOrder.Singleton);

            if (thisTypeOrder != otherTypeOrder)
            {
                return thisTypeOrder.CompareTo(otherTypeOrder);
            }

            // The types are the same so dispatch to each compare operator
            return this.Accept(CosmosNumberWithinTypeComparer.Singleton, other);
        }

        public static new CosmosNumber CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosNumber>(buffer);
        }

        public static new CosmosNumber Parse(string json)
        {
            return CosmosElement.Parse<CosmosNumber>(json);
        }

        public static bool TryCreateFromBuffer(
            ReadOnlyMemory<byte> buffer,
            out CosmosNumber cosmosNumber)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosNumber>(buffer, out cosmosNumber);
        }

        public static bool TryParse(
            string json,
            out CosmosNumber cosmosNumber)
        {
            return CosmosElement.TryParse<CosmosNumber>(json, out cosmosNumber);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosNumber> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosNumber>(buffer);
            }

            public static TryCatch<CosmosNumber> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosNumber>(json);
            }
        }

        private sealed class CosmosNumberToTypeOrder : ICosmosNumberVisitor<int>
        {
            public static readonly CosmosNumberToTypeOrder Singleton = new CosmosNumberToTypeOrder();

            private CosmosNumberToTypeOrder()
            {
            }

            public int Visit(CosmosNumber64 cosmosNumber64)
            {
                return 0;
            }

            public int Visit(CosmosInt8 cosmosInt8)
            {
                return 1;
            }

            public int Visit(CosmosInt16 cosmosInt16)
            {
                return 2;
            }

            public int Visit(CosmosInt32 cosmosInt32)
            {
                return 3;
            }

            public int Visit(CosmosInt64 cosmosInt64)
            {
                return 4;
            }

            public int Visit(CosmosUInt32 cosmosUInt32)
            {
                return 5;
            }

            public int Visit(CosmosFloat32 cosmosFloat32)
            {
                return 6;
            }

            public int Visit(CosmosFloat64 cosmosFloat64)
            {
                return 7;
            }
        }

        private sealed class CosmosNumberWithinTypeComparer : ICosmosNumberVisitor<CosmosNumber, int>
        {
            public static readonly CosmosNumberWithinTypeComparer Singleton = new CosmosNumberWithinTypeComparer();

            private CosmosNumberWithinTypeComparer()
            {
            }

            public int Visit(CosmosNumber64 cosmosNumber64, CosmosNumber input)
            {
                return cosmosNumber64.CompareTo((CosmosNumber64)input);
            }

            public int Visit(CosmosInt8 cosmosInt8, CosmosNumber input)
            {
                return cosmosInt8.CompareTo((CosmosInt8)input);
            }

            public int Visit(CosmosInt16 cosmosInt16, CosmosNumber input)
            {
                return cosmosInt16.CompareTo((CosmosInt16)input);
            }

            public int Visit(CosmosInt32 cosmosInt32, CosmosNumber input)
            {
                return cosmosInt32.CompareTo((CosmosInt32)input);
            }

            public int Visit(CosmosInt64 cosmosInt64, CosmosNumber input)
            {
                return cosmosInt64.CompareTo((CosmosInt64)input);
            }

            public int Visit(CosmosUInt32 cosmosUInt32, CosmosNumber input)
            {
                return cosmosUInt32.CompareTo((CosmosUInt32)input);
            }

            public int Visit(CosmosFloat32 cosmosFloat32, CosmosNumber input)
            {
                return cosmosFloat32.CompareTo((CosmosFloat32)input);
            }

            public int Visit(CosmosFloat64 cosmosFloat64, CosmosNumber input)
            {
                return cosmosFloat64.CompareTo((CosmosFloat64)input);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}