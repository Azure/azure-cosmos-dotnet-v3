using System;
using System.Collections.Generic;
using System.Text;

namespace BenchmarkSDK
{
    public class Book
    {
        public string id { get; set; }
        public string pk { get; set; }
        public int taskNum { get; set; }
        public double cost { get; set; }
        public string description { get; set; }
        public string status { get; set; }
        public string CamelCase { get; set; }

        public bool valid { get; set; }

        public Book[] children { get; set; }

        public override bool Equals(Object obj)
        {
            Book input = obj as Book;
            if (input == null)
            {
                return false;
            }

            return string.Equals(this.id, input.id)
                && string.Equals(this.pk, input.pk)
                && this.taskNum == input.taskNum
                && this.cost == input.cost
                && string.Equals(this.description, input.description)
                && string.Equals(this.status, input.status);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public static Book CreateRandomBook(
            string pk = null, 
            string id = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }
            if (id == null)
            {
                id = Guid.NewGuid().ToString();
            }
            return new Book()
            {
                id = id,
                pk = pk,
                description = "CreateRandomToDoActivity",
                status = "something" + Guid.NewGuid(),
                taskNum = 42,
                cost = double.MaxValue,
                CamelCase = "camelCase",
                children = new Book[]
                { 
                    new Book { id = "child1", taskNum = 30 },
                    new Book { id = "child2", taskNum = 40}
                },
                valid = true
            };
        }
    }
}
