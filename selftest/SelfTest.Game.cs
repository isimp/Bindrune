using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Bindrune.Discovery;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine.InputSystem;

namespace Bindrune.SelfTest
{
    internal sealed partial class SelfTest
    {
        /// <summary>The game methods Bindrune patches, with the kind of patch each carries.</summary>
        private static readonly (Type Type, string Method, bool Prefix, string Why)[] Patched =
        {
            (typeof(FejdStartup), "UpdateKeyboard", true, "holds the start menu's keys back while you type in the panel"),
            (typeof(ZInput), "Load", false, "puts the hotbar keys Bindrune keeps back after the game loads its controls"),
            (typeof(ZInput), "ResetToDefault", false, "puts them back after the game resets its controls"),
            (typeof(ZInput), "StartBindKey", true, "hands an Alt hotbar key back before the game's own screen rebinds it"),
            (typeof(HotkeyBar), "UpdateIcons", false, "labels the hotbar while a slot's key is moved"),
        };

        /// <summary>
        /// Every member of the game's and Jotunn's that Bindrune reaches by its name, out of the
        /// compiler's sight, so an update that renames one is told here rather than found as a
        /// feature that quietly stopped. Named "Type.member", as the standards tests look for.
        /// </summary>
        private static IEnumerable<(string Name, Func<MemberInfo> Find)> Members()
        {
            yield return ("ZInput.ButtonDef.GetActionPath", () => AccessTools.Method(typeof(ZInput.ButtonDef), "GetActionPath"));
            yield return ("ZInput.ButtonDef.Rebindable", () => AccessTools.PropertyGetter(typeof(ZInput.ButtonDef), "Rebindable"));
            yield return ("ZInput.Save", () => AccessTools.Method(typeof(ZInput), "Save"));
            yield return ("ZInput.Load", () => AccessTools.Method(typeof(ZInput), "Load"));
            yield return ("ZInput.ResetToDefault", () => AccessTools.Method(typeof(ZInput), "ResetToDefault"));
            yield return ("ZInput.StartBindKey", () => AccessTools.Method(typeof(ZInput), "StartBindKey"));
            yield return ("ZInput.KeyToPath", () => AccessTools.Method(typeof(ZInput), "KeyToPath", new[] { typeof(Key) }));
            yield return ("ZInput.m_buttons", () => AccessTools.Field(typeof(ZInput), "m_buttons"));
            yield return ("ZInput.m_presentationButtons", () => AccessTools.Field(typeof(ZInput), "m_presentationButtons"));
            yield return ("ZInput.m_presentationButtons.Name", () => AccessTools.Property(Presentation(0), "Name"));
            yield return ("ZInput.m_presentationButtons.Layout", () => AccessTools.Property(Presentation(0), "Layout"));
            yield return ("ZInput.m_presentationButtons.Path", () => AccessTools.Property(Presentation(1), "Path"));
            yield return ("ZInput.s_keyCodeToKeyMap", () => AccessTools.Field(typeof(ZInput), "s_keyCodeToKeyMap"));
            yield return ("ZInput.s_keyCodeToMouseButtonMap", () => AccessTools.Field(typeof(ZInput), "s_keyCodeToMouseButtonMap"));
            yield return ("Localization.m_instance", () => AccessTools.Field(typeof(Localization), "m_instance"));
            yield return ("Localization.m_cache", () => AccessTools.Field(typeof(Localization), "m_cache"));
            yield return ("Localization.m_cache.EvictAll", () => AccessTools.Method(AccessTools.Field(typeof(Localization), "m_cache")?.FieldType, "EvictAll"));
            yield return ("Humanoid.m_rightItem", () => AccessTools.Field(typeof(Humanoid), "m_rightItem"));
            yield return ("Humanoid.m_leftItem", () => AccessTools.Field(typeof(Humanoid), "m_leftItem"));
            yield return ("FejdStartup.UpdateKeyboard", () => AccessTools.Method(typeof(FejdStartup), "UpdateKeyboard"));
            yield return ("HotkeyBar.UpdateIcons", () => AccessTools.Method(typeof(HotkeyBar), "UpdateIcons"));
            yield return ("HotkeyBar.m_elements", () => AccessTools.Field(typeof(HotkeyBar), "m_elements"));
            yield return ("HotkeyBar.m_elements.m_go", () => AccessTools.Field(ElementType(), "m_go"));
            yield return ("ConfigFile.OrphanedEntries", () => AccessTools.Property(typeof(ConfigFile), "OrphanedEntries"));
            yield return ("KeyHintManager.KeyHints", () => AccessTools.Field(typeof(KeyHintManager), "KeyHints"));
            yield return ("InputManager.Buttons", () => AccessTools.Field(typeof(InputManager), "Buttons"));
        }

        /// <summary>The key type (0) or the value type (1) of the game's gamepad button map.</summary>
        private static Type Presentation(int which)
        {
            var map = AccessTools.Field(typeof(ZInput), "m_presentationButtons")?.FieldType;
            return map != null && map.IsGenericType && map.GetGenericArguments().Length == 2 ? map.GetGenericArguments()[which] : null;
        }

        /// <summary>The type of the hotbar's element list's items.</summary>
        private static Type ElementType()
        {
            var list = AccessTools.Field(typeof(HotkeyBar), "m_elements")?.FieldType;
            return list != null && list.IsGenericType ? list.GetGenericArguments()[0] : null;
        }

        private IEnumerator Game()
        {
            Check(Chainloader.PluginInfos.ContainsKey(Plugin.Guid), "Bindrune is loaded",
                Chainloader.PluginInfos.TryGetValue(Plugin.Guid, out var info) ? info.Metadata.Version.ToString() : null);

            foreach (var (type, method, prefix, why) in Patched)
            {
                var target = AccessTools.Method(type, method);
                var patches = target == null ? null : Harmony.GetPatchInfo(target);
                var ours = patches != null && (prefix ? patches.Prefixes : patches.Postfixes).Any(p => p.owner == Plugin.Guid);
                Check(ours, type.Name + "." + method + " carries Bindrune's " + (prefix ? "prefix" : "postfix") + ", which " + why,
                    target == null ? "the game no longer has it" : null);
            }

            foreach (var (name, find) in Members())
            {
                MemberInfo member = null;
                string problem = null;
                try
                {
                    member = find();
                }
                catch (Exception ex)
                {
                    // A member that cannot even be looked for is as missing as one not found.
                    problem = ex.GetType().Name + ": " + ex.Message;
                }
                Check(member != null, "the game still has " + name, problem);
            }

            Check(ZInput.instance != null, "the game's controls are loaded");
            Check(GUIManager.Instance != null && GUIManager.CustomGUIFront != null, "Jotunn's interface, which the panel is built with, is ready");
            yield break;
        }

        private IEnumerator GameCode()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var jump = ContextIndex.For("Jump");
            Note("reading where the game reads its controls", Ms(clock.Elapsed) + " ms, cached after the first time");

            Check(jump != null && jump.Count > 0, "the game's code says where Jump is read", jump == null ? null : string.Join(", ", jump.ToArray()));

            var tabRight = ContextIndex.For("TabRight");
            Check(tabRight != null && tabRight.Count == 1 && tabRight.Contains("building"), "TabRight is read only while building, as the notes say",
                tabRight == null ? "not found" : string.Join(", ", tabRight.ToArray()));

            Check(ContextIndex.AreDisjoint("Use", "TabRight", out var why), "Use and TabRight, both on E, are never read in the same place", why);
            yield break;
        }
    }
}
