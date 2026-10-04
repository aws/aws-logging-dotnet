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

        [Fact]
        public void CustomMaxSizeSplitsMoreAggressively()
        {
            var message = new string('a', 240000);
            // The default 256K cap keeps this in one part; a smaller custom cap splits it.
            Assert.Single(AWSLoggerCore.BreakupMessage(message));

            var parts = AWSLoggerCore.BreakupMessage(message, 100000);
            Assert.Equal((240000 / 100000) + 1, parts.Count);
        }

        [Fact]
        public void CustomMaxSizeKeepsLargeMessageInOnePart()
        {
            // A message that would split at 256K stays whole when the cap is raised toward CloudWatch's 1 MB limit.
            var message = new string('a', 300000);
            Assert.Equal(2, AWSLoggerCore.BreakupMessage(message).Count);
            Assert.Single(AWSLoggerCore.BreakupMessage(message, 1024 * 1024));
        }
    }
}
