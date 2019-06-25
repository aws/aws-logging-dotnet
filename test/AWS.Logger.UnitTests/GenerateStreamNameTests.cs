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


        private string[] SplitStreamName(string streamName)
        {
            const string searchToken = " - ";
            var tokens = new List<string>();
            int currentPos = 0;
            int pos = streamName.IndexOf(searchToken);
            while(pos != -1)
            {
                tokens.Add(streamName.Substring(currentPos, pos - currentPos));
                currentPos = pos + searchToken.Length;
                pos = streamName.IndexOf(searchToken, currentPos);
            }

            if(currentPos < streamName.Length)
            {
                tokens.Add(streamName.Substring(currentPos, streamName.Length - currentPos));
            }

            return tokens.ToArray();
        }

        private bool IsTokenDate(string token)
        {
            DateTime dt;
            return DateTime.TryParseExact(token, "yyyy/MM/ddTHH.mm.ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
        }

        private bool IsTokenGuid(string token)
        {
            Guid guid;
            return Guid.TryParse(token, out guid);
        }
    }
}
