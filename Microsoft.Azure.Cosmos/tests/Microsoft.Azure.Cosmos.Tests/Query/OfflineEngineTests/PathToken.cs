namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal abstract class PathToken : IEquatable<PathToken>, IComparable<PathToken>
    {
        public int CompareTo(PathToken other)
        {
            return this switch
            {
                IntegerPathToken integerPathToken1 => other switch
                {
                    IntegerPathToken integerPathToken2 => integerPathToken1.Index.CompareTo(integerPathToken2.Index),
                    StringPathToken => -1,
                    _ => throw new InvalidEnumArgumentException($"{nameof(other)}"),
                },
                StringPathToken stringPathToken1 => other switch
                {
                    IntegerPathToken => 1,
                    StringPathToken stringPathToken2 => stringPathToken1.PropertyName.CompareTo(stringPathToken2.PropertyName),
                    _ => throw new InvalidEnumArgumentException($"{nameof(other)}"),
                },
                _ => throw new InvalidEnumArgumentException($"this"),
            };
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

            return this switch
            {
                IntegerPathToken integerPathToken1 => other switch
                {
                    IntegerPathToken integerPathToken2 => integerPathToken1.Index.Equals(integerPathToken2.Index),
                    StringPathToken => false,
                    _ => throw new InvalidEnumArgumentException($"{nameof(other)}"),
                },
                StringPathToken stringPathToken1 => other switch
                {
                    IntegerPathToken => false,
                    StringPathToken stringPathToken2 => stringPathToken1.PropertyName.Equals(stringPathToken2.PropertyName),
                    _ => throw new InvalidEnumArgumentException($"{nameof(other)}"),
                },
                _ => throw new InvalidEnumArgumentException($"this"),
            };
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
            return this switch
            {
                IntegerPathToken integerPathToken => integerPathToken.Index.GetHashCode(),
                StringPathToken stringPathToken => stringPathToken.PropertyName.GetHashCode(),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(PathToken)} type: {this.GetType()}."),
            };
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