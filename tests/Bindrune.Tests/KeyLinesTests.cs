using Bindrune.Personal;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    public class KeyLinesTests
    {
        [Fact]
        public void ALineWithoutTheFlagCountsAsInUse()
        {
            Assert.True(KeyLines.TryParse("cfg:a:b:c\tK\tH", out _, out var entry));
            Assert.True(entry.Active);
        }

        [Fact]
        public void ALineWithTooFewPartsIsSkipped() => Assert.False(KeyLines.TryParse("cfg:a:b:c", out _, out _));

        [Fact]
        public void NoKeyIsWrittenAsNone()
        {
            var entry = new PersonalEntry { Personal = KeyCombo.None, Profile = new KeyCombo(KeyCode.J, null), Active = true };
            Assert.Equal("cfg:a:b:c\tnone\tJ\t1", KeyLines.Format("cfg:a:b:c", entry));
        }

        [Fact]
        public void AKeyWithModifiersRoundTrips()
        {
            var combo = new KeyCombo(KeyCode.F, new[] { KeyCode.LeftControl, KeyCode.LeftShift });
            Assert.Equal(combo, KeyLines.ParseCombo(KeyLines.FormatCombo(combo)));
        }

        [Fact]
        public void TextThatIsNotAKeyIsNoKey() => Assert.Equal(KeyCombo.None, KeyLines.ParseCombo("not a key"));
    }
}
