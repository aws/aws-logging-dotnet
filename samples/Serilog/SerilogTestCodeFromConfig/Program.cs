using Serilog;
using Microsoft.Extensions.Configuration;

namespace SerilogTestCodeFromConfig
{
    class Program
    {
        static void Main(string[] args)
        {
            // logger configuration reads from appsettings.json
            var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

            var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

            logger.Information("Hello!");
        }
    }
}
