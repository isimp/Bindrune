using System.IO;
using System.Linq;
using Bindrune.Personal;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// The two files Bindrune and Keepsake read from each other, checked against the samples in
    /// tests/contract, which Keepsake holds identical copies of. A test here failing means a
    /// format changed: update the sample, the version line, and Keepsake, together.
    /// </summary>
    public class ContractTests
    {
        private static string[] Sample(string name) => File.ReadAllLines(Path.Combine("contract", name));

        /// <summary>The lines of the keys section, the part Keepsake reads.</summary>
        private static string[] KeyLinesOf(string[] lines) => lines
            .SkipWhile(l => l != "[keys]").Skip(1)
            .TakeWhile(l => !l.StartsWith("["))
            .Where(l => l.Length > 0 && !l.StartsWith("#"))
            .ToArray();

        [Fact]
        public void BindruneWritesTheKeysSample()
        {
            var sample = Sample("bindrune.keys");
            Assert.Equal(KeyLines.StateVersion, sample[0]);

            foreach (var line in KeyLinesOf(sample))
            {
                Assert.True(KeyLines.TryParse(line, out var id, out var entry));
                Assert.Equal(line, KeyLines.Format(id, entry));
            }
        }

        [Fact]
        public void TheKeysSampleHoldsKeys() => Assert.Equal(4, KeyLinesOf(Sample("bindrune.keys")).Length);

        [Fact]
        public void BindruneReadsThePinsSample()
        {
            var kept = KeepsakeContract.Parse(Sample("keepsake.pins"));
            Assert.NotNull(kept);

            Assert.Equal(5, kept.Count);
            var open = kept.Single(k => k.Key == "Open");
            Assert.Equal("isimp.Example.cfg", open.File);
            Assert.Equal("Keys", open.Section);
            Assert.Equal("K", open.Value);
            Assert.Equal("H", open.Profile);

            Assert.Null(kept.Single(k => k.Key == "Toggle").Profile);
        }

        [Fact]
        public void ThePinsSampleIsTheVersionBindruneReads() =>
            Assert.Equal(KeepsakeContract.PinsVersion, Sample("keepsake.pins")[0]);
    }
}
