// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal readonly struct NonNullable<T> : IEquatable<NonNullable<T>>
        where T : class
    {
        public NonNullable(T reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            this.Reference = reference;
        }

        public T Reference { get; }

        public bool Equals(NonNullable<T> other)
        {
            return this.Reference.Equals(other.Reference);
        }

        public override bool Equals(object obj)
        {
            if (obj is T reference)
            {
                return this.Equals(reference);
            }

            if (obj is NonNullable<T> nonNullable)
            {
                return this.Equals(nonNullable);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Reference.GetHashCode();
        }

        public override string ToString()
        {
            return this.Reference.ToString();
        }

        public static implicit operator T(NonNullable<T> nonNullable) => nonNullable.Reference;
        public static implicit operator NonNullable<T>(T reference) => new NonNullable<T>(reference);
    }
}
