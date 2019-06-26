using System.Linq;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AWS.Logger.AspNetCore.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class TestScope
    {
        [Fact]
        // Make sure that a message will be logged inside a scope, even when scopes are not included.
        public void MakeSureCanCreateScope()
        {
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger("MakeSureCanCreateScope", coreLogger, null)
            {
                IncludeScopes = false
            };

            using (logger.BeginScope("Test Scope"))
            {
                logger.LogInformation("log");
            }

            Assert.Single(coreLogger.ReceivedMessages);
            Assert.True(coreLogger.ReceivedMessages.Contains("[Information] MakeSureCanCreateScope: log  \r\n"), "Messages don't contain actual log message.");
        }

        [Fact]
        // Make sure that a message will be logged outside a scope, even when scopes are included.
        public void MakeSureCanLogWithoutScope()
        {
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger("MakeSureCanCreateScope", coreLogger, null)
            {
                IncludeScopes = true
            };

            logger.LogInformation("log");

            Assert.Single(coreLogger.ReceivedMessages);
            var msg = coreLogger.ReceivedMessages.SingleOrDefault(m => m.Contains("[Information] MakeSureCanCreateScope: log  \r\n"));
            Assert.True(msg != null, "Messages don't contain actual log message.");
            Assert.False(msg.Contains("=>"), "Fragment of scopes exists (\"=>\").");
        }

        [Fact]
        // Make sure that a message inside a scope will be logged together with the scope.
        public void MakeSureScopeIsIncluded()
        {
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger("MakeSureCanCreateScope", coreLogger, null)
            {
                IncludeScopes = true
            };

            using (logger.BeginScope("Test scope"))
            {
                logger.LogInformation("log");
            }

            Assert.Single(coreLogger.ReceivedMessages);
            var msg = coreLogger.ReceivedMessages.SingleOrDefault(m => m.Contains("[Information] Test scope => MakeSureCanCreateScope: log  \r\n"));
            Assert.True(msg != null, "Messages don't contain actual log message.");
            // Same message should contain the scope
            Assert.True(msg.Contains("Test scope => "), "Scope is not included.");
        }

        [Fact]
        // Make sure that a message inside multiple scopes will be logged together with the scopes.
        public void MakeSureScopesAreIncluded()
        {
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger("MakeSureCanCreateScope", coreLogger, null)
            {
                IncludeScopes = true
            };

            using (logger.BeginScope("OuterScope"))
            {
                using (logger.BeginScope("InnerScope"))
                {
                    logger.LogInformation("log");
                }
            }

            Assert.Single(coreLogger.ReceivedMessages);
            var msg = coreLogger.ReceivedMessages.SingleOrDefault(m => m.Contains("[Information] OuterScope InnerScope => MakeSureCanCreateScope: log  \r\n"));
            Assert.True(msg != null, "Messages don't contain actual log message.");
            // Same message should contain the scope
            Assert.True(msg.Contains("OuterScope"), "Outer scope is not included.");
            Assert.True(msg.Contains("InnerScope"), "Inner scope is not included.");
        }
    }
}
