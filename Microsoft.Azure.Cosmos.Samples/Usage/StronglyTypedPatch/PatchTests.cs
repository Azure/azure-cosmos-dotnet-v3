namespace StronglyTypedPatch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class PatchTests
    {
        public void TestPathResolution()
        {
            StronglyTypedPatchOperationFactory<Person> factory = new(serializerOptions : null);

            int variableIndex = 0;
            string dictionaryIndex = "NormalMan";

            string[] actualPaths = new[] {

                factory.Add(person => person.Age, 50),
                factory.Add(person => person.Children, new List<Person> { new Person("Billy", 1, 0) }),
                factory.Add(person => person.Children[-1], new Person("Billy", 25, 0)),
                factory.Add(person => person.Children[0], new Person("Billy", 25, 0)),

                // Test constants evaluation
                factory.Add(person => person.Children[0].Age, 25),
                factory.Add(person => person.Children[variableIndex].Age, 25),
                factory.Add(person => person.Children[4-4].Age, 25),

                factory.Add(person => person.Children[0].Name, "Bill"),
                factory.Add(person => person.Children[0].Salary, 0),
                factory.Add(person => person.Children[0].Children[0], value: new Person("Susie", 1, 0)),
                factory.Add(person => person.Children[0].Children, new List<Person> { new Person("Billy", 1, 0) }),
                factory.Add(person => person.Children[0].Children[-1], new Person("Billy", 25, 0)),
                factory.Add(person => person.Name, "Ted"),
                factory.Add(person => person.Salary, 25000),

                // Value support
                factory.Increment(person => person.YearsWorked.Value, 1),

                // Dictionary support, escaping JsonPointer
                factory.Add(person => person.AlterEgos[dictionaryIndex]  , new Person("George", 1, 0)),
                factory.Add(person => person.AlterEgos["Space Man"]  , new Person("Neil", 1, 0)),
                factory.Add(person => person.AlterEgos["Tilde~Man"]  , new Person("Will", 1, 0)),
                factory.Add(person => person.AlterEgos["Slash/Woman"], new Person("Sally", 1, 0)),

            }.Select(x => x.Path)
            .ToArray();

            string[] expectedPaths = new[]
            {
                "/age",
                "/children",
                "/children/-",
                "/children/0",

                "/children/0/age",
                "/children/0/age",
                "/children/0/age",

                "/children/0/name",
                "/children/0/salary",
                "/children/0/children/0",
                "/children/0/children",
                "/children/0/children/-",
                "/name",
                "/salary",

                "/yearsWorked",

                "/alterEgos/NormalMan",
                "/alterEgos/Space Man",
                "/alterEgos/Tilde~0Man",
                "/alterEgos/Slash~1Woman",
            };

            actualPaths.Should().BeEquivalentTo(expectedPaths, o => o.WithStrictOrdering());

            FluentActions.Invoking(() => factory.Add(person => person.ToString(), "Ted"))
                .Should().Throw<InvalidOperationException>(because: "methods are not supported");

            FluentActions.Invoking(() => factory.Add(person => person.Age + 5, 1))
                .Should().Throw<InvalidOperationException>(because: "while some binary operators are supported, addition is not");
        }       
    }

    public sealed class Person
    {
        public Person(string name, int age, double salary, IReadOnlyList<Person> children = null, int? yearsWorked = null, IReadOnlyDictionary<string, Person> alterEgos = null)
        {
            this.Name = name;
            this.Age = age;
            this.Salary = salary;
            this.YearsWorked = yearsWorked;
            this.Children = children ?? Array.Empty<Person>();
            this.AlterEgos = alterEgos ?? new Dictionary<string, Person>();
        }

        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("age")]
        public int Age { get; }

        [JsonProperty("salary")]
        public double Salary { get; }

        [JsonProperty("yearsWorked")]
        public int? YearsWorked { get; }

        [JsonProperty("children")]
        public IReadOnlyList<Person> Children { get; }

        [JsonProperty("alterEgos")]
        public IReadOnlyDictionary<string, Person> AlterEgos { get; }

        public override bool Equals(object obj)
        {
            return obj is Person person && this.Equals(person);
        }

        public bool Equals(Person other)
        {
            return (this.Name == other.Name) && (this.Age == other.Age);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Name, this.Age);
        }
    }
}
