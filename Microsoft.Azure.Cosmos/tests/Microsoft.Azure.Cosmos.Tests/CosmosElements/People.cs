namespace Microsoft.Azure.Cosmos.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;

    public sealed class Person
    {
        public static readonly string[] Names = new string[]
        {
                "Alice",
                "Bob",
                "Charlie",
                "Craig",
                "Dave",
                "Eve",
                "Faythe",
                "Grace",
                "Heidi",
                "Ivan",
                "Judy",
                "Mallory",
                "Olivia",
                "Peggy",
                "Sybil",
                "Ted",
                "Trudy",
                "Walter",
                "Wendy"
        };

        public Person(string name, int age, Person[] children)
        {
            this.Name = name;
            this.Age = age;
            this.Children = children;
        }

        public string Name
        {
            get;
        }

        public int Age
        {
            get;
        }

        public Person[] Children
        {
            get;
        }

        public static Person CreateRandomPerson(Random random)
        {
            string name = Names[random.Next() % Names.Length];
            int age = random.Next(0, 100);

            List<Person> children = new List<Person>();
            if (random.Next() % 2 == 0)
            {
                int numChildren = random.Next(0, 3);
                for (int i = 0; i < numChildren; i++)
                {
                    children.Add(CreateRandomPerson(random));
                }
            }

            return new Person(name, age, children.ToArray());
        }
    }
}
