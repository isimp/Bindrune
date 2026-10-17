using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bindrune.SelfTest
{
    /// <summary>How one check came out. <see cref="Info"/> notes something neither good nor bad.</summary>
    internal enum Outcome
    {
        Pass,
        Info,
        Skip,
        Warn,
        Fail,
    }

    /// <summary>One check of a part: what was looked at, how it came out, and what was found.</summary>
    internal sealed class Check
    {
        public Check(string part, string what, Outcome outcome, string detail)
        {
            Part = part;
            What = what;
            Outcome = outcome;
            Detail = detail;
        }

        public string Part { get; }

        public string What { get; }

        public Outcome Outcome { get; }

        public string Detail { get; }
    }

    /// <summary>
    /// The text of a run: a headline that says how it went, then what failed, what needs a look,
    /// what was skipped, what held and what is only for information, each in the order checked
    /// and named by its part. A run that checked nothing never reads as a success.
    /// </summary>
    internal static class Report
    {
        private static readonly Outcome[] Order = { Outcome.Fail, Outcome.Warn, Outcome.Skip, Outcome.Pass, Outcome.Info };

        /// <summary>The headline alone: "all 52 passed", "2 failed, 50 passed".</summary>
        public static string Headline(IReadOnlyCollection<Check> checks)
        {
            int Count(Outcome o) => checks.Count(c => c.Outcome == o);
            var fail = Count(Outcome.Fail);
            var warn = Count(Outcome.Warn);
            var skip = Count(Outcome.Skip);
            var pass = Count(Outcome.Pass);
            var info = Count(Outcome.Info);

            if (fail + warn + skip + pass == 0) return "nothing was checked";

            var notes = info > 0 ? ", " + Number(info) + " for information" : "";
            if (fail == 0 && warn == 0 && skip == 0) return "all " + Number(pass) + " passed" + notes;

            var parts = new List<string>();
            if (fail > 0) parts.Add(Number(fail) + " failed");
            if (warn > 0) parts.Add(Number(warn) + (warn == 1 ? " warning" : " warnings"));
            if (skip > 0) parts.Add(Number(skip) + " skipped");
            if (pass > 0) parts.Add(Number(pass) + " passed");
            return string.Join(", ", parts) + notes;
        }

        /// <summary>Whether a run went well: something held, and nothing failed or needs a look.</summary>
        public static bool Passed(IReadOnlyCollection<Check> checks) =>
            checks.Any(c => c.Outcome == Outcome.Pass) && !checks.Any(c => c.Outcome == Outcome.Fail || c.Outcome == Outcome.Warn);

        /// <param name="title">What ran, leading the headline: "Bindrune 0.5.1 self-test".</param>
        /// <param name="checks">The checks in the order they ran.</param>
        public static List<string> Lines(string title, IReadOnlyCollection<Check> checks)
        {
            var lines = new List<string> { title + ": " + Headline(checks) };
            foreach (var outcome in Order)
            {
                foreach (var check in checks.Where(c => c.Outcome == outcome))
                    lines.Add(Mark(outcome) + check.Part + ": " + check.What + (string.IsNullOrEmpty(check.Detail) ? "" : " - " + check.Detail));
            }
            return lines;
        }

        private static string Number(int n) => n.ToString(CultureInfo.InvariantCulture);

        private static string Mark(Outcome outcome)
        {
            switch (outcome)
            {
                case Outcome.Fail: return "FAIL  ";
                case Outcome.Warn: return "WARN  ";
                case Outcome.Skip: return "skip  ";
                case Outcome.Pass: return "ok    ";
                default: return "info  ";
            }
        }
    }
}
