//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Globalization;

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class Range<T> where T: IComparable<T>
    {
        public static readonly IComparer<T> TComparer = typeof(T) == typeof(string) ? (IComparer<T>)StringComparer.Ordinal : Comparer<T>.Default;

        [JsonConstructor]
        public Range(T min, T max, bool isMinInclusive, bool isMaxInclusive)
        {
            if (min == null)
            {
                throw new ArgumentNullException("min");
            }

            if (max == null)
            {
                throw new ArgumentNullException("max");
            }

            this.Min = min;
            this.Max = max;
            this.IsMinInclusive = isMinInclusive;
            this.IsMaxInclusive = isMaxInclusive;
        }

        public static Range<T> GetPointRange(T value)
        {
            return new Range<T>(value, value, true, true);
        }

        [JsonProperty("min")]
        public T Min { get; private set; }

        [JsonProperty("max")]
        public T Max { get; private set; }

        [JsonProperty("isMinInclusive")]
        public bool IsMinInclusive { get; private set; }

        [JsonProperty("isMaxInclusive")]
        public bool IsMaxInclusive { get; private set; }

        public bool IsSingleValue
        {
            get
            {
                return this.IsMinInclusive && this.IsMaxInclusive && TComparer.Compare(this.Min, this.Max) == 0;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return (TComparer.Compare(this.Min, this.Max) == 0) && !(this.IsMinInclusive && this.IsMaxInclusive);
            }
        }

        public static Range<T> GetEmptyRange(T value)
        {
            return new Range<T>(
                value,
                value,
                true,
                false);
        }

        public bool Contains(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            int minToValueRelation = TComparer.Compare(this.Min, value);
            int maxToValueRelation = TComparer.Compare(this.Max, value);

            return ((this.IsMinInclusive && minToValueRelation <= 0) || (!this.IsMinInclusive && minToValueRelation < 0))
                   &&
                   ((this.IsMaxInclusive && maxToValueRelation >= 0) || (!this.IsMaxInclusive && maxToValueRelation > 0));
        }

        public static bool CheckOverlapping(Range<T> range1, Range<T> range2)
        {
            if (range1 == null || range2 == null || range1.IsEmpty || range2.IsEmpty)
            {
                return false;
            }

            int cmp1 = TComparer.Compare(range1.Min, range2.Max);
            int cmp2 = TComparer.Compare(range2.Min, range1.Max);

            if (cmp1 <= 0 && cmp2 <= 0)
            {
                if ((cmp1 == 0 && !(range1.IsMinInclusive && range2.IsMaxInclusive))
                    || (cmp2 == 0 && !(range2.IsMinInclusive && range1.IsMaxInclusive)))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Range<T>);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 0;
                hash = (hash * 397) ^ this.Min.GetHashCode();
                hash = (hash * 397) ^ this.Max.GetHashCode();
                hash = (hash * 397) ^ Convert.ToInt32(this.IsMinInclusive);
                hash = (hash * 397) ^ Convert.ToInt32(this.IsMaxInclusive);
                return hash;
            }
        }

        public bool Equals(Range<T> other)
        {
            if (other == null)
            {
                return false;
            }

            return TComparer.Compare(this.Min, other.Min) == 0
                && TComparer.Compare(this.Max, other.Max) == 0
                && this.IsMinInclusive == other.IsMinInclusive
                && this.IsMaxInclusive == other.IsMaxInclusive;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}{1},{2}{3}", this.IsMinInclusive ? "[" : "(", this.Min, this.Max, this.IsMaxInclusive ? "]" : ")");
        }

        public class MinComparer : IComparer<Range<T>>
        {
            public static readonly MinComparer Instance = new MinComparer(TComparer);

            private readonly IComparer<T> boundsComparer;

            private MinComparer(IComparer<T> boundsComparer)
            {
                this.boundsComparer = boundsComparer;
            }

            public int Compare(Range<T> left, Range<T> right)
            {
                int result = this.boundsComparer.Compare(left.Min, right.Min);
                if (result != 0 || left.IsMinInclusive == right.IsMinInclusive)
                {
                    return result;
                }

                if (left.IsMinInclusive)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }
        }

        public class MaxComparer : IComparer<Range<T>>
        {
            public static readonly MaxComparer Instance = new MaxComparer(TComparer);

            private readonly IComparer<T> boundsComparer;

            private MaxComparer(IComparer<T> boundsComparer)
            {
                this.boundsComparer = boundsComparer;
            }

            public int Compare(Range<T> left, Range<T> right)
            {
                int result = this.boundsComparer.Compare(left.Max, right.Max);

                if (result != 0 || left.IsMaxInclusive == right.IsMaxInclusive)
                {
                    return result;
                }

                if (left.IsMaxInclusive)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
        }
    }
}
