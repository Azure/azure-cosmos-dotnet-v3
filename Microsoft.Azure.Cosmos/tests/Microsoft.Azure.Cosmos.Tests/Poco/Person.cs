namespace Microsoft.Azure.Cosmos.Tests.Poco
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public sealed class Person
    {
        private static readonly Random random = new Random();
        private static readonly string[] names = new string[]
        {
            "Emory Carreiro",
            "Edwina Kopf",
            "Darleen Yeary",
            "Numbers Edmond",
            "Teisha Quill",
            "Mozell Beeks",
            "Lisa Fricke",
            "Everette Alas",
            "Devorah Devore",
            "Carlyn Kifer",
            "Brigida Uriarte",
            "Lindsy Mcbain",
            "Lorilee Updike",
            "Hermina Peplinski",
            "Jefferson Meese",
            "Babara Saraiva",
            "Rene Arant",
            "Spencer Guilford",
            "Jeanine Arriaga",
            "Tiara Ohern",
            "Chery Owenby",
            "Garry Moore",
            "Gene Watley",
            "Malia Plemmons",
            "Elke Wynter",
            "Arvilla Shiffer",
            "Willette Lodi",
            "Mitsue Dubuisson",
            "Booker Mallow",
            "January Korhonen",
            "Shemika Bowerman",
            "Jeneva Aponte",
            "Cristen Renfroe",
            "Alleen Comacho",
            "Lupita Acey",
            "Windy Froehlich",
            "Zofia Switzer",
            "Paz Corum",
            "Staci Aman",
            "Sixta Mejia",
            "Rudolph Beaulieu",
            "Lyn Emmerich",
            "Tawanda Cardone",
            "Leanne Mutchler",
            "Ellsworth Eisenman",
            "Micha Schepers",
            "Catalina Remus",
            "Mirian Boylan",
            "Larissa Apgar",
            "Sunny Fenton",
        };

        public Person(string name, int age, IReadOnlyList<Person> children)
        {
            this.Name = name;
            this.Age = age;
            this.Children = new List<Person>(children);
        }

        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("age")]
        public int Age { get; }

        [JsonProperty("children")]
        public IReadOnlyList<Person> Children { get; }

        public override bool Equals(object obj)
        {
            if (!(obj is Person person))
            {
                return false;
            }

            return this.Equals(person);
        }

        public bool Equals(Person other)
        {
            return (this.Name == other.Name) && (this.Age == other.Age);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public static Person GetRandomPerson()
        {
            string name = Person.names[Person.random.Next(0, Person.names.Length)];
            int age = Person.random.Next(0, 100);

            List<Person> children = new List<Person>();
            if (Person.random.Next() % 2 == 0)
            {
                int numChildren = Person.random.Next(0, 5);
                for (int i = 0; i < numChildren; i++)
                {
                    children.Add(Person.GetRandomPerson());
                }
            }

            return new Person(name, age, children);
        }
    }
}
