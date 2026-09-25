using System;
using System.Collections.Generic;
using System.Linq;
using Bindrune.Conflicts;
using Bindrune.Context;
using Bindrune.Discovery;
using Bindrune.Personal;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Bindrune.UI
{
    /// <summary>
    /// The in-game window. Left column lists every bind from every source; right column explains
    /// the selected one in full, so nothing is hidden behind a truncation or an external file.
    /// </summary>
    public static partial class BindrunePanel
    {
        private const float RowHeight = 26f;
        private const float TopChrome = 124f;
        private const float FooterHeight = 38f;
        private const float Margin = 30f;
        private const float Gap = 18f;
        private const float DetailWidth = 470f;

        private static Vector2 _size;

        private static float PanelWidth => Size.x;
        private static float PanelHeight => Size.y;

        private static Vector2 Size
        {
            get
            {
                if (_size == Vector2.zero)
                    _size = new Vector2(
                        Mathf.Min(Plugin.PanelSize.x, Screen.width * 0.95f),
                        Mathf.Min(Plugin.PanelSize.y, Screen.height * 0.95f));
                return _size;
            }
        }

        private const float MarkWidth = 96f;
        private const float KeyWidth = 200f;
        private const float ProfileKeyWidth = 150f;

        private struct RowColumns
        {
            public float Mark, Label, Key, Profile;
        }

        /// <summary>
        /// Splits a row into columns, giving up the least useful ones first as the panel narrows.
        /// The bind's name is what you are actually reading, so it shrinks last: the profile
        /// column goes, then the key column narrows, then the conflict mark.
        /// </summary>
        private static RowColumns Columns()
        {
            const float pad = 14f, gap = 8f, labelFloor = 240f, keyFloor = 130f, markFloor = 62f;

            float mark = MarkWidth, key = KeyWidth, profile = ProfileKeyWidth;

            float LabelFor(float b, float k, float p) =>
                RowWidth - pad - b - k - (p > 0f ? p + gap : 0f) - gap * 2f;

            var label = LabelFor(mark, key, profile);

            if (label < labelFloor)
            {
                profile = 0f;
                label = LabelFor(mark, key, 0f);
            }

            if (label < labelFloor)
            {
                var taken = Mathf.Min(key - keyFloor, labelFloor - label);
                key -= taken;
                label += taken;
            }

            if (label < labelFloor)
            {
                var taken = Mathf.Min(mark - markFloor, labelFloor - label);
                mark -= taken;
                label += taken;
            }

            return new RowColumns { Mark = mark, Label = Mathf.Max(label, 80f), Key = key, Profile = profile };
        }
        private static float BodyHeight => PanelHeight - TopChrome - FooterHeight;
        private static float ListWidth => PanelWidth - (Margin * 2f) - Gap - DetailWidth;
        private static float RowWidth => ListWidth - 24f;

        private static GameObject _root;
        private static InputField _search;

        /// <summary>
        /// Where the list was left. The filters, the grouping and the collapsed sections are
        /// static and outlive the window on their own, but the search field and the scroll view
        /// are new objects every time it is built, so these two have to be carried over by hand.
        /// Kept for the session only, since a search you have forgotten setting is no way to open
        /// the panel days later.
        /// </summary>
        private static string _searchText = "";

        /// <summary>1 is the top of the list, which is where a first open starts.</summary>
        private static float _scrollAt = 1f;
        private static RectTransform _content;
        private static RectTransform _detail;
        private static Text _summary;

        private static bool _groupByKey;
        private static bool _mutedOnly;
        private static float _repopulateAt;
        private static string _rendered;
        private static Text _groupButtonLabel;
        private static Text _mutedButtonLabel;
        private static Text _pressKeyLabel;

        private static bool _yoursOnly;
        private static bool _conflictsOnly;
        private static bool _internalShown;
        private static bool _storedNamesShown;
        private static Text _yoursButtonLabel;
        private static Text _conflictsButtonLabel;

        /// <summary>
        /// Which detail sections are folded away, kept between selections so the pane does not
        /// spring back open every time you click a different bind.
        /// </summary>
        private static readonly HashSet<string> _collapsed =
            new HashSet<string> { "about", "situations" };

        /// <summary>What the right-hand column is showing.</summary>
        private enum DetailPage
        {
            /// <summary>The selected bind, or the help page when nothing is selected.</summary>
            Bind,
            Legend,
            Hints
        }

        private static DetailPage _page = DetailPage.Bind;
        private static Text _legendButtonLabel;
        private static Text _hintsButtonLabel;

        /// <summary>
        /// Switches the right-hand column to one of the pages that is not a bind. Pressing the
        /// same button again goes back to the bind you were reading, so a look at the help page
        /// does not cost you your place.
        /// </summary>
        private static void Show(DetailPage page)
        {
            _page = _page == page && _selected != null ? DetailPage.Bind : page;
            _note = null;
            Refresh(rescan: false);
        }

        private static BindEntry _selected;

        /// <summary>
        /// A key you have pressed for a rebind but not yet agreed to. Held by bind id so it can
        /// never be shown against the wrong bind, and so a rescan replacing the entry objects
        /// does not lose it.
        /// </summary>
        private static string _pendingFor;
        private static KeyCombo _pending;

        /// <summary>
        /// What the last thing you did to this bind has to say, such as why a key was not set.
        /// Held rather than drawn on the spot, so it lands beside the controls it answers however
        /// many times the pane is rebuilt after.
        /// </summary>
        private static string _note;

        /// <summary>Row backgrounds by bind id, so selecting one recolours two images instead of
        /// rebuilding every row in the list.</summary>
        private static readonly Dictionary<string, Image> _rowBackgrounds = new Dictionary<string, Image>();

        public static bool IsOpen => _root != null;

        private static int _closedFrame = -1;

        /// <summary>
        /// Whether the keyboard is the panel's this frame: while it is open, and in the frame it
        /// closed in, so the key that closed it reaches nothing else.
        /// </summary>
        public static bool HoldsKeyboard => IsOpen || Time.frameCount == _closedFrame;

        /// <summary>
        /// True while the search box has the keyboard. Our hotkeys are ordinary key checks, so
        /// without this, typing a letter that happens to be one of them would fire it.
        /// </summary>
        public static bool Typing => _search != null && _search.isFocused;

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public static void Open()
        {
            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                Plugin.Log.LogWarning("Bindrune: GUI is not ready yet.");
                return;
            }

            Rescan();
            Build();
            RestorePlace();
            GUIManager.BlockInput(true);

            // While you are in here, the hints can be dragged into place.
            Hints.HintOverlay.Movable = true;

            if (_root != null) Sfx.Play(Sfx.PanelOpen);
        }

        /// <param name="quietly">For the game shutting down, which is no moment to play a sound.</param>
        public static void Close(bool quietly = false)
        {
            if (_root != null && !quietly) Sfx.Play(Sfx.PanelClose);
            if (_root != null) _closedFrame = Time.frameCount;

            KeyCapture.Changed = null;
            KeyCapture.Cancel();
            _pendingFor = null;
            // What the last thing you did had to say is spent: it would otherwise be waiting on
            // the same bind the next time the panel opens, as though it had just happened.
            _note = null;
            RememberPlace();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _content = null;
            _detail = null;
            _modal = null;
            _spareButton = null;
            GUIManager.BlockInput(false);
            Hints.HintOverlay.Movable = false;
        }

        public static void Tick()
        {
            KeyCapture.Tick();

            // Setting the first key of your own in a profile a mod manager may replace is when to ask.
            if (_askSpare)
            {
                _askSpare = false;
                AskAboutSpareCopyIfDue();
            }

            if (_repopulateAt > 0f && Time.realtimeSinceStartup >= _repopulateAt)
            {
                _repopulateAt = 0f;
                Populate();
            }
        }

        /// <summary>
        /// Typing fires a change per keystroke, and rebuilding every row each time is wasted work
        /// nobody sees. Wait for a pause instead.
        /// </summary>
        private static void RequestPopulate() => _repopulateAt = Time.realtimeSinceStartup + 0.15f;

        /// <summary>
        /// Redraws everything the panel shows after something changed. Every action needs the same
        /// steps in the same order, and forgetting one is how a stale row, a stale detail pane or
        /// an overlay still naming the old key happens.
        /// </summary>
        private static void Refresh(bool rescan = true)
        {
            if (rescan) Rescan();

            Populate();
            ShowDetail();

            // Anything that changed a key or a situation changed what the overlay should say.
            Hints.HintOverlay.Invalidate();
        }

        /// <summary>Puts both capture buttons back to whatever the current capture state is.</summary>
        private static void RefreshCaptureState()
        {
            if (_root == null) return;

            if (_pressKeyLabel != null)
                _pressKeyLabel.text = KeyCapture.IsCapturingFor(CapturePurpose.Search) ? "press a key..." : "Press key";

            ShowDetail();
        }

        private static void Rescan()
        {
            // Files edited by hand while the game runs are read again, so the panel shows them.
            SituationStore.Sync();
            PersonalStore.Sync();

            BindRegistry.Refresh();
            EquippedItems.Refresh();

            // Internal game plumbing would otherwise clash with the real control it mirrors:
            // "MouseLeft" is not a second binding for Attack, it is the same button. Binds that
            // are listed to be read rather than compared - gamepad buttons - stay out as well.
            ConflictIndex.Rebuild(BindRegistry.All.Where(b => !b.Internal && b.Compared));

            // The scan built new BindEntry objects, so the selection has to follow by id.
            if (_selected != null)
                _selected = BindRegistry.All.FirstOrDefault(b => b.Id == _selected.Id);
        }

        // ---------- construction ----------

        private static void Build()
        {
            _root = GUIManager.Instance.CreateWoodpanel(
                GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                PanelWidth, PanelHeight, true);
            _root.name = "BindrunePanel";
            _modal = null;
            SpareCopy.Followed -= OnSpareFollowed;
            SpareCopy.Followed += OnSpareFollowed;
            ((RectTransform)_root.transform).anchoredPosition = Vector2.zero;

            // A fresh panel has no rows, so the previous signature must not suppress the first draw.
            _rendered = null;

            // Cancelling a capture has to reach the button that is showing "press a key...".
            KeyCapture.Changed = RefreshCaptureState;

            BuildHeader();

            var listScroll = MakeScrollView(ListWidth, BodyHeight,
                new Vector2(Margin + ListWidth / 2f, -(TopChrome + BodyHeight / 2f)), out _content);
            if (listScroll == null) return;

            MakeScrollView(DetailWidth, BodyHeight,
                new Vector2(Margin + ListWidth + Gap + DetailWidth / 2f, -(TopChrome + BodyHeight / 2f)), out _detail, 16);

            var summary = Label("", _root.transform, PanelWidth - 120f, 22, 14, Color.white);
            Anchor(summary, new Vector2(0.5f, 0f), new Vector2(0f, FooterHeight / 2f));
            _summary = summary.GetComponent<Text>();
            _summary.alignment = TextAnchor.MiddleCenter;

            BuildResizeGrip();

            Refresh(rescan: false);
            AskAboutSpareCopyIfDue();
        }

        /// <summary>
        /// Rebuilds at the current size. The search text and the scroll position need carrying
        /// over: everything else the panel shows lives in static state that outlives the window,
        /// while those two belong to objects that are replaced with empty ones.
        /// </summary>
        private static void Rebuild()
        {
            RememberPlace();

            if (_root != null) UnityEngine.Object.Destroy(_root);
            Build();
            RestorePlace();
        }

        private static void BuildResizeGrip()
        {
            var grip = new GameObject("resize", typeof(RectTransform), typeof(Image), typeof(ResizeGrip));
            grip.transform.SetParent(_root.transform, false);

            var rect = (RectTransform)grip.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(26f, 26f);
            rect.anchoredPosition = new Vector2(-6f, 6f);

            grip.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.22f);

            var handler = grip.GetComponent<ResizeGrip>();
            handler.Target = (RectTransform)_root.transform;
            // Narrower than this and the filter row runs off the edge of the panel.
            handler.MinSize = new Vector2(1080f, 560f);
            handler.Resized = size =>
            {
                _size = size;
                Plugin.PanelSize = size;
                Rebuild();
            };
        }

        private static void BuildHeader()
        {
            const float titleRow = -36f;
            const float filterRow = -80f;

            var title = Label("Bindrune", _root.transform, 200f, 32f, 26, GUIManager.Instance.ValheimOrange, true);
            AnchorLeft(title, Margin, titleRow);

            var close = Button("Close", _root.transform, 110f, 32f, () => Close());
            AnchorRight(close, -Margin, titleRow);

            var rescan = Button("Rescan", _root.transform, 110f, 32f, () =>
            {
                Refresh();
            });
            AnchorRight(rescan, -Margin - 120f, titleRow);

            var legend = Button("?", _root.transform, 44f, 32f, () => Show(DetailPage.Legend));
            AnchorRight(legend, -Margin - 240f, titleRow);
            _legendButtonLabel = legend.GetComponentInChildren<Text>();

            // Its own page rather than another fold on the help page, since it has settings and a
            // list of its own.
            var hints = Button("Hints", _root.transform, 100f, 32f, () => Show(DetailPage.Hints));
            AnchorRight(hints, -Margin - 294f, titleRow);
            _hintsButtonLabel = hints.GetComponentInChildren<Text>();

            BuildSpareButton(-Margin - 404f, titleRow);

            var searchObject = GUIManager.Instance.CreateInputField(_root.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                InputField.ContentType.Standard, "search mod, bind or \"key\"...", 16, 300f, 32f);
            AnchorLeft(searchObject, Margin, filterRow);
            _search = searchObject.GetComponent<InputField>();
            _search.onValueChanged.AddListener(_ => RequestPopulate());

            var clear = Button("x", _root.transform, 32f, 32f, () =>
            {
                if (_search != null) _search.text = "";
                Populate();
            });
            AnchorLeft(clear, Margin + 306f, filterRow);

            // Declared first so its own handler can name it: this button both starts the capture
            // and is how you get out of one.
            GameObject pressKey = null;
            pressKey = Button("Press key", _root.transform, 120f, 32f, () =>
            {
                if (KeyCapture.IsCapturingFor(CapturePurpose.Search))
                {
                    KeyCapture.Cancel();
                    return;
                }

                KeyCapture.Begin(CapturePurpose.Search, combo =>
                {
                    // The label, so the box says what the rows say. A label can be punctuation,
                    // even a quote mark on some layouts, which is safe because Matches strips
                    // only the outer pair of quotes and takes everything between them.
                    if (_search != null) _search.text = "\"" + KeyLabels.Heading(combo) + "\"";
                    Populate();
                });

                // After Begin, which clears it: clicking this button again is how the search
                // capture is called off, so that click has to reach it.
                KeyCapture.QuitsOver((RectTransform)pressKey.transform);
            });
            AnchorLeft(pressKey, Margin + 348f, filterRow);
            _pressKeyLabel = pressKey.GetComponentInChildren<Text>();

            // Every way of narrowing the list sits together, as buttons that look alike, rather
            // than a mix of toggles and buttons spread over two rows.
            const float filterWidth = 118f;
            var filterStart = Margin + 486f;

            var conflictsFilter = Button("Conflicts", _root.transform, filterWidth, 28f, () =>
            {
                _conflictsOnly = !_conflictsOnly;
                Populate();
            });
            AnchorLeft(conflictsFilter, filterStart, filterRow);
            _conflictsButtonLabel = conflictsFilter.GetComponentInChildren<Text>();

            var yoursFilter = Button("Yours", _root.transform, filterWidth, 28f, () =>
            {
                _yoursOnly = !_yoursOnly;
                Populate();
            });
            AnchorLeft(yoursFilter, filterStart + filterWidth + 8f, filterRow);
            _yoursButtonLabel = yoursFilter.GetComponentInChildren<Text>();

            var mutedFilter = Button("Muted", _root.transform, filterWidth, 28f, () =>
            {
                _mutedOnly = !_mutedOnly;
                Populate();
            });
            AnchorLeft(mutedFilter, filterStart + (filterWidth + 8f) * 2f, filterRow);
            _mutedButtonLabel = mutedFilter.GetComponentInChildren<Text>();

            // Grouping belongs with the things that shape the list, not with the window buttons.
            var grouping = Button("Group: mod", _root.transform, 148f, 28f, () =>
            {
                _groupByKey = !_groupByKey;
                Populate();
            });
            AnchorLeft(grouping, filterStart + (filterWidth + 8f) * 3f, filterRow);
            _groupButtonLabel = grouping.GetComponentInChildren<Text>();

            // Column captions, so the marks in the list are labelled where they appear.
            const float captionY = -(TopChrome - 14f);
            var columns = Columns();
            var caret = Margin + 20f;

            var conflictCaption = Label("conflict", _root.transform, columns.Mark, 20f, 13, new Color(1f, 1f, 1f, 0.45f));
            AnchorLeft(conflictCaption, caret, captionY);
            caret += columns.Mark + 8f;

            var bindCaption = Label("bind", _root.transform, columns.Label, 20f, 13, new Color(1f, 1f, 1f, 0.45f));
            AnchorLeft(bindCaption, caret, captionY);
            caret += columns.Label + 8f;

            var keyCaption = Label("key", _root.transform, columns.Key, 20f, 13, new Color(1f, 1f, 1f, 0.45f));
            AnchorLeft(keyCaption, caret, captionY);
            caret += columns.Key + 8f;

            if (columns.Profile > 0f)
            {
                var profileCaption = Label("profile says", _root.transform, columns.Profile, 20f, 13, new Color(1f, 1f, 1f, 0.35f));
                AnchorLeft(profileCaption, caret, captionY);
            }
        }

        /// <summary>Creates a scroll view and hands back the content transform rows go into.</summary>
        private static GameObject MakeScrollView(float width, float height, Vector2 position, out RectTransform content, int padding = 6)
        {
            content = null;

            var scroll = GUIManager.Instance.CreateScrollView(
                _root.transform, false, true, 8f, 10f, GUIManager.Instance.ValheimScrollbarHandleColorBlock,
                new Color(0f, 0f, 0f, 0.25f), width, height);
            Anchor(scroll, new Vector2(0f, 1f), position);

            // Jotunn hands back a container; the ScrollRect itself sits on a child of it.
            var scrollRect = scroll.GetComponent<ScrollRect>() ?? scroll.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect == null)
            {
                Plugin.Log.LogError("Bindrune: scroll view has no ScrollRect.");
                return null;
            }

            scrollRect.scrollSensitivity = Plugin.ScrollSpeed;
            content = scrollRect.content;

            var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = 2f;

            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return scroll;
        }

        // ---------- bind list ----------

        private static void Populate()
        {
            if (_content == null) return;

            var filter = (_search != null ? _search.text : "").Trim().ToLowerInvariant();
            var conflictsOnly = _conflictsOnly;

            var showInternal = _internalShown;

            var shown = BindRegistry.All.Where(b =>
            {
                if (b.Internal && !showInternal) return false;
                if (conflictsOnly && ConflictIndex.Worst(b.Id) == null) return false;
                if (_mutedOnly && ConflictIndex.MutedCount(b.Id) == 0) return false;
                if (_yoursOnly && !PersonalKeys.IsPersonal(b.Id)) return false;
                return Matches(b, filter);
            }).ToList();

            if (_groupByKey)
                shown = shown
                    .OrderBy(b => b.Combo.IsBound ? 0 : 1)
                    .ThenBy(b => b.Combo.MainToken)
                    .ThenBy(b => b.OwnerName)
                    .ToList();

            // Rebuilding every row to produce the same picture is the expensive part, and
            // narrowing a search often does not change what is on screen at all.
            var signature = Signature(shown);
            if (signature == _rendered)
            {
                UpdateHeaderLabels(shown.Count);
                return;
            }

            _rendered = signature;
            Clear(_content);
            _rowBackgrounds.Clear();

            string group = null;
            foreach (var bind in shown)
            {
                // The key's label, or its raw input path when it has no KeyCode; otherwise such a
                // bind would be headed "None". Groups still form on MainToken, which is raw.
                var heading = _groupByKey ? KeyLabels.Heading(bind.Combo) : bind.OwnerName;

                if (heading != group)
                {
                    group = heading;
                    var header = Label(heading, _content, RowWidth, RowHeight + 6f, 17, GUIManager.Instance.ValheimOrange, true);
                    Fix(header, RowWidth, RowHeight + 6f);
                }

                AddRow(bind);
            }

            UpdateHeaderLabels(shown.Count);
        }

        private static void UpdateHeaderLabels(int shownCount)
        {
            var all = ConflictIndex.All;

            // Not a filter: grouping is always on, this only says which way, so it is not styled
            // as an on/off toggle.
            if (_groupButtonLabel != null)
            {
                _groupButtonLabel.text = _groupByKey ? "Grouped by key" : "Grouped by mod";
                _groupButtonLabel.fontSize = 15;
                _groupButtonLabel.color = new Color(1f, 1f, 1f, 0.8f);
            }
            // Filters carry less weight than the window buttons: dim while off, lit while on, so
            // the eye reads "what this list is showing" rather than a row of equal commands.
            SetFilter(_conflictsButtonLabel, "Conflicts", _conflictsOnly);
            SetFilter(_yoursButtonLabel, $"Yours ({PersonalKeys.Count})", _yoursOnly);
            SetFilter(_mutedButtonLabel, $"Muted ({all.Count(MuteStore.IsMuted)})", _mutedOnly);


            if (_summary != null)
            {
                var live = all.Where(c => !MuteStore.IsMuted(c)).ToList();
                // Counting binds the user cannot see makes the total look wrong: internal ones
                // only belong in it while they are actually being shown.
                var total = BindRegistry.All.Count(b => !b.Internal || _internalShown);

                // The restore line only appears when there is one, and it is the only part of the
                // footer that reports something that happened rather than something that is.
                var restored = PersonalKeys.RestoredCount > 0
                    ? $"     {PersonalKeys.RestoredCount} put back - see ?"
                    : "";

                _summary.text = $"{shownCount} of {total} binds shown     " +
                                $"{live.Count(c => c.Severity == Severity.Hard)} hard     " +
                                $"{live.Count(c => c.Severity == Severity.Soft)} soft     " +
                                $"{live.Count(c => c.Severity == Severity.Note)} notes     " +
                                $"{PersonalKeys.Count} kept for you" + restored;
            }
        }

        /// <summary>
        /// Everything that would change how a row looks. An equal signature means what is on
        /// screen is already correct and the rows can be left alone.
        /// </summary>
        private static string Signature(List<BindEntry> shown)
        {
            var builder = new System.Text.StringBuilder(shown.Count * 48);
            builder.Append(_groupByKey ? "k|" : "m|").Append('|');

            foreach (var bind in shown)
            {
                builder.Append(bind.Id).Append('=').Append(bind.Combo);
                if (PersonalKeys.IsPersonal(bind.Id)) builder.Append('*').Append(ProfileKeyNote(bind, true));

                var worst = ConflictIndex.Worst(bind.Id);
                if (worst != null) builder.Append('#').Append(worst).Append(ConflictIndex.Live(bind.Id).Count);
                builder.Append(';');
            }

            return builder.ToString();
        }

        /// <summary>
        /// Searching for a key is the common case and a bare substring is useless for it: "e"
        /// appears in almost every mod name. A query that names a key searches keys only, and
        /// quoting forces that reading for anything ambiguous.
        /// </summary>
        private static bool Matches(BindEntry bind, string query)
        {
            if (query.Length == 0) return true;

            if (query.Length >= 3 && query[0] == '"' && query[query.Length - 1] == '"')
                return UsesKey(bind, query.Substring(1, query.Length - 2));

            if (IsKeyName(query)) return UsesKey(bind, query);

            // Against what the row shows, so typing "ö" finds the rows reading Ö.
            return (bind.OwnerName + " " + bind.Label + " " + KeyLabels.Of(bind.Combo)).ToLowerInvariant().Contains(query);
        }

        /// <summary>
        /// Whether the query names a key. With labels on, any single visible character does:
        /// that is what a key looks like on the keycap, and letters were already treated so.
        /// </summary>
        private static bool IsKeyName(string query)
        {
            if (Plugin.KeyboardLabels && query.Length == 1 && !char.IsWhiteSpace(query[0])) return true;

            return Enum.TryParse<KeyCode>(query, true, out var key)
                   && Enum.IsDefined(typeof(KeyCode), key)
                   // Rejects "8", which parses as the numeric value of Backspace rather than a key name.
                   && string.Equals(key.ToString(), query, StringComparison.OrdinalIgnoreCase);
        }

        private static bool UsesKey(BindEntry bind, string keyName)
        {
            // A key with no KeyCode is known by its path, which is also what it is called with
            // labels off, and by its label with them on.
            if (bind.Combo.Main == KeyCode.None && KeyLabels.Answers(bind.Combo.RawPath, keyName)) return true;

            // For a key with a KeyCode, labels off is today's search, unchanged.
            if (!Plugin.KeyboardLabels)
            {
                if (!Enum.TryParse<KeyCode>(keyName, true, out var key)) return false;
                return bind.Combo.Main == key || bind.Combo.Modifiers.Contains(key);
            }

            return KeyLabels.Answers(bind.Combo.Main, keyName) ||
                   bind.Combo.Modifiers.Any(m => KeyLabels.Answers(m, keyName));
        }

        /// <summary>
        /// Brings a bind into view in the list. Whatever is narrowing the list is dropped only
        /// when it is what hides the bind, so a search someone is still using survives a jump to
        /// a bind that search already shows.
        /// </summary>
        private static void Reveal(BindEntry bind)
        {
            if (bind == null) return;

            if (!_rowBackgrounds.ContainsKey(bind.Id))
            {
                if (_search != null) _search.text = "";
                _yoursOnly = false;
                _mutedOnly = false;
                Populate();
            }

            ScrollTo(bind.Id);
        }

        /// <summary>Takes what the window is about to destroy, so the next one starts where this left off.</summary>
        private static void RememberPlace()
        {
            if (_search != null) _searchText = _search.text;

            var scroll = ListScroll();
            if (scroll != null) _scrollAt = scroll.verticalNormalizedPosition;
        }

        /// <summary>Puts the search and the scroll back, once there are rows to scroll through.</summary>
        private static void RestorePlace()
        {
            if (_search != null && _searchText.Length > 0)
            {
                _search.text = _searchText;
                Populate();
            }

            // Writing the text asks the field's own listener for a redraw a moment later, and one
            // the last window asked for can still be waiting, since the timer outlives the window
            // it was set in. Either would rebuild the rows after the scroll below was set and
            // leave the list back at the top. The rows are drawn above instead, in time.
            _repopulateAt = 0f;

            var scroll = ListScroll();
            if (scroll == null) return;

            // The rows were built this frame, so the layout has to run before the view can move.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            scroll.verticalNormalizedPosition = Mathf.Clamp01(_scrollAt);
        }

        private static ScrollRect ListScroll() =>
            _content == null ? null : _content.GetComponentInParent<ScrollRect>();

        /// <summary>Puts a row in the middle of the list, or as close to it as the ends allow.</summary>
        private static void ScrollTo(string bindId)
        {
            if (_content == null || !_rowBackgrounds.TryGetValue(bindId, out var row) || row == null) return;

            var scroll = _content.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null) return;

            // The row was created this frame, so the layout has to run before it has a position.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

            var travel = _content.rect.height - scroll.viewport.rect.height;
            if (travel <= 0f) return;

            // Rows hang below the content's top edge, so their y is negative.
            var down = -((RectTransform)row.transform).anchoredPosition.y - scroll.viewport.rect.height / 2f;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - down / travel);
        }

        /// <summary>
        /// Moves the highlight by recolouring the two rows involved, without rebuilding the list.
        /// </summary>
        private static void Select(BindEntry bind)
        {
            KeyCapture.Cancel();

            if (_selected != null && _rowBackgrounds.TryGetValue(_selected.Id, out var previous) && previous != null)
                previous.color = RowColour(false);

            // Clicking a bind is a request to look at that bind, whichever page you were on, and
            // it abandons a key you pressed for a different one without agreeing to it.
            _page = DetailPage.Bind;
            _pendingFor = null;
            _note = null;
            _selected = bind;

            if (bind != null && _rowBackgrounds.TryGetValue(bind.Id, out var current) && current != null)
                current.color = RowColour(true);
        }

        private static Color RowColour(bool selected) =>
            selected ? new Color(1f, 0.7f, 0.2f, 0.28f) : new Color(0f, 0f, 0f, 0.01f);

        private static void AddRow(BindEntry bind)
        {
            var row = new GameObject("row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image), typeof(Button));
            row.transform.SetParent(_content, false);
            Fix(row, RowWidth, RowHeight);

            var background = row.GetComponent<Image>();
            _rowBackgrounds[bind.Id] = background;
            background.color = RowColour(_selected != null && _selected.Id == bind.Id);

            row.GetComponent<Button>().onClick.AddListener(() =>
            {
                Select(bind);
                ShowDetail();
            });

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            // Left over width would otherwise be shared out between the cells, so rows with
            // fewer cells would line up differently from rows with more.
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.padding = new RectOffset(14, 0, 0, 0);

            var conflicts = ConflictIndex.Live(bind.Id);
            var worst = ConflictIndex.Worst(bind.Id);

            // One mark, not two: the severity and how many there are belong together.
            var muted = ConflictIndex.MutedCount(bind.Id);
            // Count only the conflicts at the worst severity: "hard (4)" while three of them are
            // notes reads as four hard clashes and hides that something was ruled out.
            var atWorst = worst == null ? 0 : conflicts.Count(c => c.Severity == worst);
            var mark = worst != null
                ? $"{worst.ToString().ToLowerInvariant()} ({atWorst})"
                : muted > 0 ? $"muted ({muted})" : "";
            var markColor = worst != null ? ColorFor(worst) : new Color(1f, 1f, 1f, 0.35f);

            // With key headings the mod name is the thing you need on the row instead.
            var name = _groupByKey ? $"{bind.OwnerName} - {bind.Label}" : bind.Label;

            // A dot marks a key you set that survives profile syncs.
            var yours = PersonalKeys.IsPersonal(bind.Id);
            var key = KeyLabels.Of(bind.Combo) + (yours ? "  *" : "");

            var columns = Columns();
            Cell(row.transform, mark, columns.Mark, markColor, 13);
            Cell(row.transform, name, columns.Label, Color.white, 15);
            Cell(row.transform, key, columns.Key, bind.Combo.IsBound ? Color.white : new Color(1f, 1f, 1f, 0.35f), 15);

            if (columns.Profile > 0f)
                Cell(row.transform, ProfileKeyNote(bind, yours), columns.Profile, new Color(1f, 1f, 1f, 0.4f), 12);
        }

        private static Color ColorFor(Severity? severity)
        {
            if (severity == Severity.Hard) return new Color(1f, 0.38f, 0.32f);
            if (severity == Severity.Soft) return new Color(1f, 0.8f, 0.32f);
            if (severity == Severity.Note) return new Color(0.65f, 0.65f, 0.65f);
            return Color.white;
        }
    }
}
