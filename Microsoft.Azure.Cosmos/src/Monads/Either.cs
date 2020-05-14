// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Monads
{
    using System;

    internal readonly struct Either<TLeft, TRight>
    {
        private readonly TLeft left;
        private readonly TRight right;

        private Either(TLeft left, TRight right, bool isLeft)
        {
            this.left = left;
            this.right = right;
            this.IsLeft = isLeft;
        }

        public bool IsLeft { get; }

        public bool IsRight
        {
            get
            {
                return !this.IsLeft;
            }
        }

        public void Match(Action<TLeft> onLeft, Action<TRight> onRight)
        {
            if (this.IsLeft)
            {
                onLeft(this.left);
            }
            else
            {
                onRight(this.right);
            }
        }

        public TResult Match<TResult>(Func<TLeft, TResult> onLeft, Func<TRight, TResult> onRight)
        {
            TResult result;
            if (this.IsLeft)
            {
                result = onLeft(this.left);
            }
            else
            {
                result = onRight(this.right);
            }

            return result;
        }

        public TLeft FromLeft(TLeft defaultValue)
        {
            TLeft result;
            if (this.IsLeft)
            {
                result = this.left;
            }
            else
            {
                result = defaultValue;
            }

            return result;
        }

        public TRight FromRight(TRight defaultValue)
        {
            TRight result;
            if (this.IsRight)
            {
                result = this.right;
            }
            else
            {
                result = defaultValue;
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is Either<TLeft, TRight> other)
            {
                return this.Equals(other);
            }

            return false;
        }

        public bool Equals(Either<TLeft, TRight> other)
        {
            if (this.IsLeft != other.IsLeft)
            {
                return false;
            }

            bool memberEquals;
            if (this.IsLeft)
            {
                TLeft left1 = this.left;
                TLeft left2 = other.left;
                memberEquals = left1.Equals(left2);
            }
            else
            {
                TRight right1 = this.right;
                TRight right2 = other.right;
                memberEquals = right1.Equals(right2);
            }

            return memberEquals;
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            hashCode ^= this.IsLeft.GetHashCode();
            if (this.IsLeft)
            {
                hashCode ^= this.left.GetHashCode();
            }
            else
            {
                hashCode ^= this.right.GetHashCode();
            }

            return hashCode;
        }

        public static implicit operator Either<TLeft, TRight>(TLeft left)
        {
            return new Either<TLeft, TRight>(
                left: left,
                right: default,
                isLeft: true);
        }

        public static implicit operator Either<TLeft, TRight>(TRight right)
        {
            return new Either<TLeft, TRight>(
                left: default,
                right: right,
                isLeft: false);
        }
    }
}
