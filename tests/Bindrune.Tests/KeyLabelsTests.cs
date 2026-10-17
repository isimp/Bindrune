using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// What a key is called on screen once the keyboard has said what it calls it: the letter
    /// printed on the key under your layout, in upper case, and the key's own name wherever the
    /// keyboard's answer would read worse. Asking the keyboard needs the game, and the self-test's
    /// Key names part does that; what is made of the answer is tested here.
    /// </summary>
    public class KeyLabelsTests
    {
        [Fact]
        public void AKeyLabelledWithOneLetterShowsThatLetterInUpperCase()
        {
            // KeyCode.Z is the key marked Y on a German keyboard.
            Assert.Equal("Y", KeyLabels.Label("Z", "y", anyLength: false));
            Assert.Equal("Ö", KeyLabels.Label("Semicolon", "ö", anyLength: false));
        }

        [Fact]
        public void ALabelWithSpaceAroundItIsReadWithout()
        {
            Assert.Equal("Y", KeyLabels.Label("Z", " y ", anyLength: false));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NoLabelKeepsTheKeysName(string shown)
        {
            Assert.Equal("Z", KeyLabels.Label("Z", shown, anyLength: false));
            Assert.Equal("<Keyboard>/oem102", KeyLabels.Label("<Keyboard>/oem102", shown, anyLength: true));
        }

        [Fact]
        public void ALabelWithAControlCharacterKeepsTheKeysName()
        {
            Assert.Equal("Tab", KeyLabels.Label("Tab", "\t", anyLength: false));
            Assert.Equal("Return", KeyLabels.Label("Return", "a\nb", anyLength: true));
        }

        [Fact]
        public void ALongerLabelIsKeptOnlyForAKeyKnownByItsPath()
        {
            // A key with a KeyCode keeps Unity's name, which the rest of the panel uses too.
            Assert.Equal("LeftAlt", KeyLabels.Label("LeftAlt", "Left Alt", anyLength: false));

            // A key known only by its input path is better called by the keyboard than by the path.
            Assert.Equal("\\ |", KeyLabels.Label("<Keyboard>/oem102", "\\ |", anyLength: true));
        }
    }
}
