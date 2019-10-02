//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Collections.Generic
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    internal sealed class CountableEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> enumerable;
        private readonly int count;

        public CountableEnumerable(
            IEnumerable<T> enumerable,
            int count)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException("enumerable");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            this.enumerable = enumerable;
            this.count = count;
        }

        public int Count
        {
            get
            {
                return this.count;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            int i = 0;
            foreach (T item in this.enumerable)
            {
                if (i++ >= this.count)
                {
                    break;
                }

                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
