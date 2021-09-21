// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class ToDoActivity
    {
        [JsonProperty(propertyName: "id")]
        public string Id { get; set; }
        [JsonProperty(propertyName: "taskNum")]
        public int TaskNum { get; set; }
        [JsonProperty(propertyName: "cost")]
        public double Cost { get; set; }
        [JsonProperty(propertyName: "description")]
        public string Description { get; set; }
        [JsonProperty(propertyName: "status")]
        public string Status { get; set; }
        [JsonProperty(propertyName: "camelCase")]
        public string CamelCase { get; set; }
        [JsonProperty(propertyName: "pk")]
        public string Pk { get; set; }
        [JsonProperty(propertyName: "valid")]
        public bool Valid { get; set; }
        [JsonProperty(propertyName: "children")]
        public ToDoActivity[] Children { get; set; }

        public override bool Equals(Object obj)
        {
            if (! (obj is ToDoActivity input))
            {
                return false;
            }

            return string.Equals(this.Id, input.Id)
                && this.TaskNum == input.TaskNum
                && this.Cost == input.Cost
                && string.Equals(this.Pk, input.Pk)
                && string.Equals(this.Description, input.Description)
                && string.Equals(this.Status, input.Status);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static IList<ToDoActivity> CreateRandomItems(int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
        {
            List<ToDoActivity> createdList = new List<ToDoActivity>();
            for (int i = 0; i < pkCount; i++)
            {
                string pk = "TBD";
                if (randomPartitionKey)
                {
                    pk += Guid.NewGuid().ToString();
                }

                for (int j = 0; j < perPKItemCount; j++)
                {
                    ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity(pk);
                    createdList.Add(temp);
                }
            }

            return createdList;
        }

        public static ToDoActivity CreateRandomToDoActivity(string pk = null, string id = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }
            if (id == null)
            {
                id = Guid.NewGuid().ToString();
            }
            return new ToDoActivity()
            {
                Id = id,
                Description = "CreateRandomToDoActivity",
                Pk = pk,
                TaskNum = 42,
                Cost = double.MaxValue,
                CamelCase = "camelCase",
                Children = new ToDoActivity[]
                { new ToDoActivity { Id = "child1", TaskNum = 30 },
                  new ToDoActivity { Id = "child2", TaskNum = 40}
                },
                Valid = true
            };
        }
    }
}
