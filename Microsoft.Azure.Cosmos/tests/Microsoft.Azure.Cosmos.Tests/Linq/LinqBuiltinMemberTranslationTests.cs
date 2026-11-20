//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LinqBuiltinMemberTranslationTests
    {
        [TestMethod]
        public void StringLength_TranslatesToLength()
        {
            Assert.AreEqual("LENGTH(doc[\"Name\"])", Translate(doc => doc.Name.Length));
        }

        [TestMethod]
        public void ICollectionCount_TranslatesToArrayLength()
        {
            Assert.AreEqual("ARRAY_LENGTH(doc[\"Orders\"])", Translate(doc => doc.Orders.Count));
        }

        [TestMethod]
        public void ListCount_TranslatesToArrayLength()
        {
            Assert.AreEqual("ARRAY_LENGTH(doc[\"Tags\"])", Translate(doc => doc.Tags.Count));
        }

        [TestMethod]
        public void IReadOnlyListCount_TranslatesToArrayLength()
        {
            Assert.AreEqual("ARRAY_LENGTH(doc[\"Ranks\"])", Translate(doc => doc.Ranks.Count));
        }

        [TestMethod]
        public void NonGenericICollectionCount_TranslatesToArrayLength()
        {
            Assert.AreEqual("ARRAY_LENGTH(doc[\"Items\"])", Translate(doc => doc.Items.Count));
        }

        [TestMethod]
        public void ArrayListCount_TranslatesToArrayLength()
        {
            Assert.AreEqual("ARRAY_LENGTH(doc[\"Entries\"])", Translate(doc => doc.Entries.Count));
        }

        [TestMethod]
        public void ArrayLength_TranslatesToArrayLength()
        {
            Assert.AreEqual("ARRAY_LENGTH(doc[\"Scores\"])", Translate(doc => doc.Scores.Length));
        }

        [TestMethod]
        public void ArrayLengthAsPropertyAccess_TranslatesToArrayLength()
        {
            // Expression.Property form of array Length, as built by expression-tree producers other than the C# compiler
            ParameterExpression doc = Expression.Parameter(typeof(Document), "doc");
            Expression body = Expression.Property(
                Expression.Property(doc, nameof(Document.Scores)),
                typeof(Array).GetProperty(nameof(Array.Length)));

            Assert.AreEqual("ARRAY_LENGTH(doc[\"Scores\"])", SqlTranslator.TranslateExpression(body));
        }

        [TestMethod]
        public void ByteArrayLength_IsNotSupported()
        {
            // byte[] is stored as a base64 string, so ARRAY_LENGTH would be undefined
            Assert.ThrowsException<DocumentQueryException>(() => Translate(doc => doc.Payload.Length));
        }

        [TestMethod]
        public void ByteArrayLengthAsPropertyAccess_IsNotArrayLength()
        {
            ParameterExpression doc = Expression.Parameter(typeof(Document), "doc");
            Expression body = Expression.Property(
                Expression.Property(doc, nameof(Document.Payload)),
                typeof(Array).GetProperty(nameof(Array.Length)));

            Assert.AreEqual("doc[\"Payload\"][\"Length\"]", SqlTranslator.TranslateExpression(body));
        }

        [TestMethod]
        public void ByteArrayCollectionCountAsPropertyAccess_StaysPath()
        {
            ParameterExpression doc = Expression.Parameter(typeof(Document), "doc");
            Expression body = Expression.Property(
                Expression.Property(doc, nameof(Document.Payload)),
                typeof(ICollection<byte>).GetProperty(nameof(ICollection<byte>.Count)));

            Assert.AreEqual("doc[\"Payload\"][\"Count\"]", SqlTranslator.TranslateExpression(body));
        }

        [TestMethod]
        public void RectangularArrayLength_StaysPath()
        {
            // Length counts every element, but a rectangular array is stored as nested JSON arrays
            Assert.AreEqual("doc[\"Matrix\"][\"Length\"]", Translate(doc => doc.Matrix.Length));
        }

        [TestMethod]
        public void RectangularArrayCollectionCountAsPropertyAccess_StaysPath()
        {
            ParameterExpression doc = Expression.Parameter(typeof(Document), "doc");
            Expression body = Expression.Property(
                Expression.Property(doc, nameof(Document.Matrix)),
                typeof(ICollection).GetProperty(nameof(ICollection.Count)));

            Assert.AreEqual("doc[\"Matrix\"][\"Count\"]", SqlTranslator.TranslateExpression(body));
        }

        [DataTestMethod]
        [DataRow(nameof(DateTime.Year), "yyyy")]
        [DataRow(nameof(DateTime.Month), "mm")]
        [DataRow(nameof(DateTime.Day), "dd")]
        [DataRow(nameof(DateTime.Hour), "hh")]
        [DataRow(nameof(DateTime.Minute), "mi")]
        [DataRow(nameof(DateTime.Second), "ss")]
        [DataRow(nameof(DateTime.Millisecond), "ms")]
        public void DateTimePart_TranslatesToDateTimePart(string member, string part)
        {
            ParameterExpression doc = Expression.Parameter(typeof(Document), "doc");
            Expression body = Expression.Property(Expression.Property(doc, nameof(Document.Created)), member);

            Assert.AreEqual($"DateTimePart(\"{part}\", doc[\"Created\"])", SqlTranslator.TranslateExpression(body));
        }

        [TestMethod]
        public void NullableDateTimeValueYear_TranslatesToDateTimePart()
        {
            Assert.AreEqual("DateTimePart(\"yyyy\", doc[\"Registered\"])", Translate(doc => doc.Registered.Value.Year));
        }

        [TestMethod]
        public void UnmappedDateTimeMember_StaysPath()
        {
            Assert.AreEqual("doc[\"Created\"][\"DayOfYear\"]", Translate(doc => doc.Created.DayOfYear));
        }

        [TestMethod]
        public void DateTimeOffsetYear_StaysPath()
        {
            // DateTimeOffset members are not mapped
            Assert.AreEqual("doc[\"Updated\"][\"Year\"]", Translate(doc => doc.Updated.Year));
        }

        [TestMethod]
        public void UserTypeMembers_StayPaths()
        {
            Assert.AreEqual("doc[\"Edition\"][\"Year\"]", Translate(doc => doc.Edition.Year));
            Assert.AreEqual("doc[\"Edition\"][\"Count\"]", Translate(doc => doc.Edition.Count));
            Assert.AreEqual("doc[\"Edition\"][\"Length\"]", Translate(doc => doc.Edition.Length));
        }

        [TestMethod]
        public void UserCollectionDeclaringCount_StaysPath()
        {
            Assert.AreEqual("doc[\"Custom\"][\"Count\"]", Translate(doc => doc.Custom.Count));
        }

        [TestMethod]
        public void UserTypeInNamespaceStartingWithSystemCollections_CountStaysPath()
        {
            Assert.AreEqual("doc[\"Tally\"][\"Count\"]", Translate(doc => doc.Tally.Count));
        }

        [TestMethod]
        public void UserTypeInSystemCollectionsGenericNamespace_CountStaysPath()
        {
            Assert.AreEqual("doc[\"Batch\"][\"Count\"]", Translate(doc => doc.Batch.Count));
        }

        [TestMethod]
        public void AnonymousTypeCount_StaysPath()
        {
            Assert.AreEqual("a[\"Count\"]", TranslateLike(new { Count = 0 }, a => a.Count));
        }

        [TestMethod]
        public void GenericDictionaryCount_StaysPath()
        {
            Assert.AreEqual("doc[\"Counts\"][\"Count\"]", Translate(doc => doc.Counts.Count));
        }

        [TestMethod]
        public void DictionaryCount_StaysPath()
        {
            Assert.AreEqual("doc[\"Totals\"][\"Count\"]", Translate(doc => doc.Totals.Count));
        }

        [TestMethod]
        public void IDictionaryCount_StaysPath()
        {
            Assert.AreEqual("doc[\"Limits\"][\"Count\"]", Translate(doc => doc.Limits.Count));
        }

        [TestMethod]
        public void NonGenericDictionaryCount_StaysPath()
        {
            Assert.AreEqual("doc[\"Table\"][\"Count\"]", Translate(doc => doc.Table.Count));
        }

        [TestMethod]
        public void NonGenericIDictionaryCount_StaysPath()
        {
            Assert.AreEqual("doc[\"Lookup\"][\"Count\"]", Translate(doc => doc.Lookup.Count));
        }

        private static string Translate<TResult>(Expression<Func<Document, TResult>> selector)
        {
            return SqlTranslator.TranslateExpression(selector.Body);
        }

        private static string TranslateLike<T, TResult>(T _, Expression<Func<T, TResult>> selector)
        {
            return SqlTranslator.TranslateExpression(selector.Body);
        }

        private sealed class Document
        {
            public string Name { get; set; }

            public ICollection<Edition> Orders { get; set; }

            public List<int> Tags { get; set; }

            public IReadOnlyList<int> Ranks { get; set; }

            public ICollection Items { get; set; }

            public ArrayList Entries { get; set; }

            public int[] Scores { get; set; }

            public byte[] Payload { get; set; }

            public int[,] Matrix { get; set; }

            public DateTime Created { get; set; }

            public DateTime? Registered { get; set; }

            public DateTimeOffset Updated { get; set; }

            public Edition Edition { get; set; }

            public CustomList Custom { get; set; }

            public System.CollectionsCustom.Tally Tally { get; set; }

            public BatchInfo Batch { get; set; }

            public IReadOnlyDictionary<string, int> Counts { get; set; }

            public Dictionary<string, int> Totals { get; set; }

            public IDictionary<string, int> Limits { get; set; }

            public Hashtable Table { get; set; }

            public IDictionary Lookup { get; set; }
        }

        private sealed class Edition
        {
            public int Year { get; set; }

            public int Count { get; set; }

            public int Length { get; set; }
        }

        private sealed class CustomList : List<int>
        {
            public new int Count => 0;
        }
    }
}

namespace System.CollectionsCustom
{
    internal sealed class Tally
    {
        public int Count { get; set; }
    }
}

namespace System.Collections.Generic
{
    internal sealed class BatchInfo
    {
        public int Count { get; set; }
    }
}
