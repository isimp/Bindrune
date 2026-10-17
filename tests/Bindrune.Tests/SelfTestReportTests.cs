using System.Collections.Generic;
using System.Linq;
using Bindrune.SelfTest;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// What a self-test run says when it ends: its headline is the first thing read, in the chat
    /// and at the top of the log, so it must never call a run a success that checked nothing or
    /// that found something wrong, and what went wrong comes first in the lines below it.
    /// </summary>
    public class SelfTestReportTests
    {
        private static Check C(Outcome outcome, string what = "x") => new Check("Part", what, outcome, null);

        [Fact]
        public void ARunThatCheckedNothingNeverReadsAsASuccess()
        {
            var none = new List<Check>();
            Assert.Equal("nothing was checked", Report.Headline(none));
            Assert.False(Report.Passed(none));
        }

        [Fact]
        public void ARunOfOnlyNotesCheckedNothing()
        {
            var notes = new List<Check> { C(Outcome.Info), C(Outcome.Info) };
            Assert.Equal("nothing was checked", Report.Headline(notes));
            Assert.False(Report.Passed(notes));
        }

        [Fact]
        public void ARunWhereEverythingHeldSaysSoAndCountsItsNotesApart()
        {
            var checks = new List<Check> { C(Outcome.Pass), C(Outcome.Pass), C(Outcome.Info) };
            Assert.Equal("all 2 passed, 1 for information", Report.Headline(checks));
            Assert.True(Report.Passed(checks));
        }

        [Fact]
        public void AFailureOrAWarningIsNeverASuccess()
        {
            Assert.False(Report.Passed(new List<Check> { C(Outcome.Pass), C(Outcome.Fail) }));
            Assert.False(Report.Passed(new List<Check> { C(Outcome.Pass), C(Outcome.Warn) }));
        }

        [Fact]
        public void ASkippedCheckIsToldButDoesNotFailTheRun()
        {
            var checks = new List<Check> { C(Outcome.Pass), C(Outcome.Skip) };
            Assert.Equal("1 skipped, 1 passed", Report.Headline(checks));
            Assert.True(Report.Passed(checks));
        }

        [Fact]
        public void TheHeadlineCountsEveryKindThatIsNotAPass()
        {
            var checks = new List<Check> { C(Outcome.Fail), C(Outcome.Warn), C(Outcome.Warn), C(Outcome.Skip), C(Outcome.Pass), C(Outcome.Info) };
            Assert.Equal("1 failed, 2 warnings, 1 skipped, 1 passed, 1 for information", Report.Headline(checks));
        }

        [Fact]
        public void WhatWentWrongComesFirstThenWhatHeldThenTheNotes()
        {
            var checks = new List<Check>
            {
                C(Outcome.Pass, "held"), C(Outcome.Info, "note"), C(Outcome.Fail, "broke"), C(Outcome.Skip, "skipped"), C(Outcome.Warn, "odd"),
            };
            var lines = Report.Lines("Bindrune self-test", checks);

            Assert.Equal("Bindrune self-test: 1 failed, 1 warning, 1 skipped, 1 passed, 1 for information", lines[0]);
            Assert.Equal(new[] { "FAIL  Part: broke", "WARN  Part: odd", "skip  Part: skipped", "ok    Part: held", "info  Part: note" }, lines.Skip(1));
        }

        [Fact]
        public void AFindingIsToldBesideItsCheck()
        {
            var lines = Report.Lines("t", new List<Check> { new Check("Clashes", "the pair is hard", Outcome.Fail, "it was soft") });
            Assert.Equal("FAIL  Clashes: the pair is hard - it was soft", lines[1]);
        }
    }
}
