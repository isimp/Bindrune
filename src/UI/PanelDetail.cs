using System.Collections.Generic;
using System.Linq;
using Bindrune.Conflicts;
using Bindrune.Context;
using Bindrune.Discovery;
using Bindrune.Hints;
using Bindrune.Personal;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Bindrune.UI
{
    /// <summary>
    /// The right-hand column when a bind is selected: everything known about it, and every
    /// control that changes it. The pages shown instead of a bind live in PanelPages.
    /// </summary>
    public static partial class BindrunePanel
    {
        private static void ShowDetail()
        {
            if (_detail == null) return;
            Clear(_detail);

            // Here rather than with the other header labels: these say which page is on show, so
            // they have to follow the page. Clicking a bind redraws the detail and nothing else,
            // so anywhere else would leave "?" or "Hints" still lit over a bind's page.
            TintText(_legendButtonLabel, _page == DetailPage.Legend);
            TintText(_hintsButtonLabel, _page == DetailPage.Hints);

            if (_page == DetailPage.Hints)
            {
                ShowHintsPage();
                return;
            }

            // Nothing selected leaves nothing to say about a bind, so the help page stands in.
            if (_page == DetailPage.Legend || _selected == null)
            {
                ShowLegend();
                return;
            }

            var bind = _selected;
            var width = DetailWidth - 40f;

            Wrapped(bind.OwnerName, _detail, width, 15, new Color(1f, 1f, 1f, 0.7f));
            Wrapped(bind.Label, _detail, width, 21, GUIManager.Instance.ValheimOrange, true);
            Spacer(6f);

            Wrapped(bind.Combo.IsBound ? KeyLabels.Of(bind.Combo) : "not bound", _detail, width, 24, Color.white, true);

            // The config file holds Unity's name, not the label. Only asked for when editing a
            // config by hand, so it waits behind the switch on the help page.
            var stored = bind.Combo.ToString();
            if (_storedNamesShown && bind.Combo.IsBound && KeyLabels.Of(bind.Combo) != stored)
                Wrapped($"stored as {stored}", _detail, width, 12, new Color(1f, 1f, 1f, 0.45f));

            Spacer(8f);

            if (bind.Editable && _pendingFor == bind.Id)
            {
                ShowPendingKey(bind, width);
            }
            else if (bind.Editable)
            {
                var buttons = HorizontalRow(_detail, 34f);
                // Only this button waits when this button started the capture; the search box
                // has its own.
                var capturing = KeyCapture.IsCapturingFor(CapturePurpose.Rebind);

                FixedButton(capturing ? "press a key..." : "Rebind", buttons, 140f, 32f, () => BeginRebind(bind));

                var cancel = FixedButton(capturing ? "Cancel" : "Clear", buttons, 110f, 32f, () =>
                {
                    if (KeyCapture.Active) KeyCapture.Cancel();
                    else BindWriter.Apply(bind, KeyCombo.None, SaveTarget.Personal);
                    Refresh();
                });

                // Told each time it is drawn: this pane is rebuilt when the capture starts, so the
                // button the capture began with is not the one anyone ends up clicking.
                if (capturing) KeyCapture.QuitsOver((RectTransform)cancel.transform);

                var fallback = BindWriter.DefaultOf(bind);
                if (!capturing && fallback.IsBound && !fallback.Equals(bind.Combo))
                {
                    FixedButton("Default", buttons, 110f, 32f, () =>
                    {
                        var problem = BindWriter.Reset(bind);
                        Refresh();
                        if (problem != null) Note(problem);
                    });
                }

                if (capturing)
                    Wrapped("Press any key, or press and release a modifier on its own to bind it. A key nothing else " +
                            "uses is set straight away. Esc cancels.",
                        _detail, width, 13, new Color(1f, 0.85f, 0.4f));
            }
            else
            {
                Wrapped("Locked: " + bind.ReadOnlyReason, _detail, width, 13, new Color(1f, 0.8f, 0.4f));
            }

            Spacer(10f);
            ShowSaveTarget(bind, width);
            ShowOnScreen(bind, width);

            if (Section("about", "What this bind is", width))
            {
                if (!string.IsNullOrEmpty(bind.Description))
                {
                    Wrapped(bind.Description, _detail, width, 13, new Color(1f, 1f, 1f, 0.8f));
                    Spacer(8f);
                }

                Wrapped(Behaviour(bind), _detail, width, 13, new Color(1f, 1f, 1f, 0.65f));
                Spacer(12f);
            }

            ShowSituations(bind, width);

            // "No conflicts" has to mean we looked and found none. For a bind nothing compares,
            // the section above has already said why, and a green all-clear would be misleading.
            if (!bind.Compared) return;

            var conflicts = ConflictIndex.For(bind.Id).OrderBy(c => c.Severity).ToList();
            if (conflicts.Count == 0)
            {
                Spacer(18f);
                Wrapped("No conflicts.", _detail, width, 15, new Color(0.6f, 0.9f, 0.6f));
                return;
            }

            // Never folded: this is the reason the panel exists.
            Spacer(18f);
            Wrapped($"Conflicts ({conflicts.Count})", _detail, width, 17, Color.white, true);
            Spacer(6f);

            foreach (var conflict in conflicts)
            {
                Spacer(14f);
                var muted = MuteStore.IsMuted(conflict);

                var head = HorizontalRow(_detail, 30f);
                // The tag takes all the slack so the two buttons land on the column's right edge.
                var tagWidth = width - 96f - 86f - 38f;
                var severity = conflict.Severity.ToString().ToLowerInvariant() + (conflict.Confirmed ? " - confirmed" : "");
                FixedLabel(muted ? "muted" : severity, head, tagWidth, 24f, 14,
                    muted ? new Color(1f, 1f, 1f, 0.4f) : ColorFor(conflict.Severity), true);

                // A clash is a pair, and half of it is always the bind you are not looking at.
                var other = conflict.A.Id == bind.Id ? conflict.B : conflict.A;
                FixedButton("Go to", head, 86f, 26f, () =>
                {
                    Select(other);
                    Reveal(other);
                    ShowDetail();
                });

                FixedButton(muted ? "Unmute" : "Mute", head, 96f, 26f, () =>
                {
                    MuteStore.Toggle(conflict);
                    Refresh(rescan: false);
                });

                Spacer(4f);
                Wrapped(conflict.Reason, _detail, width, 13,
                    muted ? new Color(1f, 1f, 1f, 0.35f) : new Color(1f, 1f, 1f, 0.85f));
            }
        }

        /// <summary>
        /// Waits for a key and shows what it would clash with before writing it. Nothing is
        /// refused: the first button applies it whatever it clashes with.
        /// </summary>
        private static void BeginRebind(BindEntry bind)
        {
            var id = bind.Id;
            KeyCapture.Begin(CapturePurpose.Rebind, combo => Propose(id, combo));
        }

        /// <summary>
        /// Takes a key for a bind, pressed or picked from the suggestions. One nothing else uses
        /// is written straight away, since there is nothing to weigh; anything else goes to the
        /// preview first, where the clash is seen before a thing is written. A modifier on its
        /// own always goes to the preview, which explains why it cannot be judged.
        /// </summary>
        private static void Propose(string id, KeyCombo combo)
        {
            // The entry the capture began with may have been replaced by a rescan since.
            var bind = BindRegistry.All.FirstOrDefault(b => b.Id == id);

            if (bind != null && combo.IsBound && !KeyCombo.IsModifier(combo.Main) && FreeKeys.Unused(bind, combo))
            {
                var problem = BindWriter.Apply(bind, combo, SaveTarget.Personal);
                _pendingFor = null;
                Refresh();
                if (problem != null) Note(problem);
                return;
            }

            _pendingFor = id;
            _pending = combo;
            Refresh(rescan: false);
        }

        /// <summary>The pressed key, what it would run into, and what to do about it.</summary>
        private static void ShowPendingKey(BindEntry bind, float width)
        {
            var clashes = ConflictEngine.Preview(bind, _pending, BindRegistry.All);

            Wrapped(_pending.IsBound ? KeyLabels.Of(_pending) : "nothing", _detail, width, 22,
                GUIManager.Instance.ValheimOrange, true);
            Spacer(4f);

            // A key nothing uses is written without coming here (see Propose), so there is no
            // "free" case to show: what reaches the preview is a clash, a note or a lone modifier.
            // Saying "free" about Alt would be a half truth anyway: nothing is reported on a
            // modifier because mods share them deliberately, which is not the same as nothing using it.
            if (KeyCombo.IsModifier(_pending.Main))
            {
                Wrapped("A modifier on its own. Bindrune does not report clashes on Alt, Ctrl or Shift, because mods " +
                        "share them on purpose - so it cannot tell you whether this one is free.",
                    _detail, width, 13, new Color(1f, 0.8f, 0.4f));
            }
            else
            {
                foreach (var clash in clashes.Take(4))
                {
                    var other = clash.A.Id == bind.Id ? clash.B : clash.A;
                    var severity = clash.Severity.ToString().ToLowerInvariant() + (clash.Confirmed ? " - confirmed" : "");

                    Wrapped($"{severity}: {other.OwnerName}'s \"{other.Label}\"",
                        _detail, width, 14, ColorFor(clash.Severity), true);
                    Wrapped(clash.Reason, _detail, width, 12, new Color(1f, 1f, 1f, 0.75f));
                    Spacer(6f);
                }

                if (clashes.Count > 4)
                    Wrapped($"...and {clashes.Count - 4} more.", _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));

                // Only when the key needs replacing: one with nothing worse than a note is fine as it is.
                if (clashes.Any(c => c.Severity != Severity.Note)) ShowFreeKeys(bind, width);
            }

            Spacer(8f);

            var row = HorizontalRow(_detail, 34f);

            FixedButton(clashes.Count == 0 ? "Use it" : "Use it anyway", row, 150f, 32f, () =>
            {
                var problem = BindWriter.Apply(bind, _pending, SaveTarget.Personal);
                _pendingFor = null;
                Refresh();
                if (problem != null) Note(problem);
            });

            FixedButton("Try another", row, 130f, 32f, () =>
            {
                _pendingFor = null;
                BeginRebind(bind);
                Refresh(rescan: false);
            });

            FixedButton("Cancel", row, 100f, 32f, () =>
            {
                _pendingFor = null;
                Refresh(rescan: false);
            });

            Wrapped("Nothing has been written yet. Bindrune never refuses a key; this shows the clash before you " +
                    "decide.",
                _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));
        }

        /// <summary>
        /// A few keys near the one pressed that would not clash. Picking one puts it in this
        /// preview as if it had been pressed, so nothing is written until Use it.
        /// </summary>
        private static void ShowFreeKeys(BindEntry bind, float width)
        {
            var options = FreeKeys.For(bind, _pending, 3);
            if (options.Count == 0) return;

            Spacer(4f);
            Wrapped("Free as far as Bindrune can see", _detail, width, 14, new Color(0.6f, 0.9f, 0.6f), true);
            Spacer(2f);

            foreach (var option in options)
            {
                var combo = option.Combo;
                var row = HorizontalRow(_detail, 30f);

                FixedButton(KeyLabels.Of(combo), row, 190f, 28f, () => Propose(bind.Id, combo));
                FixedLabel(option.Why, row, width - 200f, 28f, 13, new Color(1f, 1f, 1f, 0.75f));

                // A shared key is offered on purpose, but it should say so.
                if (option.Shared.Count > 0)
                    Wrapped(SharedWith(option.Shared, bind), _detail, width, 12, new Color(1f, 1f, 1f, 0.5f));
            }
        }

        private static string SharedWith(List<Conflict> shared, BindEntry bind)
        {
            if (shared.Count > 1) return $"Shared with {shared.Count} binds, none of which should interfere.";

            var other = shared[0].A.Id == bind.Id ? shared[0].B : shared[0].A;
            return $"Shared with {other.OwnerName}'s \"{other.Label}\", which should not interfere.";
        }

        /// <summary>
        /// Editing something that came from key hints or the shared list starts from what was
        /// already shown, rather than from nothing: your own situations replace that source
        /// outright, so without this the first click would drop everything else it supplied.
        /// </summary>
        private static void Adopt(BindEntry bind, HashSet<string> situations, SituationSource source)
        {
            if (source != SituationSource.KeyHints &&
                source != SituationSource.Pack &&
                source != SituationSource.Game) return;

            foreach (var situation in situations) SituationStore.Toggle(bind.Id, situation);
        }

        /// <summary>
        /// Who a rebind belongs to. Keys kept as yours are restored after a profile sync wipes the
        /// mod's config; keys given to the profile are shared with everyone on it instead.
        /// </summary>
        private static void ShowSaveTarget(BindEntry bind, float width)
        {
            if (!PersonalKeys.Eligible(bind)) return;

            // The highlight says where the key in use right now came from, not what a future
            // rebind might do - anything else describes an intention the bind does not have.
            var yours = PersonalKeys.IsPersonal(bind.Id);
            var entry = PersonalKeys.Get(bind.Id);

            Wrapped("This key comes from", _detail, width, 15, Color.white, true);
            Spacer(4f);

            var row = HorizontalRow(_detail, 34f);
            var half = (width - 12f) / 2f;

            var forYou = FixedButton(yours ? "* Just for you" : "Just for you", row, half, 30f, () =>
            {
                PersonalKeys.UsePersonal(bind);
                Refresh();
            });
            TintButton(forYou, yours);

            var forProfile = FixedButton(yours ? "Whole profile" : "* Whole profile", row, half, 30f, () =>
            {
                PersonalKeys.UseProfile(bind);
                Refresh();
            });
            TintButton(forProfile, !yours);

            Wrapped(yours
                    ? "Yours, and kept when the profile syncs: if a sync overwrites this key, Bindrune puts yours back."
                    : "The profile's, shared with everyone on it. Rebinding it makes the new key yours.",
                _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));

            // Both keys are kept, so say what switching would put back.
            if (entry != null)
            {
                var other = yours ? entry.Profile : entry.Personal;
                if (other.IsBound)
                    Wrapped(yours ? $"The profile's key is {KeyLabels.Of(other)}." : $"Your key is {KeyLabels.Of(other)}.",
                        _detail, width, 12, new Color(1f, 1f, 1f, 0.45f));
            }

            Spacer(12f);
        }

        /// <summary>
        /// Puts a bind on the HUD. Opt-in one at a time, because showing everything would be a wall
        /// of hints; the second button takes all of one mod's binds at once.
        /// </summary>
        private static void ShowOnScreen(BindEntry bind, float width)
        {
            // Nothing to show for an unbound key, and a gamepad button is not ours to draw.
            if (!bind.Combo.IsBound || !bind.Compared) return;

            var showing = HintChoice.Shows(bind.Id);

            Wrapped("On screen", _detail, width, 15, Color.white, true);
            Spacer(4f);

            var row = HorizontalRow(_detail, 34f);
            var half = (width - 12f) / 2f;

            var toggle = FixedButton(showing ? "* Showing" : "Show on screen", row, half, 30f, () =>
            {
                HintChoice.Toggle(bind.Id);
                Refresh(rescan: false);
            });
            TintButton(toggle, showing);

            var siblings = BindRegistry.All
                .Where(b => b.OwnerGuid == bind.OwnerGuid && b.Combo.IsBound && b.Compared)
                .Select(b => b.Id).ToList();
            var allShowing = siblings.All(HintChoice.Shows);

            FixedButton(allShowing ? "None from this mod" : "All from this mod", row, half, 30f, () =>
            {
                if (allShowing) HintChoice.Remove(siblings);
                else HintChoice.Add(siblings);
                Refresh(rescan: false);
            });

            var already = SelfHinting.Describe(bind);
            if (already != null)
                Wrapped(already + " Showing it here as well would say it twice.",
                    _detail, width, 12, new Color(1f, 0.8f, 0.4f));
            else
                Wrapped($"Appears in the corner while its situations apply. {Plugin.HintsKeyText} shows and hides the list.",
                    _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));

            // A hint that is always on screen is the thing this feature was meant to avoid, so
            // say what would fix it rather than quietly showing it the whole time.
            if (showing && KnownSituations.For(bind, out SituationSource _).Count == 0)
                Wrapped("Nothing says when this one applies, so it will show the whole time the list is up. " +
                        "Filling in \"Applies when\" below narrows it.",
                    _detail, width, 12, new Color(1f, 0.8f, 0.4f));

            Spacer(12f);
        }

        /// <summary>
        /// What the profile would put here instead, shown only when it actually differs - so the
        /// column stays empty for the many binds nobody has touched.
        /// </summary>
        private static string ProfileKeyNote(BindEntry bind, bool yours)
        {
            if (!yours) return "";

            var entry = PersonalKeys.Get(bind.Id);
            if (entry == null || !entry.Profile.IsBound || entry.Profile.Equals(bind.Combo)) return "";

            return "profile: " + KeyLabels.Of(entry.Profile);
        }

        /// <summary>
        /// A foldable heading. Returns whether its contents should be drawn, so a section costs
        /// one line until you ask for it.
        /// </summary>
        private static bool Section(string key, string title, float width)
        {
            var open = !_collapsed.Contains(key);

            Spacer(8f);
            var button = FixedButton((open ? "  -   " : "  +   ") + title, _detail, width, 30f, () =>
            {
                if (!_collapsed.Remove(key)) _collapsed.Add(key);
                ShowDetail();
            });

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleLeft;
                text.color = open ? GUIManager.Instance.ValheimOrange : new Color(1f, 1f, 1f, 0.75f);
            }

            if (open) Spacer(8f);
            return open;
        }

        /// <summary>Filter labels: smaller and dimmer than the window buttons, lit when active.</summary>
        private static void SetFilter(Text label, string text, bool active)
        {
            if (label == null) return;

            label.text = text;
            label.fontSize = 15;
            label.color = active ? GUIManager.Instance.ValheimOrange : new Color(1f, 1f, 1f, 0.55f);
        }

        private static void TintButton(GameObject button, bool active) =>
            TintText(button.GetComponentInChildren<Text>(), active);

        private static void TintText(Text text, bool active)
        {
            if (text != null)
                text.color = active ? GUIManager.Instance.ValheimOrange : new Color(1f, 1f, 1f, 0.6f);
        }

        /// <summary>
        /// When a bind is actually live: where it applies, and what it needs in your hands.
        /// Whatever another source already knew is shown here too, so this is one editor over
        /// five answers - KnownSituations decides which of them wins.
        /// </summary>
        private static void ShowSituations(BindEntry bind, float width)
        {
            // Marking a bind nothing compares would be a dead end: no conflict could be ruled out
            // or confirmed by the answer, so do not ask for it.
            if (!bind.Compared)
            {
                Spacer(12f);
                Wrapped("Not compared with anything else: it is on another device, and the game does not let it be " +
                        "changed from here or anywhere else.",
                    _detail, width, 13, new Color(1f, 1f, 1f, 0.6f));
                Spacer(8f);
                return;
            }

            var situations = KnownSituations.For(bind, out var source);
            var fromMod = source == SituationSource.Mod;
            if (!Section("situations", situations.Count > 0 ? $"Applies when ({situations.Count})" : "Applies when", width)) return;

            var provenance = Provenance(source, bind.OwnerName);
            if (provenance != null)
            {
                Wrapped(provenance, _detail, width, 12,
                    fromMod ? new Color(1f, 0.8f, 0.4f) : new Color(1f, 1f, 1f, 0.5f));
                Spacer(6f);
            }

            // The game's own names for where it reads the button, which are finer than the list
            // below - worth showing in full even where nothing maps onto a situation.
            var contexts = bind.Source == BindSource.Vanilla ? ContextIndex.For(bind.Label) : null;
            if (contexts != null && contexts.Count > 0)
                Wrapped("Read by the game's own code in: " + string.Join(", ", contexts.OrderBy(d => d).ToArray()),
                    _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));

            Spacer(4f);

            const int perRow = 2;
            const float buttonHeight = 32f, buttonGap = 12f;
            var buttonWidth = (width - buttonGap) / perRow;

            Transform row = null;
            for (var i = 0; i < SituationTags.All.Length; i++)
            {
                if (i % perRow == 0) row = HorizontalRow(_detail, buttonHeight + 8f);

                var tag = SituationTags.All[i];
                var active = situations.Contains(tag);

                var button = FixedButton(active ? "* " + tag : tag, row, buttonWidth, buttonHeight, () =>
                {
                    if (fromMod) return;

                    Adopt(bind, situations, source);
                    SituationStore.Toggle(bind.Id, tag);
                    Refresh();
                });

                var clickable = button.GetComponent<Button>();
                if (clickable != null) clickable.interactable = !fromMod;

                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                    text.color = fromMod
                        ? new Color(1f, 1f, 1f, active ? 0.5f : 0.25f)
                        : active ? GUIManager.Instance.ValheimOrange : new Color(1f, 1f, 1f, 0.65f);
            }

            Spacer(10f);

            if (fromMod)
            {
                var held = situations.Where(EquippedItems.IsHeldTag).Select(EquippedItems.Describe).ToList();
                if (held.Count > 0)
                    Wrapped("Only when holding: " + string.Join(", ", held.ToArray()),
                        _detail, width, 12, new Color(1f, 1f, 1f, 0.5f));

                Spacer(18f);
                return;
            }

            ShowHeldItems(bind, width, situations, source);

            Wrapped("Two clashing binds that never apply at the same time stop being reported. " +
                    "Two that do apply together are marked as confirmed.",
                _detail, width, 12, new Color(1f, 1f, 1f, 0.5f));
            Spacer(18f);
        }

        /// <summary>
        /// Whose answer is on show, and whether clicking will change it. A mod that describes its
        /// own bind is final: its author knows when their code runs, and no amount of guessing
        /// from the outside beats that. Every other source is a starting point you can take over.
        /// </summary>
        private static string Provenance(SituationSource source, string ownerName)
        {
            switch (source)
            {
                case SituationSource.Mod:
                    return $"{ownerName} describes this bind itself, so these cannot be changed here.";
                case SituationSource.KeyHints:
                    return $"Taken from {ownerName}'s own key hints. Change anything to make it yours instead.";
                case SituationSource.Pack:
                    return "From the shared list that travels with this profile. Change anything to make it yours instead.";
                case SituationSource.Game:
                    return "What the game does with its own bind. Change anything to make it yours instead.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Binds that only matter with something in hand: a pickaxe for a digging mod, one
        /// specific remote for another. Groups come first so "any pickaxe" is one click.
        /// </summary>
        private static void ShowHeldItems(BindEntry bind, float width, HashSet<string> situations, SituationSource source)
        {
            // Read from the resolved situations, not just the ones you set: an item a mod's key
            // hints supplied is still an item this bind needs in hand.
            var current = situations.Where(EquippedItems.IsHeldTag).ToList();

            Spacer(6f);
            Wrapped("...and only when holding", _detail, width, 13, new Color(1f, 1f, 1f, 0.7f), true);
            Spacer(4f);

            foreach (var tag in current)
            {
                var row = HorizontalRow(_detail, 28f);
                FixedLabel(EquippedItems.Describe(tag), row, width - 106f, 24f, 14, GUIManager.Instance.ValheimOrange);

                FixedButton("Remove", row, 96f, 26f, () =>
                {
                    Adopt(bind, situations, source);
                    SituationStore.Toggle(bind.Id, tag);
                    Refresh();
                });
            }

            if (!EquippedItems.Known)
            {
                Wrapped("The item list is built from the game's item database, which is only filled once you load a world. Load a save and rescan.",
                    _detail, width, 12, new Color(1f, 1f, 1f, 0.5f));
                Spacer(10f);
                return;
            }

            // A dropdown has no separator row, so the heading over the items nobody found a way
            // to obtain is an ordinary line with no tag behind it, which the handler refuses.
            var labels = new List<string> { "add an item or group..." };
            var tags = new List<string> { null };
            var divided = false;

            foreach (var choice in EquippedItems.Choices().Where(c => !current.Contains(c.Tag)))
            {
                if (!choice.Obtainable && !divided)
                {
                    divided = true;
                    labels.Add("--- no obvious way to obtain ---");
                    tags.Add(null);
                }

                labels.Add(choice.Label);
                tags.Add(choice.Tag);
            }

            var dropdownObject = GUIManager.Instance.CreateDropDown(_detail, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, 15, width, 30f);
            Fix(dropdownObject, width, 30f);

            var dropdown = dropdownObject.GetComponent<Dropdown>();
            TuneDropdownScroll(dropdown);
            dropdown.ClearOptions();
            dropdown.AddOptions(labels);
            dropdown.value = 0;
            dropdown.onValueChanged.AddListener(index =>
            {
                var chosen = index > 0 && index < tags.Count ? tags[index] : null;
                if (chosen == null)
                {
                    dropdown.value = 0;
                    return;
                }

                Adopt(bind, situations, source);
                SituationStore.Toggle(bind.Id, chosen);
                Refresh();
            });

            Spacer(10f);
        }

        /// <summary>
        /// A dropdown builds its list by cloning its template when opened, so the template's
        /// scroll rect is where the wheel speed has to be set - the list itself does not exist yet.
        /// </summary>
        private static void TuneDropdownScroll(Dropdown dropdown)
        {
            var template = dropdown != null ? dropdown.template : null;
            if (template == null) return;

            var scroll = template.GetComponent<ScrollRect>() ?? template.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null) scroll.scrollSensitivity = Plugin.ScrollSpeed;
        }

        /// <summary>
        /// What is known about this bind, kept to what its storage proves. A setting holding one
        /// key cannot require a modifier - but its owner may still look at Alt itself, and no
        /// amount of reading the config would show that, so the wording says so rather than
        /// stating a behaviour we have not seen.
        /// </summary>
        private static string Behaviour(BindEntry bind)
        {
            string modifiers;
            if (bind.Source == BindSource.Gamepad)
                modifiers = "A gamepad button, read from whichever controller layout you picked. Modifiers do not enter into it.";
            // Only the keys the game reads in its own code carry a modifier on the game's side,
            // and it checks that one is down, not that nothing else is.
            else if (bind.Source == BindSource.Vanilla && bind.Modifiers == ModifierBehavior.Strict)
                modifiers = "Fires while this modifier is held, whatever else is held with it.";
            else if (bind.Modifiers == ModifierBehavior.Strict)
                modifiers = "Needs exactly these modifiers, and will not fire while any other key is held (including movement keys).";
            else if (bind.Modifiers == ModifierBehavior.Unknown)
                modifiers = "Stored as text, so whether modifiers matter here cannot be told from the setting.";
            else if (bind.Source == BindSource.Vanilla || bind.Source == BindSource.Jotunn)
                modifiers = "A single key, read without looking at modifiers, so it fires while Alt, Ctrl or Shift are held too.";
            else
                modifiers = "A single key, with no modifier to match, so it fires while Alt, Ctrl or Shift are held - " +
                            "unless the mod checks them in its own code, which the setting does not show.";

            return $"{modifiers}\nFound as: {SourceLabel(bind.Source)}.";
        }

        /// <summary>Where the bind came from, said in words rather than in the enum's name.</summary>
        private static string SourceLabel(BindSource source)
        {
            switch (source)
            {
                case BindSource.Vanilla: return "one of the game's own controls";
                case BindSource.Gamepad: return "one of the game's gamepad buttons";
                case BindSource.Jotunn: return "a mod button registered through Jotunn";
                case BindSource.ModTyped: return "a keybind setting in the mod's config";
                default: return "a text setting in the mod's config that reads as a key";
            }
        }

        private static void Note(string message)
        {
            Wrapped(message, _detail, DetailWidth - 40f, 13, new Color(1f, 0.7f, 0.4f));
        }
    }
}
