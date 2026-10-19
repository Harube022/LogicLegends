using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenArchiveAuthoring
{
    /// <summary>Editor overlays only: empty slot/spawn anchors need no runtime components.</summary>
    [InitializeOnLoad]
    public static class OpenArchiveReservationGizmos
    {
        const string Preference = "LogicLegends.OpenArchive.ReservationGizmos";
        static OpenArchiveReservationGizmos() { SceneView.duringSceneGui += Draw; }

        [MenuItem("Tools/Logic Legends/Open Archive/Show Reserved Footprints And Entrances")]
        static void Toggle() { EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, true)); SceneView.RepaintAll(); }
        [MenuItem("Tools/Logic Legends/Open Archive/Show Reserved Footprints And Entrances", true)]
        static bool Validate() { Menu.SetChecked("Tools/Logic Legends/Open Archive/Show Reserved Footprints And Entrances", EditorPrefs.GetBool(Preference, true)); return true; }

        static void Draw(SceneView view)
        {
            if (!EditorPrefs.GetBool(Preference, true) || !view.drawGizmos) return;
            var previous = Handles.zTest; var color = Handles.color;
            Handles.zTest = CompareFunction.Always;
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++) foreach (var go in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    if (go.name != "OpenArchive") continue;
                    var slots = go.transform.Find("FutureStructureSlots");
                    if (slots != null) foreach (Transform slot in slots)
                    {
                        Handles.color = new Color(.35f, 1, .55f, .95f);
                        using (new Handles.DrawingScope(Matrix4x4.TRS(slot.position + Vector3.up * .12f, slot.rotation, Vector3.one)))
                            Handles.DrawWireCube(Vector3.zero, new Vector3(14, .08f, 14));
                        Handles.Label(slot.position + Vector3.up * .4f, slot.name + "\n14 × 14 m reserved");
                    }
                    var nest = go.transform.Find("UniversalMonsterNest");
                    if (nest == null) continue;
                    Handles.color = new Color(1, .68f, .18f, 1);
                    foreach (string direction in new[] { "North", "South", "East", "West" })
                    {
                        var entrance = nest.Find("Entrance_" + direction); if (entrance == null) continue;
                        Handles.DrawAAPolyLine(4, entrance.position - entrance.right * 5 + Vector3.up * .15f, entrance.position + entrance.right * 5 + Vector3.up * .15f);
                        Handles.Label(entrance.position + entrance.forward * 5, direction + "\n10 m ramp");
                    }
                }
            }
            finally { Handles.zTest = previous; Handles.color = color; }
        }
    }
}
