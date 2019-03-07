//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using Newtonsoft.Json;
    using System.Collections.Generic;

    #region Family classes

    public class GreatGreatFamily : LinqTestObject
    {
        [JsonProperty(PropertyName = "id")]
        public string GreatFamilyId;

        public GreatFamily GreatFamily;
    }

    public class GreatFamily
    {
        public Family Family;
    }

    public class Family : LinqTestObject
    {
        public string FamilyId;
        public Parent[] Parents;
        public Child[] Children;
        public bool IsRegistered;
        public object NullObject;
        public int Int;
        public int? NullableInt;
    }

    public class Parent : LinqTestObject
    {
        public string FamilyName;
        public string GivenName;
    }

    public class Child : LinqTestObject
    {
        public string FamilyName;
        public string GivenName;
        public string Gender;
        public int Grade;
        public List<Pet> Pets;
        public Dictionary<string, string> Things;
    }

    public class Pet : LinqTestObject
    {
        public string GivenName;
    }

    #endregion

    #region Simple Data classes

    public class Data
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        public int Number { get; set; }

        public bool Flag { get; set; }

        public int[] Multiples { get; set; }
    }

    #endregion
}