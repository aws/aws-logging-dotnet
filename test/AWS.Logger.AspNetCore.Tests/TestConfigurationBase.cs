using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AWS.Logger.AspNetCore.Tests
{
    /// <summary>
    /// provides access to methods helpful when dealing with configuration in .net core JSON form
    /// </summary>
    public class TestConfigurationBase
    {
        /// <summary>
        /// read IConfiguration from a JSON file, for testing purposes
        /// </summary>
        /// <param name="jsonFileName"></param>
        /// <param name="configSectionInfoBlockName"></param>
        /// <param name="sourceFilePath"></param>
        /// <returns>IConfiguration from a JSON file</returns>
        public IConfiguration LoggerConfigSectionSetup(string jsonFileName, string configSectionInfoBlockName, [System.Runtime.CompilerServices.CallerFilePath]string sourceFilePath = "")
        {
            var configurationBuilder = new ConfigurationBuilder()
                                       .SetBasePath(Path.GetDirectoryName(sourceFilePath))
                                       .AddJsonFile(jsonFileName);

            IConfiguration Config;
            if (configSectionInfoBlockName != null)
            {
                Config = configurationBuilder
                    .Build()
                    .GetSection(configSectionInfoBlockName);
            }

            else
            {
                Config = configurationBuilder
                      .Build()
                      .GetSection("AWS.Logging");
            }

            return Config;

        }
    }
}
