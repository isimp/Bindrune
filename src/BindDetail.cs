using System.Collections.Generic;
using System.Linq;
using Bindrune.Conflicts;

namespace Bindrune
{
    /// <summary>
    /// What the right-hand column shows for a selected bind, decided apart from drawing it: its
    /// key, the controls that change it or why it cannot be changed, whether Default is offered,
    /// and every clash it is in, worst first. The panel draws what this holds, in its order.
    /// </summary>
    internal sealed class BindDetail
    {
        /// <summary>What stands under the key.</summary>
        public enum Controls
        {
            /// <summary>Rebind, Clear and, where it differs, Default.</summary>
            Edit,

            /// <summary>A key pressed for this bind, waiting to be applied or dropped.</summary>
            Pending,

            /// <summary>Why the bind cannot be changed here.</summary>
            Locked,
        }

        /// <summary>One clash, as the conflict list draws it.</summary>
        public sealed class ConflictLine
        {
            public Conflict Conflict;

            /// <summary>The bind on the clash's other side: half of a clash is always the bind you are not looking at.</summary>
            public BindEntry Other;

            public bool Muted;

            /// <summary>"muted", or the tier in lower case, with "- confirmed" where both sides confirm it.</summary>
            public string Tag;
        }

        public string Owner;
        public string Label;

        /// <summary>The key as your keyboard labels it, or "not bound".</summary>
        public string KeyLine;

        /// <summary>"stored as" and Unity's name, where the help page's switch asks for it and the name differs from the label; null otherwise.</summary>
        public string StoredAs;

        public Controls Mode;

        /// <summary>Why the bind cannot be changed, as shown in place of its controls.</summary>
        public string Locked;

        /// <summary>Whether this bind's Rebind button started the wait for a key.</summary>
        public bool Capturing;

        public string RebindLabel => Capturing ? "press a key..." : "Rebind";

        public string ClearLabel => Capturing ? "Cancel" : "Clear";

        /// <summary>Whether the words on capturing a key are shown, which is while it waits for one.</summary>
        public bool TellsHowToCapture => Capturing;

        /// <summary>Whether Default is offered: the bind's own default key is a key, differs from the one set, and no key is awaited.</summary>
        public bool OffersDefault;

        /// <summary>The clashes worst first, empty for "No conflicts", or null for a bind nothing compares, which the section above has already explained.</summary>
        public List<ConflictLine> Conflicts;

        /// <param name="bind">The selected bind.</param>
        /// <param name="pending">Whether a key pressed for this bind waits to be applied.</param>
        /// <param name="capturing">Whether the Rebind button waits for a key.</param>
        /// <param name="storedNamesShown">Whether the help page's switch shows the names keys are stored by.</param>
        public static BindDetail Of(BindEntry bind, bool pending, bool capturing, bool storedNamesShown)
        {
            var label = bind.Combo.IsBound ? KeyLabels.Of(bind.Combo) : "not bound";
            var stored = bind.Combo.ToString();

            var view = new BindDetail
            {
                Owner = bind.OwnerName,
                Label = bind.Label,
                KeyLine = label,
                StoredAs = storedNamesShown && bind.Combo.IsBound && label != stored ? "stored as " + stored : null,
                Mode = !bind.Editable ? Controls.Locked : pending ? Controls.Pending : Controls.Edit,
                Locked = "Locked: " + bind.ReadOnlyReason,
                Capturing = capturing,
            };

            if (view.Mode == Controls.Edit && !capturing)
            {
                var fallback = BindWriter.DefaultOf(bind);
                view.OffersDefault = fallback.IsBound && !fallback.Equals(bind.Combo);
            }

            if (bind.Compared)
            {
                view.Conflicts = ConflictIndex.For(bind.Id).OrderBy(c => c.Severity).Select(c =>
                {
                    var muted = MuteStore.IsMuted(c);
                    return new ConflictLine
                    {
                        Conflict = c,
                        Other = c.A.Id == bind.Id ? c.B : c.A,
                        Muted = muted,
                        Tag = muted ? "muted" : c.Severity.ToString().ToLowerInvariant() + (c.Confirmed ? " - confirmed" : ""),
                    };
                }).ToList();
            }

            return view;
        }
    }
}
