// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract class CosmosTypedElement : CosmosElement, IEquatable<CosmosTypedElement>, IComparable<CosmosTypedElement>, IComparable
    {
        protected CosmosTypedElement(CosmosElementType elementType)
            : base(elementType)
        {
        }

        public bool Equals(CosmosTypedElement other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Type == other.Type && this.CoreEquals(other);
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(null, obj))
            {
                return false;
            }

            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((CosmosTypedElement)obj);
        }

        public override int GetHashCode()
        {
            return this.Type.GetHashCode() ^ this.CoreGetHashCode();
        }

        protected abstract bool CoreEquals(CosmosTypedElement elementType);

        protected abstract int CoreGetHashCode();

        public abstract int CompareTo(CosmosTypedElement other);

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return 1;
            }

            if (ReferenceEquals(this, obj))
            {
                return 0;
            }

            return obj is CosmosTypedElement other ? this.CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(CosmosTypedElement)}");
        }
    }

    internal abstract class CosmosTypedElement<T> : CosmosTypedElement
    {
        protected CosmosTypedElement(CosmosElementType elementType)
            : base(elementType)
        {
        }

        public abstract T Value
        {
            get;
        }

        protected override bool CoreEquals(CosmosTypedElement elementType)
        {
            CosmosTypedElement<T> otherAsTypeT = elementType as CosmosTypedElement<T>;
            if (otherAsTypeT == null)
            {
                return false;
            }

            return otherAsTypeT.Value.Equals(this.Value);
        }

        protected override int CoreGetHashCode()
        {
            return this.Value.GetHashCode();
        }

        public override int CompareTo(CosmosTypedElement other)
        {
            if (other == null)
            {
                return 1;
            }

            int compareValue = this.Type.CompareTo(other.Type);
            if (compareValue != 0)
            {
                return compareValue;
            }

            CosmosTypedElement<T> typedElement = (CosmosTypedElement<T>)other;
            return Comparer<T>.Default.Compare(this.Value, typedElement.Value);
        }

        internal sealed class EagerTypedElement : CosmosTypedElement<T>
        {
            private readonly Action<T, IJsonWriter> writerFunc;

            public EagerTypedElement(T value, CosmosElementType elementType, Action<T, IJsonWriter> writerFunc)
                : base(elementType)
            {
                if (value == null)
                {
                    throw new ArgumentNullException($"{nameof(value)}");
                }

                this.writerFunc = writerFunc;

                this.Value = value;
            }

            public override T Value
            {
                get;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                this.writerFunc(this.Value, jsonWriter);
            }
        }

        internal sealed class LazyCosmosTypedElement : CosmosTypedElement<T>
        {
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;
            private readonly Lazy<T> lazyValue;

            public LazyCosmosTypedElement(
                IJsonNavigator jsonNavigator,
                IJsonNavigatorNode jsonNavigatorNode,
                JsonNodeType expectedNodeType,
                Func<IJsonNavigator, IJsonNavigatorNode, T> valueFunc,
                CosmosElementType elementType)
            : base(elementType)
            {
                if (jsonNavigator == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonNavigator)}");
                }

                if (jsonNavigatorNode == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonNavigatorNode)}");
                }

                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != expectedNodeType)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {expectedNodeType} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
                this.lazyValue = new Lazy<T>(() =>
                {
                    return valueFunc(this.jsonNavigator, this.jsonNavigatorNode);
                });
            }

            public override T Value
            {
                get
                {
                    return this.lazyValue.Value;
                }
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                jsonWriter.WriteJsonNode(this.jsonNavigator, this.jsonNavigatorNode);
            }
        }
    }
}