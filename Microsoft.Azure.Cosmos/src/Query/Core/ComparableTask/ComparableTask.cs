//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class ComparableTask : IComparableTask
    {
        protected readonly int schedulePriority;

        protected ComparableTask(int schedulePriority)
        {
            this.schedulePriority = schedulePriority;
        }

        public abstract Task StartAsync(CancellationToken token);

        public virtual int CompareTo(IComparableTask other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            return this.CompareToByPriority(other as ComparableTask);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as IComparableTask);
        }

        public abstract bool Equals(IComparableTask other);

        public override abstract int GetHashCode();

        protected int CompareToByPriority(ComparableTask other)
        {
            if (other == null)
            {
                // This task is not ComparableTask, assume it has a higher priority.
                return 1;
            }

            if (object.ReferenceEquals(this, other))
            {
                return 0;
            }

            return this.schedulePriority.CompareTo(other.schedulePriority);
        }
    }
}