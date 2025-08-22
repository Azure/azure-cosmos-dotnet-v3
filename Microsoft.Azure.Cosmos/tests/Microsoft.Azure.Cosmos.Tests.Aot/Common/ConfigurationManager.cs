namespace Microsoft.Azure.Cosmos.Tests.Aot.Common
{
    using System.Text.Json;

    /// <summary>
    /// ConfigurationManager shim for netstandard
    /// </summary>
    internal static class ConfigurationManager
    {
        private const string GatewayEndpointSettingName = "GatewayEndpoint";
        private const string GatewayEndpointEnvironmentName = "COSMOSDBEMULATOR_ENDPOINT";

        static ConfigurationManager()
        {
            AppSettings = new Dictionary<string, string>();

            dynamic config = JsonSerializer.Deserialize<dynamic>(File.ReadAllText("settings.json"));
            dynamic appSettings = config.GetProperty("AppSettings");

            AppSettings = new Dictionary<string, string>();
            foreach(var propertyName in appSettings.EnumerateObject())
            {
                AppSettings[propertyName.Name] = propertyName.Value.ToString();
            }
        }

        public static Dictionary<string, string> AppSettings { get; private set; }
    }
}
