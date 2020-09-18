namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.ComponentModel;

    internal abstract class PathToken : IEquatable<PathToken>, IComparable<PathToken>
    {
        public int CompareTo(PathToken other)
        {
            switch (this)
            {
                case IntegerPathToken integerPathToken1:
                    switch (other)
                    {
                        case IntegerPathToken integerPathToken2:
                            return integerPathToken1.Index.CompareTo(integerPathToken2.Index);

                        case StringPathToken stringPathToken2:
                            return -1;

                        default:
                            throw new InvalidEnumArgumentException($"{nameof(other)}");
                    }

                case StringPathToken stringPathToken1:
                    switch (other)
                    {
                        case IntegerPathToken integerPathToken2:
                            return 1;

                        case StringPathToken stringPathToken2:
                            return stringPathToken1.PropertyName.CompareTo(stringPathToken2.PropertyName);

                        default:
                            throw new InvalidEnumArgumentException($"{nameof(other)}");
                    }

                default:
                    throw new InvalidEnumArgumentException($"this");
            }
        }

        public bool Equals(PathToken other)
        {
            if (other == null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            switch (this)
            {
                case IntegerPathToken integerPathToken1:
                    switch (other)
                    {
                        case IntegerPathToken integerPathToken2:
                            return integerPathToken1.Index.Equals(integerPathToken2.Index);

                        case StringPathToken stringPathToken2:
                            return false;

                        default:
                            throw new InvalidEnumArgumentException($"{nameof(other)}");
                    }

                case StringPathToken stringPathToken1:
                    switch (other)
                    {
                        case IntegerPathToken integerPathToken2:
                            return false;

                        case StringPathToken stringPathToken2:
                            return stringPathToken1.PropertyName.Equals(stringPathToken2.PropertyName);

                        default:
                            throw new InvalidEnumArgumentException($"{nameof(other)}");
                    }

                default:
                    throw new InvalidEnumArgumentException($"this");
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PathToken pathToken))
            {
                return false;
            }

            return this.Equals(pathToken);
        }

        public override int GetHashCode()
        {
            switch (this)
            {
                case IntegerPathToken integerPathToken:
                    return integerPathToken.Index.GetHashCode();

                case StringPathToken stringPathToken:
                    return stringPathToken.PropertyName.GetHashCode();

                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(PathToken)} type: {this.GetType()}.");
            }
        }

        public static implicit operator PathToken(int index)
        {
            return new IntegerPathToken(index);
        }

        public static implicit operator PathToken(string propertyName)
        {
            return new StringPathToken(propertyName);
        }
    }
}
