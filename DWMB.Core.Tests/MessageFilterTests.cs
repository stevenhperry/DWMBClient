using System;
using DWMB.Core;
using Xunit;

namespace DWMB.Core.Tests
{
    public class MessageFilterTests
    {
        // -- IsForwardable ------------------------------------------------------------

        [Theory]
        [InlineData("server", "DAL123")]
        [InlineData("DAL123", "server")]
        [InlineData("SOMEONE", "fp")]
        [InlineData("SOMEONE", "data")]
        public void IsForwardable_ServerTraffic_IsNeverForwarded(string sender, string receiver)
        {
            bool result = MessageFilter.IsForwardable(sender, receiver, "DAL123 hello", "DAL123");
            Assert.False(result);
        }

        [Fact]
        public void IsForwardable_ExactRecipientMatch_IsForwarded()
        {
            bool result = MessageFilter.IsForwardable("UAL1", "DAL123", "hi there", "DAL123");
            Assert.True(result);
        }

        [Fact]
        public void IsForwardable_MessagePrefixedWithCallsign_IsForwarded()
        {
            bool result = MessageFilter.IsForwardable("UAL1", "@22800", "dal123 check in", "DAL123");
            Assert.True(result);
        }

        [Fact]
        public void IsForwardable_MessagePrefixMatch_IsCaseInsensitive()
        {
            bool result = MessageFilter.IsForwardable("UAL1", "@22800", "DAL123, say altitude", "dal123");
            Assert.True(result);
        }

        [Fact]
        public void IsForwardable_PartialCallsignMatch_IsNotForwarded()
        {
            // Regression test: v1's own comment (MainWindow.xaml.cs) explicitly calls out
            // that string.StartsWith() would give false positives here (e.g. UAL1
            // matching inside UAL123) -- the regex must be anchored, not a substring check.
            bool result = MessageFilter.IsForwardable("SOMEONE", "SOMEONEELSE", "just chatting about UAL123 traffic", "UAL1");
            Assert.False(result);
        }

        [Fact]
        public void IsForwardable_SelfSentMessage_IsNeverForwarded_EvenWhenAddressedToSelf()
        {
            bool result = MessageFilter.IsForwardable("DAL123", "DAL123", "DAL123 talking to myself", "DAL123");
            Assert.False(result);
        }

        [Fact]
        public void IsForwardable_UnrelatedMessage_IsNotForwarded()
        {
            bool result = MessageFilter.IsForwardable("UAL1", "AAL2", "not addressed to me", "DAL123");
            Assert.False(result);
        }

        // -- IsDuplicate ----------------------------------------------------------------

        [Fact]
        public void IsDuplicate_NoPriorMessage_IsNotDuplicate()
        {
            var current = new RelayMessage(DateTimeOffset.UtcNow, "UAL1", "DAL123", "hi");
            Assert.False(MessageFilter.IsDuplicate(null, current));
        }

        [Fact]
        public void IsDuplicate_SameFieldsWithinWindow_IsDuplicate()
        {
            var now = DateTimeOffset.UtcNow;
            var last = new RelayMessage(now, "UAL1", "DAL123", "hi");
            var current = new RelayMessage(now.AddSeconds(1.5), "ual1", "dal123", "HI");
            Assert.True(MessageFilter.IsDuplicate(last, current));
        }

        [Fact]
        public void IsDuplicate_SameFieldsOutsideWindow_IsNotDuplicate()
        {
            var now = DateTimeOffset.UtcNow;
            var last = new RelayMessage(now, "UAL1", "DAL123", "hi");
            var current = new RelayMessage(now.AddSeconds(2.5), "UAL1", "DAL123", "hi");
            Assert.False(MessageFilter.IsDuplicate(last, current));
        }

        [Theory]
        [InlineData("AAL2", "DAL123", "hi")]
        [InlineData("UAL1", "AAL2", "hi")]
        [InlineData("UAL1", "DAL123", "bye")]
        public void IsDuplicate_AnyFieldDiffers_IsNotDuplicate(string sender, string receiver, string message)
        {
            var now = DateTimeOffset.UtcNow;
            var last = new RelayMessage(now, "UAL1", "DAL123", "hi");
            var current = new RelayMessage(now.AddSeconds(0.5), sender, receiver, message);
            Assert.False(MessageFilter.IsDuplicate(last, current));
        }

        // -- FreqTag ----------------------------------------------------------------------

        [Fact(Skip = "Hz is confirmed by xPilot's official SDK docs but not independently verified for vPilot, nor against a real running session for either host -- see MessageFilter.FreqTag's doc comment.")]
        public void FreqTag_ConvertsHzToWireFormat()
        {
            // xPilot's plugin SDK source explicitly documents this example: 123.725 MHz -> 123725000 Hz.
            string result = MessageFilter.FreqTag(new[] { 123_725_000 });
            Assert.Equal("@23725", result);
        }
    }
}
