using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Bindrune.Context;
using Bindrune.Discovery;
using Bindrune.Personal;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;

namespace Bindrune.SelfTest
{
    internal sealed partial class SelfTest
    {
        /// <summary>The item the self-test's key hint is for, which no game or mod item is named, so it adds to no real item's hints.</summary>
        private const string HintedItem = "BindruneSelfTestItem";

        /// <summary>
        /// Jotunn's buttons as Bindrune reads them: a button backed by a setting is listed and can
        /// be changed, one on an axis or with a key in the mod's code is read only and says why,
        /// and a key hint for an item says the button is live while that item is in hand. The
        /// buttons are the self-test's own, put in Jotunn's list of buttons, which is all Bindrune
        /// reads, and taken out again; they are never registered with the game's controls, which
        /// would keep them in its saved bindings.
        /// </summary>
        private IEnumerator JotunnBinds()
        {
            if (!(AccessTools.Field(typeof(InputManager), "Buttons")?.GetValue(null) is IDictionary buttons) || KeyHintManager.Instance == null)
            {
                Skip("Jotunn's buttons", "Jotunn's list of buttons cannot be read");
                yield break;
            }

            var others = BindRegistry.All.Count(b => b.Source == BindSource.Jotunn);
            Note("Jotunn buttons of the mods in this profile", Count(others) + ", of them " +
                Count(BindRegistry.All.Count(b => b.Source == BindSource.Jotunn && b.Editable)) + " that can be changed here");

            var key = _config.Bind(TestBinds.Section, "JotunnKey", KeyCode.F14, "A setting of the Bindrune self-test, there only while it runs.");
            key.Value = KeyCode.F14;
            var dig = Button("SelfTestDig", b => { b.Config = key; b.Hint = "Dig"; });
            var axis = Button("SelfTestWheel", b => b.Axis = "Mouse ScrollWheel");
            var fixedKey = Button("SelfTestFixed", b => b.Key = KeyCode.F13);
            var hint = new KeyHintConfig { Item = HintedItem, ButtonConfigs = new[] { dig } };
            var added = new[] { dig, axis, fixedKey };

            try
            {
                foreach (var button in added) buttons[button.Name] = button;
                Check(KeyHintManager.Instance.AddKeyHint(hint), "a key hint for an item is added as a mod adds one");
                yield return Reopen();

                var digBind = ById(BindIds.Jotunn(dig.Name));
                Check(digBind != null && digBind.Source == BindSource.Jotunn && digBind.Editable && digBind.Label == "Dig",
                    "a Jotunn button backed by a setting is listed by its hint and can be changed", digBind?.ToString());
                Check(digBind != null && digBind.OwnerName == "Bindrune self-test", "it is listed under the mod that added it", digBind?.OwnerName);
                Check(!BindRegistry.All.Any(b => b.Source == BindSource.ModTyped && ReferenceEquals(b.Handle, key)),
                    "the setting behind it is not listed a second time as a setting of its own");

                var wheel = ById(BindIds.Jotunn(axis.Name));
                Check(wheel != null && !wheel.Editable && (wheel.ReadOnlyReason ?? "").Contains("axis"),
                    "a button on an axis is read only and says why", wheel?.ReadOnlyReason);
                var hard = ById(BindIds.Jotunn(fixedKey.Name));
                Check(hard != null && !hard.Editable && (hard.ReadOnlyReason ?? "").Contains("hardcoded"),
                    "a button whose key is in the mod's code is read only and says why", hard?.ReadOnlyReason);

                if (digBind != null)
                {
                    var situations = KnownSituations.For(digBind, out var source);
                    Check(source == SituationSource.KeyHints && situations.Contains(EquippedItems.Prefix + "item:" + HintedItem),
                        "the mod's key hint says the button is live while its item is in hand", source + ": " + string.Join(", ", situations.ToArray()));

                    var moved = Combo(KeyCode.F15);
                    Check(BindWriter.Apply(digBind, moved, SaveTarget.Unrecorded) == null && key.Value == KeyCode.F15,
                        "a key set here goes into the setting behind the button", key.Value.ToString());
                }
            }
            finally
            {
                KeyHintManager.Instance.RemoveKeyHint(hint);
                foreach (var button in added) buttons.Remove(button.Name);
                _config.Remove(key.Definition);
                _config.Save();
            }

            yield return Reopen();
            Check(ById(BindIds.Jotunn(dig.Name)) == null, "taken out of Jotunn's list, the button is gone from the panel");
        }

        /// <summary>A button of the self-test's, named as Jotunn names a button it adds: the button's name and the mod's.</summary>
        private static ButtonConfig Button(string name, System.Action<ButtonConfig> fill)
        {
            var button = new ButtonConfig();
            fill(button);
            button.Name = name + "!" + SelfTestPlugin.Guid;
            return button;
        }
    }
}
