//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PatchOperationAddPathBuilderTests
    {
        [TestMethod]
        public void TestMe()
        {
            PatchOperation.AddPathBuilder<Person> builder = new ();
            IEnumerable<string> pathSegments = builder
                .Property(person => person.Description)
                .ArrayIndex(1)
                .Property(person => person.Id)
                .Build();

            Assert.AreEqual("Description/1/Id", builder.ToString());
        }
    }

    public class Person
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
