using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using AWS.Logger;

namespace AWS.Logger.UnitTests
{
    public class AWSLoggerConfigTests
    {
        [Fact]
        public void TestDefaultBatchSizeInBytes()
        {
            AWSLoggerConfig config = new AWSLoggerConfig();
            Assert.Equal(102400, config.BatchSizeInBytes);
        }

        [Fact]
        public void SetInvalidValueOnBatchSizeInBytes()
        {
            AWSLoggerConfig config = new AWSLoggerConfig();

            var exception = Record.Exception(() => config.BatchSizeInBytes = (int)Math.Pow(1024, 2) + 1);
            Assert.NotNull(exception);

            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void SetValidValueOnBatchSizeInBytes()
        {
            AWSLoggerConfig config = new AWSLoggerConfig();

            var exception = Record.Exception(() => config.BatchSizeInBytes = (int)Math.Pow(1024, 2) - 1);
            Assert.Null(exception);
        }
    }
}
