namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;
    [TestClass]
    public class NewtonsoftSerialization
    {
        [TestMethod]
        public void Blah()
        {
            Bar bar = new Bar()
            {
                Blah = "asdf",
                Blah2 = "asdf12"
            };

            Foo barAsFoo = bar as Foo;

            string payload = JsonConvert.SerializeObject(barAsFoo);
        }

        public abstract class Foo
        { 
            public string Blah { get; set; }
        }

        public class Bar : Foo
        {
            public string Blah2 { get; set; }
        }
    }
}
