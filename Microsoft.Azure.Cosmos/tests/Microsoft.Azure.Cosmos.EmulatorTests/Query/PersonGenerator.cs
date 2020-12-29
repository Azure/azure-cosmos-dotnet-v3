namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal static class PersonGenerator
    {
        private static string GetRandomName(Random rand)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < rand.Next(0, 100); i++)
            {
                stringBuilder.Append('a' + rand.Next(0, 26));
            }

            stringBuilder.Append("💩");

            return stringBuilder.ToString();
        }

        private static City GetRandomCity(Random rand)
        {
            int index = rand.Next(0, 3);
            switch (index)
            {
                case 0:
                    return City.LosAngeles;
                case 1:
                    return City.NewYork;
                case 2:
                    return City.Seattle;
                default:
                    break;
            }

            return City.LosAngeles;
        }

        private static double GetRandomIncome(Random rand)
        {
            return rand.NextDouble() * double.MaxValue;
        }

        private static int GetRandomAge(Random rand)
        {
            return rand.Next();
        }

        private static Pet GetRandomPet(Random rand)
        {
            string name = PersonGenerator.GetRandomName(rand);
            int age = PersonGenerator.GetRandomAge(rand);
            return new Pet(name, age);
        }

        public static Person GetRandomPerson(Random rand)
        {
            string name = PersonGenerator.GetRandomName(rand);
            City city = PersonGenerator.GetRandomCity(rand);
            double income = PersonGenerator.GetRandomIncome(rand);
            List<Person> people = new List<Person>();
            if (rand.Next(0, 11) % 10 == 0)
            {
                for (int i = 0; i < rand.Next(0, 5); i++)
                {
                    people.Add(PersonGenerator.GetRandomPerson(rand));
                }
            }

            Person[] children = people.ToArray();
            int age = PersonGenerator.GetRandomAge(rand);
            Pet pet = PersonGenerator.GetRandomPet(rand);
            Guid guid = Guid.NewGuid();
            object mixedTypeField = (rand.Next(0, 7)) switch
            {
                0 => name,
                1 => city,
                2 => income,
                3 => children,
                4 => age,
                5 => pet,
                6 => guid,
                _ => throw new ArgumentException(),
            };
            return new Person(name, city, income, children, age, pet, guid, mixedTypeField);
        }
    }

    public enum City
    {
        NewYork,
        LosAngeles,
        Seattle
    }

    public sealed class Pet
    {
        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("age")]
        public int Age { get; }

        public Pet(string name, int age)
        {
            this.Name = name;
            this.Age = age;
        }
    }

    public sealed class Person
    {
        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("city")]
        [JsonConverter(typeof(StringEnumConverter))]
        public City City { get; }

        [JsonProperty("income")]
        public double Income { get; }

        [JsonProperty("children")]
        public Person[] Children { get; }

        [JsonProperty("age")]
        public int Age { get; }

        [JsonProperty("pet")]
        public Pet Pet { get; }

        [JsonProperty("guid")]
        public Guid Guid { get; }

        [JsonProperty("mixedTypeField")]
        public object MixedTypeField { get; }

        public Person(
            string name,
            City city,
            double income,
            Person[] children,
            int age,
            Pet pet,
            Guid guid,
            object mixedTypeField)
        {
            this.Name = name;
            this.City = city;
            this.Income = income;
            this.Children = children;
            this.Age = age;
            this.Pet = pet;
            this.Guid = guid;
            this.MixedTypeField = mixedTypeField;
        }
    }
}
