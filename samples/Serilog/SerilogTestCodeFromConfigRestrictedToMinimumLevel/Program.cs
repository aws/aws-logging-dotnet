using Serilog;
using Microsoft.Extensions.Configuration;

namespace SerilogTestCodeFromConfigRestrictedToMinimumLevel
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

            logger.Information("This should not log since restrictedToMinimumLevel is Error in appsettings.json!");
            logger.Error("Hello!");
        }
    }
}
