//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Utils
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Configuration;


    /// <summary>
    /// ConfigurationManager shim for netstandard
    /// </summary>
    internal static class ConfigurationManager
    {
        const string GatewayEndpointSettingName = "GatewayEndpoint";
        const string GatewayEndpointEnvironmentName = "COSMOSDBEMULATOR_ENDPOINT";

        static ConfigurationManager()
        {
            AppSettings = new Dictionary<string, string>();

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonFile($"settings.json", true, true);

            IConfigurationRoot configuration = builder.Build();
            foreach(IConfigurationSection configurationSetting in configuration.GetSection("AppSettings").GetChildren())
            {
                string configurationValue = configurationSetting.Value;
                if (string.Equals(GatewayEndpointSettingName, configurationSetting.Key))
                {
                    configurationValue = Environment.GetEnvironmentVariable(GatewayEndpointEnvironmentName) ?? configurationValue;
                }

                AppSettings.Add(configurationSetting.Key, configurationValue);
            }
        }

        public static Dictionary<string, string> AppSettings { get; private set; }
    }
}
