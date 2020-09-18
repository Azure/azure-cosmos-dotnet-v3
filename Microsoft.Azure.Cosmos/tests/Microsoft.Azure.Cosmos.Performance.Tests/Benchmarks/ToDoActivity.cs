// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    public class ToDoActivity
    {
        public string id { get; set; }
        public int taskNum { get; set; }
        public double cost { get; set; }
        public string description { get; set; }
        public string status { get; set; }
        public string CamelCase { get; set; }

        public bool valid { get; set; }

        public ToDoActivity[] children { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is ToDoActivity input))
            {
                return false;
            }

            return string.Equals(this.id, input.id)
                && this.taskNum == input.taskNum
                && this.cost == input.cost
                && string.Equals(this.description, input.description)
                && string.Equals(this.status, input.status);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
