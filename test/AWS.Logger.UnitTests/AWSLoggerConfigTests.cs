using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using AWS.Logger;

namespace AWS.Logger.UnitTests
{
    public class AWSLoggerConfigTests
    {
        AWSLoggerConfig config;
        public AWSLoggerConfigTests()
        {
            config = new AWSLoggerConfig();
        }
        [Fact]
        public void TestDefaultBatchSizeInBytes()
        {
            Assert.Equal(102400, config.BatchSizeInBytes);
        }

        [Fact]
        public void SetInvalidValueOnBatchSizeInBytes()
        {
            var exception = Record.Exception(() => config.BatchSizeInBytes = (int)Math.Pow(1024, 2) + 1);
            Assert.NotNull(exception);

            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void SetValidValueOnBatchSizeInBytes()
        {
            var exception = Record.Exception(() => config.BatchSizeInBytes = (int)Math.Pow(1024, 2) - 1);
            Assert.Null(exception);
        }
    }
}
