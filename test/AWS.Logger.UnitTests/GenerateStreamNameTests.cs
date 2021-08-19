using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Xunit;
using AWS.Logger;

using AWS.Logger.Core;

namespace AWS.Logger.UnitTests
{
    public class GenerateStreamNameTests
    {
        [Fact]
        public void DefaultConfig()
        {
            var config = new AWSLoggerConfig
            {

            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Equal(2, tokens.Length);

            Assert.True(IsTokenDate(tokens[0]));
            Assert.True(IsTokenGuid(tokens[1]));
        }

        [Fact]
        public void SuffixSet()
        {
            var config = new AWSLoggerConfig
            {
                LogStreamNameSuffix = "TheSuffix"
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Equal(2, tokens.Length);

            Assert.True(IsTokenDate(tokens[0]));
            Assert.Equal(config.LogStreamNameSuffix, tokens[1]);
        }

        [Fact]
        public void PrefixSetSuffixAtDefault()
        {
            var config = new AWSLoggerConfig
            {
                LogStreamNamePrefix = "ThePrefix"
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Equal(3, tokens.Length);

            Assert.Equal(config.LogStreamNamePrefix, tokens[0]);
            Assert.True(IsTokenDate(tokens[1]));
            Assert.True(IsTokenGuid(tokens[2]));
        }

        [Fact]
        public void PrefixSetSuffixSet()
        {
            var config = new AWSLoggerConfig
            {
                LogStreamNamePrefix = "ThePrefix",
                LogStreamNameSuffix = "TheSuffix"
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Equal(3, tokens.Length);

            Assert.Equal(config.LogStreamNamePrefix, tokens[0]);
            Assert.True(IsTokenDate(tokens[1]));
            Assert.Equal(config.LogStreamNameSuffix, tokens[2]);
        }

        [Fact]
        public void PrefixSetSuffixSetToNull()
        {
            var config = new AWSLoggerConfig
            {
                LogStreamNamePrefix = "ThePrefix",
                LogStreamNameSuffix = null
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Equal(2, tokens.Length);

            Assert.Equal(config.LogStreamNamePrefix, tokens[0]);
            Assert.True(IsTokenDate(tokens[1]));
        }

        [Fact]
        public void PrefixSetSuffixSetToEmptyString()
        {
            var config = new AWSLoggerConfig
            {
                LogStreamNamePrefix = "ThePrefix",
                LogStreamNameSuffix = string.Empty
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Equal(2, tokens.Length);

            Assert.Equal(config.LogStreamNamePrefix, tokens[0]);
            Assert.True(IsTokenDate(tokens[1]));
        }

        [Fact]
        public void NoTimestampSuffixSet()
        {
            var config = new AWSLoggerConfig
            {
                IncludeTimestampInLogStreamName = false,
                LogStreamNameSuffix = "TheSuffix"
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Single(tokens);

            Assert.Equal(config.LogStreamNameSuffix, tokens[0]);
        }

        [Fact]
        public void PrefixSetNoTimestampSuffixAtDefault()
        {
            var config = new AWSLoggerConfig
            {
                IncludeTimestampInLogStreamName = false,
                LogStreamNamePrefix = "ThePrefix"
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Equal(2, tokens.Length);

            Assert.Equal(config.LogStreamNamePrefix, tokens[0]);
            Assert.True(IsTokenGuid(tokens[1]));
        }

        [Fact]
        public void PrefixSetNoTimestampSuffixSet()
        {
            var config = new AWSLoggerConfig
            {
                IncludeTimestampInLogStreamName = false,
                LogStreamNamePrefix = "ThePrefix",
                LogStreamNameSuffix = "TheSuffix"
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Equal(2, tokens.Length);

            Assert.Equal(config.LogStreamNamePrefix, tokens[0]);
            Assert.Equal(config.LogStreamNameSuffix, tokens[1]);
        }

        [Fact]
        public void PrefixSetNoTimestampSuffixSetToNull()
        {
            var config = new AWSLoggerConfig
            {
                IncludeTimestampInLogStreamName = false,
                LogStreamNamePrefix = "ThePrefix",
                LogStreamNameSuffix = null
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Single(tokens);

            Assert.Equal(config.LogStreamNamePrefix, tokens[0]);
        }

        [Fact]
        public void PrefixSetNoTimestampSuffixSetToEmptyString()
        {
            var config = new AWSLoggerConfig
            {
                IncludeTimestampInLogStreamName = false,
                LogStreamNamePrefix = "ThePrefix",
                LogStreamNameSuffix = string.Empty
            };
            var streamName = AWSLoggerCore.GenerateStreamName(config);

            var tokens = SplitStreamName(streamName);
            Assert.Single(tokens);

            Assert.Equal(config.LogStreamNamePrefix, tokens[0]);
        }


        private string[] SplitStreamName(string streamName)
        {
            return streamName.Split(" - ");
        }

        private bool IsTokenDate(string token)
        {
            // This assumes the date separator on the system where tests are run is '/'. If it's '-' or anything else, tests will break.
            return DateTime.TryParseExact(token, "yyyy/MM/ddTHH.mm.ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }

        private bool IsTokenGuid(string token)
        {
            return Guid.TryParse(token, out _);
        }
    }
}
