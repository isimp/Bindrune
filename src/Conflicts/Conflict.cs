using System;

namespace Bindrune.Conflicts
{
    public enum Severity
    {
        /// <summary>Same key, same modifiers. Both mods will act on the same press.</summary>
        Hard,
        /// <summary>Different modifiers, but one side ignores modifiers and fires anyway.</summary>
        Soft,
        /// <summary>Same key, different modifiers, both sides check modifiers exactly.</summary>
        Note
    }

    public class Conflict
    {
        public BindEntry A;
        public BindEntry B;
        public Severity Severity;
        public string Reason;
        /// <summary>The contested key, used for sorting and as the row label.</summary>
        public string KeyLabel;

        /// <summary>Set when both binds were marked as applying to the same situation.</summary>
        public bool Confirmed;

        /// <summary>Stable identity for a pair, so muting survives restarts and reordering.</summary>
        public string PairKey =>
            string.CompareOrdinal(A.Id, B.Id) <= 0 ? A.Id + "|" + B.Id : B.Id + "|" + A.Id;

        public override string ToString() => $"[{Severity}] {A.OwnerName}/{A.Label} vs {B.OwnerName}/{B.Label}";
    }
}
