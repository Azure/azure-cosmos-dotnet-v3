namespace Cosmos.Samples.DistributedTransaction
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a bank account in either the US or Canadian bank container.
    /// </summary>
    public class BankAccount
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("accountHolder")]
        public string AccountHolder { get; set; }

        [JsonProperty("balance")]
        public decimal Balance { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        public override string ToString()
        {
            return $"{AccountHolder} ({Currency}): {Balance:F2}";
        }
    }

    /// <summary>
    /// Represents an exchange rate entry stored in the ExchangeRates container.
    /// </summary>
    public class ExchangeRate
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("fromCurrency")]
        public string FromCurrency { get; set; }

        [JsonProperty("toCurrency")]
        public string ToCurrency { get; set; }

        [JsonProperty("rate")]
        public decimal Rate { get; set; }

        [JsonProperty("lastUpdated")]
        public string LastUpdated { get; set; }

        public override string ToString()
        {
            return $"{FromCurrency} -> {ToCurrency}: {Rate:F4}";
        }
    }
}
