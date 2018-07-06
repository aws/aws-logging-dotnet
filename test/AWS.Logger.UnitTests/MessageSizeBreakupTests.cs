using System;
using System.Collections.Generic;
using System.Text;

using Xunit;

using AWS.Logger.Core;

namespace AWS.Logger.UnitTests
{
    public class MessageSizeBreakupTests
    {
        [Fact]
        public void AsciiTest()
        {
            var message = new string('a', 240000);
            Assert.Single(AWSLoggerCore.BreakupMessage(message));
        }

        [Fact]
        public void UnicodeCharTest()
        {
            var testChar = '∀';
            var charCount = 240000;
            var message = new string(testChar, charCount);
            var bytesSize = Encoding.UTF8.GetByteCount(message);
            Assert.Equal((bytesSize / 256000) + 1, AWSLoggerCore.BreakupMessage(message).Count);
        }
    }
}
