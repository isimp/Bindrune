using System.IO;
using System.Linq;
using Bindrune.Conflicts;
using Bindrune.Personal;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// A clash you muted stays out of the way for as long as it is the clash you looked at: it
    /// shows again once the same two binds do something worse, and a mute for a clash that is gone
    /// is forgotten, so moving a key away and back reports it again. A bind that is only missing
    /// for now, as at the start menu, keeps its mutes.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class MuteStoreTests
    {
        private static readonly BindEntry A = new BindEntry { Id = "cfg:a:Keys:Open" };
        private static readonly BindEntry B = new BindEntry { Id = "vanilla:Jump" };

        private static Conflict Clash(Severity severity, bool confirmed = false) =>
            new Conflict { A = A, B = B, Severity = severity, Confirmed = confirmed };

        private static readonly string[] Both = { A.Id, B.Id };

        [Fact]
        public void AMutedClashStaysMutedInTheNextGame()
        {
            using var profile = new TestProfile();

            MuteStore.Toggle(Clash(Severity.Soft));
            TestProfile.NewGame();

            Assert.True(MuteStore.IsMuted(Clash(Severity.Soft)));
            Assert.True(MuteStore.IsMuted(Clash(Severity.Note)));
        }

        [Fact]
        public void AWorseClashOfTheSamePairShowsAgain()
        {
            using var profile = new TestProfile();

            MuteStore.Toggle(Clash(Severity.Soft));

            Assert.False(MuteStore.IsMuted(Clash(Severity.Hard)));
        }

        [Fact]
        public void LearningThatBothApplyInTheSamePlaceShowsItAgain()
        {
            using var profile = new TestProfile();

            MuteStore.Toggle(Clash(Severity.Soft));

            Assert.False(MuteStore.IsMuted(Clash(Severity.Soft, confirmed: true)));
        }

        [Fact]
        public void UnmutingShowsItAgain()
        {
            using var profile = new TestProfile();

            MuteStore.Toggle(Clash(Severity.Soft));
            MuteStore.Toggle(Clash(Severity.Soft));

            Assert.False(MuteStore.IsMuted(Clash(Severity.Soft)));
        }

        [Fact]
        public void MutingAClashAMuteNoLongerCoversMutesItAsItIsNow()
        {
            using var profile = new TestProfile();
            MuteStore.Toggle(Clash(Severity.Note));

            // It got worse, so it shows; muting it again waves the worse one through.
            MuteStore.Toggle(Clash(Severity.Hard));

            Assert.True(MuteStore.IsMuted(Clash(Severity.Hard)));
        }

        [Fact]
        public void AMuteForAClashThatIsGoneIsForgotten()
        {
            using var profile = new TestProfile();
            MuteStore.Toggle(Clash(Severity.Soft));

            // Both binds are there and no longer clash: the key was moved away.
            MuteStore.Settle(Both, Enumerable.Empty<Conflict>());

            Assert.False(MuteStore.IsMuted(Clash(Severity.Soft)));
            Assert.Empty(PersonalStore.Lines(PersonalStore.Muted));
        }

        [Fact]
        public void ABindThatIsOnlyMissingForNowKeepsItsMutes()
        {
            using var profile = new TestProfile();
            MuteStore.Toggle(Clash(Severity.Soft));

            // At the start menu, before the mod binds its settings.
            MuteStore.Settle(new[] { B.Id }, Enumerable.Empty<Conflict>());

            Assert.True(MuteStore.IsMuted(Clash(Severity.Soft)));
        }

        [Fact]
        public void AnOldLineWithoutASeverityMutesThePairAndThenRecordsTheClashItHid()
        {
            using var profile = new TestProfile();
            Assert.True(PersonalStore.Replace(PersonalStore.Muted, new[] { Clash(Severity.Hard).PairKey }));

            Assert.True(MuteStore.IsMuted(Clash(Severity.Hard, confirmed: true)));

            MuteStore.Settle(Both, new[] { Clash(Severity.Soft) });

            // Now it hides exactly what it was hiding, and anything worse shows.
            Assert.True(MuteStore.IsMuted(Clash(Severity.Soft)));
            Assert.False(MuteStore.IsMuted(Clash(Severity.Hard)));
        }

        [Theory]
        [InlineData("serious")]
        [InlineData("")]
        [InlineData("7")]
        public void ALineTypedByHandThatCannotBeReadMutesThePairOutright(string severity)
        {
            using var profile = new TestProfile();
            Assert.True(PersonalStore.Replace(PersonalStore.Muted, new[] { Clash(Severity.Hard).PairKey + "\t" + severity }));

            Assert.True(MuteStore.IsMuted(Clash(Severity.Hard, confirmed: true)));
        }

        [Fact]
        public void AMuteAddedByHandWhileTheGameRunsIsHonoured()
        {
            using var profile = new TestProfile();
            MuteStore.Toggle(Clash(Severity.Note));
            Assert.False(MuteStore.IsMuted(Clash(Severity.Hard)));

            var text = File.ReadAllText(profile.KeysFile).Replace("\tnote\t0", "\thard\t1");
            File.WriteAllText(profile.KeysFile, text);
            File.SetLastWriteTimeUtc(profile.KeysFile, System.DateTime.UtcNow.AddMinutes(1));
            PersonalStore.Sync();

            Assert.True(MuteStore.IsMuted(Clash(Severity.Hard, confirmed: true)));
        }
    }
}
