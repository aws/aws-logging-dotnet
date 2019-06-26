using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using Xunit;

namespace AWS.Logger.AspNetCore.Tests
{
    public class TestFormatter
    {
        [Theory]
        [InlineData("my log message", LogLevel.Trace)]
        [InlineData("my log message", LogLevel.Debug)]
        [InlineData("my log message", LogLevel.Critical)]
        public void CustomFormatter_Must_Be_Applied(string message, LogLevel logLevel)
        {
            Func<LogLevel, object, Exception, string> customFormatter 
                = (level, state, ex) => level + " hello world" + state.ToString();

            Func<string, LogLevel, bool> filter = (categoryName, level) => true;

            var coreLogger = new FakeCoreLogger();

            var logger = new AWSLogger("TestCategory", coreLogger, filter, customFormatter);

            logger.Log(logLevel, 0, message, null, (state, ex) => state.ToString());

            string expectedMessage = customFormatter(logLevel, message, null);

            Assert.Equal(expectedMessage, coreLogger.ReceivedMessages.First().Replace(Environment.NewLine, string.Empty));
        }
    }
}
