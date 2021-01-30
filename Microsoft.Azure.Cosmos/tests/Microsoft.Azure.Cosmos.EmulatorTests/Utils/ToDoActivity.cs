//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    public class ToDoActivity
    {
        public string id { get; set; }
        public int taskNum { get; set; }
        public double cost { get; set; }
        public string description { get; set; }
        public string pk { get; set; }
        public string CamelCase { get; set; }

        public bool valid { get; set; }

        public ToDoActivity[] children { get; set; }

        public override bool Equals(Object obj)
        {
            ToDoActivity input = obj as ToDoActivity;
            if (input == null)
            {
                return false;
            }

            return string.Equals(this.id, input.id)
                && this.taskNum == input.taskNum
                && this.cost == input.cost
                && string.Equals(this.description, input.description)
                && string.Equals(this.pk, input.pk);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static async Task<IList<ToDoActivity>> CreateRandomItems(Container container, int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
        {
            Assert.IsFalse(!randomPartitionKey && pkCount > 1);

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

                    await container.CreateItemAsync<ToDoActivity>(item: temp);
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
                id = id,
                description = "CreateRandomToDoActivity",
                pk = pk,
                taskNum = 42,
                cost = double.MaxValue,
                CamelCase = "camelCase",
                children = new ToDoActivity[]
                { new ToDoActivity { id = "child1", taskNum = 30 },
                  new ToDoActivity { id = "child2", taskNum = 40}
                },
                valid = true
            };
        }
    }
}
