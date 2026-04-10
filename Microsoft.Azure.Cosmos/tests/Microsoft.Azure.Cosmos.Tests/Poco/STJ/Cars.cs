namespace Microsoft.Azure.Cosmos.Tests.Poco.STJ
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public sealed class Cars
    {
        private static readonly Random random = new();
        private static readonly string[] names = new string[]
        {
            "Toyota 4Runner",
            "Toyota Camry",
            "Toyota Prius",
            "Toyota Rav4",
            "Toyota Avalon",
            "Honda Civic",
            "Honda Accord",
            "Honda CRV",
            "Honda MRV",
            "BMW C18",
            "BMW 328i",
            "BMW 530i",
        };

        private static readonly string[] features = new string[]
        {
            "all wheel drive",
            "abs",
            "apple car play",
            "power window",
            "heated seating",
            "steering mounted control",
        };

        public Cars(
            string name,
            int colorCode,
            double vin,
            List<string> customFeatures)
        {
            this.Name = name;
            this.ColorCode = colorCode;
            this.Vin = vin;
            this.CustomFeatures = customFeatures;
        }

        [JsonPropertyName("name")]
        public string Name { get; }

        [JsonPropertyName("colorCode")]
        public int ColorCode { get; }

        [JsonPropertyName("vin")]
        public double Vin { get; }

        [JsonPropertyName("customFeatures")]
        public List<string> CustomFeatures { get; }

        public override bool Equals(object obj)
        {
            if (!(obj is Cars car))
            {
                return false;
            }

            return this.Equals(car);
        }

        public bool Equals(Cars other)
        {
            return this.Name == other.Name && this.ColorCode == other.ColorCode;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public static Cars GetRandomCar()
        {
            string name = names[random.Next(0, names.Length)];
            int colorCode = random.Next(0, 100);
            double vin = random.NextDouble() * 10000000;
            List<string> customFeatures = new();

            int numOfFeatures = random.Next(0, 3);
            for (int i = 0; i < numOfFeatures; i++)
            {
                customFeatures.Add(GetRandomFeature());
            }

            return new Cars(name, colorCode, vin, customFeatures);
        }

        public static string GetRandomFeature()
        {
            return features[random.Next(0, features.Length)];
        }
    }
}