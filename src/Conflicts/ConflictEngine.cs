using System.Collections.Generic;
using System.Linq;
using Bindrune.Context;
using Bindrune.Discovery;
using UnityEngine;

namespace Bindrune.Conflicts
{
    public static class ConflictEngine
    {
        public static List<Conflict> Find(IEnumerable<BindEntry> binds)
        {
            var list = binds.Where(b => b.Combo.IsBound).ToList();
            var result = new List<Conflict>();

            foreach (var group in list.GroupBy(b => b.Combo.MainToken))
            {
                var members = group.ToList();

                // Alt, Ctrl and Shift are shared on purpose: mods watch for them being held to
                // qualify a click or another key. Reporting those as clashes buries the real ones.
                if (KeyCombo.IsModifier(members[0].Combo.Main)) continue;

                for (var i = 0; i < members.Count; i++)
                for (var j = i + 1; j < members.Count; j++)
                {
                    var conflict = SameKey(members[i], members[j]);
                    if (conflict != null) result.Add(conflict);
                }
            }

            return result
                .OrderBy(c => c.Severity)
                .ThenByDescending(c => c.Confirmed)
                .ThenBy(c => c.KeyLabel)
                .ToList();
        }

        /// <summary>
        /// What a bind would clash with if you gave it this key, asked before anything is written.
        /// The answer comes from the same SameKey the real scan uses, on a stand-in carrying the
        /// candidate combo, so a preview can never disagree with what you see afterwards.
        ///
        /// The stand-in keeps the real bind's id, which is how its situations still resolve: they
        /// are recorded against the bind, not against whatever key it happens to be on.
        /// </summary>
        public static List<Conflict> Preview(BindEntry bind, KeyCombo candidate, IEnumerable<BindEntry> others)
        {
            var result = new List<Conflict>();
            if (!candidate.IsBound || KeyCombo.IsModifier(candidate.Main)) return result;

            var stand = new BindEntry
            {
                Id = bind.Id,
                OwnerName = bind.OwnerName,
                OwnerGuid = bind.OwnerGuid,
                Label = bind.Label,
                Section = bind.Section,
                Source = bind.Source,
                Modifiers = bind.Modifiers,
                Combo = candidate,
                Editable = bind.Editable,
                Handle = bind.Handle
            };

            foreach (var other in others)
            {
                if (other.Internal || !other.Compared || other.Id == bind.Id) continue;
                if (!other.Combo.IsBound || other.Combo.MainToken != candidate.MainToken) continue;

                var conflict = SameKey(stand, other);
                if (conflict != null) result.Add(conflict);
            }

            return result.OrderBy(c => c.Severity).ThenByDescending(c => c.Confirmed).ToList();
        }

        private static Conflict SameKey(BindEntry a, BindEntry b)
        {
            // Only ever shown: the key label on conflict rows and in their sentences. Grouping and
            // identity use MainToken, which keeps Unity's names.
            var key = a.Combo.Main != KeyCode.None ? KeyLabels.Of(a.Combo.Main) : a.Combo.RawPath;

            // Situations win over key analysis: two binds that are never live at the same time
            // cannot collide no matter what they are bound to.
            var situationsA = Situations(a);
            var situationsB = Situations(b);

            if (NeverTogether(situationsA, situationsB))
            {
                return new Conflict
                {
                    A = a, B = b, Severity = Severity.Note, KeyLabel = key,
                    Reason = $"{Name(a)} applies to {Join(situationsA.Select(Describe))} and {Name(b)} to {Join(situationsB.Select(Describe))}, so they are never live together."
                };
            }

            var conflict = Classify(a, b, key);
            if (conflict == null) return conflict;
            if (conflict.Severity == Severity.Note) return conflict;

            // Confirmed means they genuinely meet somewhere, so it needs a real overlap. Both
            // merely saying something is not enough: one can say where it is live and the other what
            // it needs in hand, which neither confirms nor rules anything out.
            var shared = Shared(situationsA, situationsB);
            if (shared.Count > 0)
            {
                conflict.Confirmed = true;
                conflict.Reason += $" Both apply to {Join(shared)}.";
                return conflict;
            }

            // Nothing could be ruled out. Say what is missing, so the reader knows what would
            // settle it.
            var aSilent = situationsA.Count == 0;
            var bSilent = situationsB.Count == 0;

            if (aSilent && bSilent)
            {
                conflict.Reason += " Neither says when it applies, so this cannot be ruled out yet.";
                return conflict;
            }

            if (aSilent || bSilent)
            {
                var unknown = aSilent ? a : b;
                var known = aSilent ? b : a;
                conflict.Reason += $" {Name(known)} says when it applies, {Name(unknown)} does not" +
                                   " - setting that one's situations would settle this.";
                return conflict;
            }

            // Both describe themselves, but on different axes: one names an item in hand, the
            // other only where it is live. Name the missing half rather than claiming they meet.
            var aNeedsItem = HeldItems(situationsA) != null;
            var bNeedsItem = HeldItems(situationsB) != null;

            if (aNeedsItem != bNeedsItem)
            {
                var holding = aNeedsItem ? a : b;
                var quiet = aNeedsItem ? b : a;
                conflict.Reason += $" {Name(holding)} needs something in hand, and {Name(quiet)}" +
                                   " does not say which items it applies to, so this cannot be ruled out.";
            }

            return conflict;
        }

        /// <summary>
        /// Two binds can never fire together only if one of the two axes rules it out, and each
        /// axis needs both sides to say something.
        ///
        /// The axes are independent: "World" and "holding a pickaxe" are not rival answers, they
        /// are where you are and what is in your hands. Treating a held item as a place would make
        /// a bind that declares only HeldItems look disjoint from everything.
        /// </summary>
        private static bool NeverTogether(HashSet<string> a, HashSet<string> b)
        {
            var whereA = Where(a);
            var whereB = Where(b);
            if (whereA.Count > 0 && whereB.Count > 0 && !whereA.Overlaps(whereB)) return true;

            // Two hands, and neither bind is holding the other's item.
            var itemsA = HeldItems(a);
            var itemsB = HeldItems(b);
            return itemsA != null && itemsB != null && !itemsA.Overlaps(itemsB);
        }

        private static bool AtDefault(BindEntry bind) => BindWriter.DefaultOf(bind).Equals(bind.Combo);

        private static HashSet<string> Where(IEnumerable<string> tags) =>
            new HashSet<string>(tags.Where(t => !EquippedItems.IsHeldTag(t)));

        private static HashSet<string> HeldItems(IEnumerable<string> tags)
        {
            HashSet<string> all = null;
            foreach (var expanded in tags.Select(EquippedItems.Expand).Where(e => e != null))
            {
                if (all == null) all = new HashSet<string>();
                all.UnionWith(expanded);
            }

            return all;
        }

        /// <summary>
        /// Situations both binds really are live in. Only genuine overlaps: somewhere both name,
        /// or an item both require. Never one side's own situations, which would put words in the
        /// other bind's mouth.
        /// </summary>
        private static List<string> Shared(HashSet<string> a, HashSet<string> b)
        {
            var common = Where(a).Intersect(Where(b)).Select(Describe).ToList();

            var itemsA = HeldItems(a);
            var itemsB = HeldItems(b);
            if (itemsA != null && itemsB != null && itemsA.Overlaps(itemsB))
            {
                // Name the narrower side: "Iron pickaxe" is true of both when the other said
                // "any pickaxe", where "any pickaxe" would overstate what the first one covers.
                var narrower = itemsA.Count <= itemsB.Count ? a : b;
                common.AddRange(narrower.Where(EquippedItems.IsHeldTag).Select(Describe));
            }

            return common.Distinct().ToList();
        }

        private static string Describe(string tag) =>
            EquippedItems.IsHeldTag(tag) ? EquippedItems.Describe(tag) : tag;

        /// <summary>When a bind is live, from whichever source describes it. See KnownSituations.For.</summary>
        private static HashSet<string> Situations(BindEntry bind) => KnownSituations.For(bind, out SituationSource _);

        private static string Join(IEnumerable<string> values) => string.Join(" / ", values.OrderBy(v => v).ToArray());

        private static Conflict Classify(BindEntry a, BindEntry b, string key)
        {
            // The game reuses keys across modes on purpose. When we can prove from the game's own
            // code that two of its binds are never read in the same situation, say so.
            if (a.Source == BindSource.Vanilla && b.Source == BindSource.Vanilla)
            {
                if (ContextIndex.AreDisjoint(a.Label, b.Label, out var explanation))
                {
                    return new Conflict
                    {
                        A = a, B = b, Severity = Severity.Note, KeyLabel = key,
                        Reason = $"Both are vanilla binds on {key}, but {explanation}."
                    };
                }

                // Only excuse a pair the game actually shipped this way. Once either side has
                // been rebound the overlap is the player's, and saying "the game ships both"
                // would be untrue as well as unhelpful.
                if (AtDefault(a) && AtDefault(b))
                    return new Conflict
                    {
                        A = a, B = b, Severity = Severity.Note, KeyLabel = key,
                        Reason = $"The game ships both {Name(a)} and {Name(b)} on {key}; we could not determine whether they are ever read at the same time."
                    };
            }

            if (a.Combo.SameModifiers(b.Combo))
            {
                return new Conflict
                {
                    A = a, B = b, Severity = Severity.Hard, KeyLabel = key,
                    Reason = $"{Name(a)} and {Name(b)} both act on exactly {KeyLabels.Of(a.Combo)}, so one press triggers both."
                };
            }

            // Different modifiers only separate two binds if both sides can tell them apart.
            var loose = Unprotected(a) ? a : Unprotected(b) ? b : null;
            if (loose != null)
            {
                var other = ReferenceEquals(loose, a) ? b : a;
                return new Conflict
                {
                    A = a, B = b, Severity = Severity.Soft, KeyLabel = key,
                    Reason = Unmatched(loose, other, key)
                };
            }

            return new Conflict
            {
                A = a, B = b, Severity = Severity.Note, KeyLabel = key,
                Reason = $"Same key, different modifiers ({KeyLabels.Of(a.Combo)} vs {KeyLabels.Of(b.Combo)}), and both check modifiers exactly, so they should not interfere."
            };
        }

        private static string Name(BindEntry e) => $"{e.OwnerName}'s \"{e.Label}\"";

        /// <summary>A bind whose own modifiers cannot be relied on to keep another one off it.</summary>
        private static bool Unprotected(BindEntry e) =>
            e.Modifiers == ModifierBehavior.SingleKey || e.Modifiers == ModifierBehavior.Unknown;

        /// <summary>
        /// Why a different set of modifiers did not separate these two. How sure we are depends
        /// on who does the reading: the game and Jotunn we have read, a mod's own code we have
        /// not, so the mod case says what is likely and names the thing that would change it.
        /// </summary>
        private static string Unmatched(BindEntry loose, BindEntry other, string key)
        {
            var press = $"pressing {KeyLabels.Of(other.Combo)} for {Name(other)}";

            if (loose.Modifiers == ModifierBehavior.Unknown)
                return $"{Name(loose)} is stored as text, so whether it ignores the modifiers on {key} " +
                       $"cannot be seen from here - {press} may well fire it too.";

            if (loose.Source == BindSource.Vanilla || loose.Source == BindSource.Jotunn)
                return $"{Name(loose)} is a single key with no modifiers, and it is read without looking " +
                       $"at any, so {press} fires it as well.";

            return $"{Name(loose)} is a single key with no modifiers to match, so {press} fires it too " +
                   "unless the mod checks them in its own code.";
        }
    }
}
