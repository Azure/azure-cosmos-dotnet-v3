//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Microsoft.Azure.Documents;

    public class Author
    {
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Name { get; set; }
        public string Location { get; set; }
    }

    public class Language
    {
        public string Name { get; set; }
        public string Copyright { get; set; }
    }

    public class Edition
    {
        public string Name { get; set; }
        public int Year { get; set; }
    }

    public class Book
    {
        //Verify that we can override the propertyName but still can query them using .NET Property names.
        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        public Language[] Languages { get; set; }
        public Author Author { get; set; }
        public double Price { get; set; }
        [JsonProperty(PropertyName = Constants.Properties.Id)]
        public string Id { get; set; }
        public List<Edition> Editions { get; set; }
    }
}
