namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;
    using Microsoft.Azure.Documents;

    internal class ToDoItem
    {
        public string id { get; set; }
        public string pk { get; set; }
        public double cost { get; set; }
        public bool isDone { get; set; }
        public int count { get; set; }
        public string _rid { get; set; }

        public static ToDoItem Create(string idPrefix, ResourceId itemRid = null)
        {
            idPrefix ??= string.Empty;

            string id = idPrefix + Guid.NewGuid().ToString();

            return new ToDoItem()
            {
                id = id,
                pk = Guid.NewGuid().ToString(),
                cost = 9000.00001,
                isDone = true,
                count = 42,
                _rid = itemRid?.ToString()
            };
        }

        public static IList<ToDoItem> CreateItems(
            int count,
            string idPrefix,
            string containerRid = null)
        {
            List<ToDoItem> items = new List<ToDoItem>();
            for (uint i = 0; i < count; i++)
            {
                ResourceId rid = null;
                if (containerRid != null)
                {
                    // id 0 returns null for resource id
                    rid = ToDoItem.NewDocumentId(containerRid, i + 1);
                }

                items.Add(ToDoItem.Create(idPrefix, rid));
            }

            return items;
        }

        //Workaround
        //This method was removed from Direct
        private static ResourceId NewDocumentId(string collectionId, uint documentId)
        {
            ResourceId collectionResourceId = ResourceId.Parse(collectionId);

            ResourceId documentResourceId = ResourceId.Empty;

            //Properties have private setters
            documentResourceId.GetType().GetProperty(nameof(documentResourceId.Database)).SetValue(documentResourceId, collectionResourceId.Database);
            documentResourceId.GetType().GetProperty(nameof(documentResourceId.DocumentCollection)).SetValue(documentResourceId, collectionResourceId.DocumentCollection);
            documentResourceId.GetType().GetProperty(nameof(documentResourceId.Document)).SetValue(documentResourceId, documentId);

            return documentResourceId;
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

            if (x is not ToDoItem a || y is not ToDoItem b)
            {
                throw new ArgumentException("Invalid type");
            }

            if (a.isDone != b.isDone
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