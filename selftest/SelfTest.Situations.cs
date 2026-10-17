using System;
using System.Collections;
using Bindrune.Hints;
using UnityEngine;

namespace Bindrune.SelfTest
{
    internal sealed partial class SelfTest
    {
        /// <summary>Longer than the hints wait between two looks at where you are.</summary>
        private const float SituationSettles = 0.4f;

        /// <summary>
        /// The hints follow where you are: with the map open they show the binds live on the map,
        /// with the inventory open those live there, and a bind that says nothing of where shows
        /// everywhere. The map and the inventory are opened as the game opens them, and left
        /// as they were.
        /// </summary>
        private IEnumerator HintsBySituation()
        {
            var map = TestBinds.Id("OnMap");
            var inventory = TestBinds.Id("InInventory");
            var anywhere = TestBinds.Id("HintMe");
            if (Bind("OnMap") == null || Bind("InInventory") == null || Bind("HintMe") == null || Minimap.instance == null || InventoryGui.instance == null)
            {
                Skip("hints by where you are", "the test settings, the map or the inventory are not there");
                yield break;
            }

            var hintsWereOn = Plugin.HintsVisible;
            var mapMode = Minimap.instance.m_mode;
            var inventoryWasOpen = InventoryGui.IsVisible();
            var pinned = new[] { map, inventory, anywhere };
            try
            {
                foreach (var id in pinned)
                    if (!HintChoice.Shows(id)) HintChoice.Toggle(id);
                if (!Plugin.HintsVisible) HintOverlay.Toggle();

                if (inventoryWasOpen) InventoryGui.instance.Hide();
                if (Minimap.IsOpen()) Minimap.instance.SetMapMode(Minimap.MapMode.Small);
                yield return Wait(SituationSettles);
                HintOverlay.Tick(force: true);
                var world = HintOverlay.Shown ?? "";
                Check(Has(world, "HintMe") && !Has(world, "OnMap") && !Has(world, "InInventory"),
                    "out in the world, only the bind that says nothing of where shows", Lines(world));

                Minimap.instance.SetMapMode(Minimap.MapMode.Large);
                yield return Wait(SituationSettles);
                HintOverlay.Tick(force: true);
                var onMap = HintOverlay.Shown ?? "";
                Check(Minimap.IsOpen() && Has(onMap, "OnMap") && !Has(onMap, "InInventory") && Has(onMap, "HintMe"),
                    "with the map open, the bind live on the map shows too", Lines(onMap));
                Minimap.instance.SetMapMode(Minimap.MapMode.Small);

                InventoryGui.instance.Show(null);
                yield return Wait(SituationSettles);
                HintOverlay.Tick(force: true);
                var inInventory = HintOverlay.Shown ?? "";
                Check(InventoryGui.IsVisible() && Has(inInventory, "InInventory") && !Has(inInventory, "OnMap") && Has(inInventory, "HintMe"),
                    "with the inventory open, the bind live there shows instead", Lines(inInventory));
                InventoryGui.instance.Hide();
            }
            finally
            {
                if (Minimap.instance != null && Minimap.instance.m_mode != mapMode) Minimap.instance.SetMapMode(mapMode);
                if (InventoryGui.instance != null && InventoryGui.IsVisible() != inventoryWasOpen)
                {
                    if (inventoryWasOpen) InventoryGui.instance.Show(null);
                    else InventoryGui.instance.Hide();
                }
                foreach (var id in pinned)
                    if (HintChoice.Shows(id)) HintChoice.Toggle(id);
                if (Plugin.HintsVisible != hintsWereOn) HintOverlay.Toggle();
            }
        }

        private static bool Has(string shown, string label) => shown.IndexOf(label, StringComparison.Ordinal) >= 0;

        private static string Lines(string shown) => shown.Replace('\n', '|');
    }
}
