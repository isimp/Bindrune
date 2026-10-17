using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Bindrune.Personal;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Bindrune.SelfTest
{
    internal sealed partial class SelfTest
    {
        private static KeyCombo Combo(KeyCode key, params KeyCode[] modifiers) => new KeyCombo(key, modifiers);

        /// <summary>What a test setting holds now, read from the setting itself.</summary>
        private static KeyCombo Held(ConfigEntryBase entry) =>
            entry.BoxedValue is KeyboardShortcut shortcut ? new KeyCombo(shortcut.MainKey, shortcut.Modifiers)
            : entry.BoxedValue is KeyCode key ? new KeyCombo(key, null)
            : KeyCombo.None;

        private IEnumerator YoursAndTheProfiles()
        {
            var id = TestBinds.Id("Yours");
            var bind = Bind("Yours");
            if (bind == null || !(bind.Handle is ConfigEntryBase entry))
            {
                Skip("your key and the profile's", "the test setting was not found");
                yield break;
            }

            var profiles = Held(entry);
            var mine = Combo(KeyCode.F14, KeyCode.RightShift);
            var synced = Combo(KeyCode.F15, KeyCode.RightControl);
            try
            {
                Check(BindWriter.Apply(bind, mine, SaveTarget.Personal) == null, "Just for you sets your own key", mine.ToString());
                Check(Held(entry).Equals(mine), "the setting holds your key", Held(entry).ToString());
                var file = File.ReadAllText(entry.ConfigFile.ConfigFilePath);
                Check(file.Contains("Yours = " + new KeyboardShortcut(KeyCode.F14, KeyCode.RightShift).Serialize()), "the setting's cfg file is saved with your key");

                var recorded = PersonalKeys.Get(id);
                Check(PersonalKeys.IsPersonal(id) && recorded != null && recorded.Personal.Equals(mine) && recorded.Profile.Equals(profiles),
                    "your key is kept beside the profile's key it replaced", recorded == null ? "nothing kept" : recorded.Personal + " / " + recorded.Profile);

                PersonalKeys.UseProfile(Bind("Yours"));
                Check(Held(entry).Equals(profiles) && !PersonalKeys.IsPersonal(id) && PersonalKeys.Get(id)?.Personal.Equals(mine) == true,
                    "Whole profile puts the profile's key back and keeps yours aside", Held(entry).ToString());

                PersonalKeys.UsePersonal(Bind("Yours"));
                Check(Held(entry).Equals(mine) && PersonalKeys.IsPersonal(id), "Just for you puts your key back", Held(entry).ToString());

                // A sync hands the setting another key, as the game reads it at the next launch.
                entry.BoxedValue = new KeyboardShortcut(KeyCode.F15, KeyCode.RightControl);
                entry.ConfigFile.Save();
                Discovery.BindRegistry.Refresh();
                Check(Held(entry).Equals(mine), "after a sync changed the setting, your key is put back", Held(entry).ToString());
                Check(PersonalKeys.Get(id)?.Profile.Equals(synced) == true, "the key the sync brought is now the profile's", PersonalKeys.Get(id)?.Profile.ToString());

                Check(BindWriter.Reset(Bind("Yours")) == null && Held(entry).Equals(DefaultOf(entry)) && !PersonalKeys.IsPersonal(id),
                    "Default gives the mod's own key back, on the profile's side", Held(entry).ToString());
                Check(PersonalKeys.Get(id)?.Personal.Equals(mine) == true, "your key is kept aside, for Just for you to bring back",
                    PersonalKeys.Get(id)?.Personal.ToString());
            }
            finally
            {
                PersonalKeys.Forget(id);
                entry.BoxedValue = entry.DefaultValue;
                entry.ConfigFile.Save();
            }

            yield return Reopen();
        }

        private static KeyCombo DefaultOf(ConfigEntryBase entry) =>
            entry.DefaultValue is KeyboardShortcut shortcut ? new KeyCombo(shortcut.MainKey, shortcut.Modifiers) : KeyCombo.None;

        private IEnumerator Refusals()
        {
            var text = Bind("TextKey");
            var yours = Bind("Yours");
            var jump = ById(BindIds.Vanilla("Jump"));
            var digit = ById(BindIds.Vanilla("Hotbar1"));
            if (text == null || yours == null || jump == null || digit == null)
            {
                Skip("what a bind takes", "a bind it asks about was not found");
                yield break;
            }

            Check(BindWriter.Refusal(text, Combo(KeyCode.F15)) != null, "a hotkey kept as text cannot be changed here", BindWriter.Refusal(text, Combo(KeyCode.F15)));
            Check(BindWriter.Refusal(yours, Combo(KeyCode.F13, KeyCode.LeftAlt, KeyCode.LeftShift)) == null, "a mod's shortcut takes any modifiers");
            Check(BindWriter.Refusal(yours, new KeyCombo(KeyCode.None, null, "<Keyboard>/OEM1")) == BindWriter.UnseenByMods,
                "a key only the game's newer input sees cannot go on a mod's bind");

            var jumpWithAlt = BindWriter.Refusal(jump, Combo(KeyCode.Space, KeyCode.LeftAlt));
            Check(jumpWithAlt != null, "the game's own controls take no modifier", jumpWithAlt);
            Check(BindWriter.Refusal(digit, Combo(KeyCode.Alpha1, KeyCode.LeftAlt)) == null, "a hotbar key takes one modifier");
            var two = BindWriter.Refusal(digit, Combo(KeyCode.Alpha1, KeyCode.LeftAlt, KeyCode.LeftControl));
            Check(two != null, "a hotbar key takes no second modifier", two);
        }

        private IEnumerator HotbarKeys()
        {
            var zinput = ZInput.instance;
            if (zinput == null)
            {
                Skip("moving a hotbar key", "the game's controls are not loaded");
                yield break;
            }

            // A slot whose key you have not moved, so nothing of yours is touched.
            var moved = new HashSet<string>(PersonalStore.Lines(PersonalStore.Fixed).Select(l => l.Split('\t')[0]));
            var slot = Enumerable.Range(1, 8).Reverse().FirstOrDefault(i => !moved.Contains("Hotbar" + Count(i)));
            if (slot == 0)
            {
                Skip("moving a hotbar key", "every slot's key is moved already");
                yield break;
            }

            var name = "Hotbar" + Count(slot);
            var digit = (KeyCode)((int)KeyCode.Alpha0 + slot);
            var withAlt = Combo(digit, KeyCode.LeftAlt);
            var bind = ById(BindIds.Vanilla(name));
            if (bind == null)
            {
                Skip("moving a hotbar key", name + " is not listed");
                yield break;
            }

            try
            {
                Check(BindWriter.Apply(bind, withAlt, SaveTarget.Personal) == null, "a hotbar key is moved to Alt + " + Count(slot));
                var composite = FixedKeys.Composite(FixedKeys.Live(name));
                Check(composite.HasValue && composite.Value.Equals(withAlt), "the game's control for the slot now answers to Alt + " + Count(slot), composite?.ToString());

                var label = Plain(FixedKeys.SlotLabel(zinput, slot - 1));
                Check(label == "Alt + " + Count(slot), "the slot is labelled with its new key", label);

                // The game loads its controls again as its Controls screen closes.
                AccessTools.Method(typeof(ZInput), "Load")?.Invoke(zinput, null);
                composite = FixedKeys.Composite(FixedKeys.Live(name));
                Check(composite.HasValue && composite.Value.Equals(withAlt), "the moved key survives the game loading its controls again", composite?.ToString());

                // The bar relabels as the game updates it, which it does every frame.
                string shown = null;
                for (var waited = 0f; waited < 1f; waited += Time.unscaledDeltaTime)
                {
                    shown = SlotOnScreen(slot - 1);
                    if (shown == label) break;
                    yield return null;
                }

                if (shown == null) Skip("the hotbar on screen", "the game's hotbar has no slot " + Count(slot) + " to show");
                else Check(shown == label, "the hotbar on screen shows the new key on the slot",
                    "slot " + Count(slot) + " reads \"" + shown + "\"; bar moved " + FixedKeys.BarChanged + ", " + Count(BarSlots()) + " slots");
            }
            finally
            {
                BindWriter.Reset(ById(BindIds.Vanilla(name)) ?? bind);
            }

            yield return null;
            Check(FixedKeys.Composite(FixedKeys.Live(name)) == null, "Default hands the slot back to its digit");
            var back = Plain(FixedKeys.SlotLabel(zinput, slot - 1));
            Check(back == Count(slot), "the slot is labelled with its digit again", back);
            Check(!PersonalStore.Lines(PersonalStore.Fixed).Any(l => l.StartsWith(name + "\t", System.StringComparison.Ordinal)),
                "nothing of the moved key is left in bindrune.keys");
        }

        /// <summary>Text with the non-breaking spaces the hotbar labels use read as plain ones.</summary>
        private static string Plain(string text) => text?.Replace((char)0xA0, ' ');

        /// <summary>The game's own hotbar, as Bindrune labels it, or null when there is none.</summary>
        private static HotkeyBar Bar()
        {
            var hud = Hud.instance.OrNull();
            var bar = hud == null ? null : hud.m_rootObject.transform.Find("HotKeyBar");
            return bar == null ? null : bar.GetComponent<HotkeyBar>().OrNull();
        }

        private static IList Elements() => Bar() == null ? null : AccessTools.Field(typeof(HotkeyBar), "m_elements")?.GetValue(Bar()) as IList;

        private static int BarSlots() => Elements()?.Count ?? 0;

        /// <summary>What a slot of the game's own hotbar reads on screen, or null when it has no such slot.</summary>
        private static string SlotOnScreen(int index)
        {
            var elements = Elements();
            if (elements == null || index >= elements.Count) return null;
            var go = AccessTools.Field(elements[index].GetType(), "m_go")?.GetValue(elements[index]) as GameObject;
            var binding = go == null ? null : go.transform.Find("binding");
            return binding == null ? null : Plain(binding.GetComponent<TMP_Text>().OrNull()?.text);
        }
    }
}
