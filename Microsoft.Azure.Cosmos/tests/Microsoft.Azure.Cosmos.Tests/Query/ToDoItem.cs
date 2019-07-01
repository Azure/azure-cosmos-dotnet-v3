using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Microsoft.Azure.Cosmos.Query
{
    public class ToDoItem
    {
        public string id { get; set; }
        public string pk { get; set; }
        public double cost { get; set; }
        public bool isDone { get; set; }
        public int count { get; set; }

        public static ToDoItem Create(string idPrefix)
        {
            if(idPrefix == null)
            {
                idPrefix = string.Empty;
            }

            string id = idPrefix + Guid.NewGuid().ToString();

            return new ToDoItem()
            {
                id = id,
                pk = Guid.NewGuid().ToString(),
                cost = 9000.00001,
                isDone = true,
                count = 42
            };
        }

        public static ReadOnlyCollection<ToDoItem> CreateItems(int count, string idPrefix)
        {
            List<ToDoItem> items = new List<ToDoItem>();
            for (int i = 0; i < count; i++)
            {
                items.Add(ToDoItem.Create(idPrefix));
            }

            return items.AsReadOnly();
        }
    }

    public class ToDoItemComparer : IComparer
    {
        public int Compare(object x, object y)
        {
            if (x == y)
            {
                return 0;
            }

            ToDoItem a = x as ToDoItem;
            ToDoItem b = y as ToDoItem;

            if(a == null || b == null)
            {
                throw new ArgumentException("Invalid type");
            }

            if(a.isDone != b.isDone
                || a.count != b.count
                || a.cost != b.cost
                || !string.Equals(a.id, b.id)
                || !string.Equals(a.pk, b.pk))
            {
                return 1;
            }

            return 0;
        }
    }
}
