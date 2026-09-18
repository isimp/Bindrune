namespace Bindrune
{
    /// <summary>
    /// How a bind is named in everything that outlives a session: your keys, your situations,
    /// muted pairs, the shared list. Built here so the four scanners cannot drift apart, and so
    /// there is one place to look when an id in a file has to be recognised by eye.
    ///
    /// An id is opaque. Nothing splits one back apart, and nothing should: a section or a key
    /// name may itself contain a colon. The parts are in it to make it unique and legible, not
    /// to be read back out.
    ///
    /// The section is part of a config id because a key name is only unique within its section.
    /// The cost is that a mod moving a setting to another section looks like a new bind, and
    /// your key for the old one becomes an orphan the panel offers to forget. That is the right
    /// trade: the alternative silently applies your key to whatever setting inherits the name.
    /// </summary>
    public static class BindIds
    {
        public static string Config(string guid, string section, string key) => $"cfg:{guid}:{section}:{key}";

        /// <summary>Jotunn's own button key, which already carries "&lt;button&gt;!&lt;mod guid&gt;".</summary>
        public static string Jotunn(string buttonKey) => "jotunn:" + buttonKey;

        public static string Vanilla(string buttonName) => "vanilla:" + buttonName;

        /// <summary>
        /// A gamepad button, which the game keys by name and layout because the same name means
        /// a different button under another layout.
        /// </summary>
        public static string Gamepad(string buttonName, string layout) => $"gamepad:{layout}:{buttonName}";
    }
}
