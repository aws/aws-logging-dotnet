using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Xunit;

namespace AWS.Logger.AspNetCore.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class TestScope
    {
        [Fact]
        // Althrough scoping isn't currently supported make sure that at least it doesn't cause
        // issues if it is used.
        public void MakeSureCanCreateScope()
        {
            var coreLogger = new FakeCoreLogger();
            var logger = new AWSLogger("MakeSureCanCreateScope", coreLogger, null);

            using (var scope = logger.BeginScope<TestScope>(this))
            {
                logger.LogInformation("log");
            }

            Assert.Equal(1, coreLogger.ReceivedMessages.Count);
            Assert.True(coreLogger.ReceivedMessages.Contains("log"));
        }
    }
}
