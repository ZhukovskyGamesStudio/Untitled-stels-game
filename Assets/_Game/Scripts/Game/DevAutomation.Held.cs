using System.Collections;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// Everything that goes into the hand, as its holder sees it (<c>SEED=7 LEVEL=3 Tools/dev.sh run held</c>): one frame
        /// per holdable thing in the lit lobby, the view a little down. The fuel finders get loot lying ahead and to the
        /// left so the plain one blinks and the upgraded one's ring points at it (a frame lit, a frame turned away).
        /// </summary>
        IEnumerator HeldScenario()
        {
            if (!Me.IsServer) yield break;
            var gm = GameManager.Instance;
            DevFlags.Peaceful = true;
            foreach (var d in Detector.All.ToArray()) Session.Despawn(d.gameObject);
            Me.SetHatch(false);
            yield return new WaitForSeconds(4f); // (the entry banner goes)
            var layout = ActiveLevel.Layout;
            var room = layout.Lobby.Center;
            var stand = room + new Vector3(0f, 0.05f, -1.5f);
            var inv = Me.GetComponent<PlayerInventory>();
            // loot ahead and to the left: what the finders feel
            gm.SpawnItemServer(ItemCatalog.Book, 0, room + new Vector3(-2.5f, 0.3f, 2.5f), 0f);

            for (int shape = 0; shape < ItemCatalog.Shapes.Length; shape++)
            {
                if (!ItemCatalog.Holdable(shape)) continue;
                inv.ClearServer();
                inv.GiveToHandServer((byte)shape, ItemCatalog.FreshColor(shape, 2));
                LookAtFrom(new Pose(stand, Quaternion.LookRotation(Vector3.forward)), stand + new Vector3(0f, 1.2f, 4f));
                yield return new WaitForSeconds(0.6f);
                string id = ItemCatalog.Shapes[shape].Id;
                if (shape is ItemCatalog.Finder or ItemCatalog.ProFinder)
                {
                    // catch the flash: several frames over one blink
                    for (int i = 0; i < 3; i++) { yield return new WaitForSeconds(0.12f); yield return Shot($"{shape:00}_{id}_{i}"); }
                    var nearest = WorldItem.All.Where(w => w != null && ItemCatalog.IsLoot(w.Shape)).OrderBy(w => (w.transform.position - stand).sqrMagnitude).FirstOrDefault();
                    if (nearest != null) Log($"{id}: nearest loot {ItemCatalog.Shapes[nearest.Shape].Id} at {Flat(nearest.transform.position - stand)} from the player (+x east, +z ahead)");
                    LookAtFrom(new Pose(stand, Quaternion.LookRotation(Vector3.right)), stand + new Vector3(4f, 1.2f, 0f));
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot($"{shape:00}_{id}_turned");
                }
                else yield return Shot($"{shape:00}_{id}");
                Log($"held {id}: hand {inv.HandIndex()} model {(Me.HeldThing != null ? Me.HeldThing.transform.childCount : -1)}");
            }
            inv.ClearServer();
        }
    }
}
