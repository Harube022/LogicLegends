using System;
using System.Linq;
using LogicLegends.Induction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Rebinds an existing puzzle to the user's replacement models without rebuilding the stage.</summary>
public static class InductionStationReconnect
{
    const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/Induction/";
    static readonly string[] Names = { "Base_Case", "Inductive Hypothesis 1", "Inductive Step 1" };
    static readonly string[] Assets = { "Base_Case.fbx", "InductiveHypothesis 1.fbx", "InductiveStep 1.fbx" };

    [MenuItem("Tools/Logic Legends/Reconnect Induction Replacement Altars")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before reconnecting stations.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Induction.unity") throw new InvalidOperationException("Open Induction first.");
        var manager = Object.FindFirstObjectByType<InductionStageManager>();
        if (manager == null || manager.slots.Length != 6 || manager.slots.Any(s => s == null))
            throw new InvalidOperationException("The six existing Induction answer targets are needed for this migration.");
        var oldStations = manager.stations.ToArray();
        var oldSlots = manager.slots.ToArray();
        var replacements = new Transform[3];
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Reconnect Induction replacement altars");
        for (int index = 0; index < 3; index++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + Assets[index]);
            if (prefab == null) throw new InvalidOperationException("Missing replacement model: " + Assets[index]);
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Names[index]);
            if (existing == null)
            {
                // Deleted models have offset pivots. Retain their visible layout using
                // the surviving target positions, rather than the broken root positions.
                var groupSlots = oldSlots.Where(s => s.key.StartsWith(new[] { "Base:", "Hypothesis:", "Step:" }[index])).ToArray();
                float x = groupSlots.Average(s => s.transform.position.x);
                float z = groupSlots.Max(s => s.transform.position.z) + 2.96f;
                var floor = scene.GetRootGameObjects().Single(g => g.name == "Stage").GetComponent<MeshCollider>();
                RaycastHit hit;
                if (!floor.Raycast(new Ray(new Vector3(x, 60, z), Vector3.down), out hit, 50))
                    throw new InvalidOperationException("No walk surface beneath the replacement station.");
                existing = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(existing, "Use replacement Induction model");
                existing.name = Names[index]; existing.transform.rotation = Quaternion.Euler(0,180,0);
                existing.transform.position = new Vector3(x, hit.point.y - prefab.GetComponent<MeshFilter>().sharedMesh.bounds.min.y, z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(existing.transform);
            }
            replacements[index] = existing.transform;
            var filter = existing.GetComponent<MeshFilter>();
            if (existing.GetComponent<MeshCollider>() == null)
            { var collision = Undo.AddComponent<MeshCollider>(existing); collision.sharedMesh = filter.sharedMesh; }
        }

        var baseSlot = oldSlots.Single(s => s.key == "Base:1");
        var newBase = Object.Instantiate(baseSlot.gameObject).GetComponent<InductionSlot>();
        Undo.RegisterCreatedObjectUndo(newBase.gameObject, "Replace the old Base Case Slot");
        newBase.name = "Base Scroll Target";
        Undo.RecordObject(manager, "Reconnect Induction station references");
        manager.slots = oldSlots.Select(s => s == baseSlot ? newBase : s).ToArray();
        manager.stations = replacements;
        Undo.DestroyObjectImmediate(baseSlot.gameObject);
        for (int index = 0; index < replacements.Length; index++)
        {
            var station = replacements[index];
            var bounds = station.GetComponent<MeshRenderer>().bounds;
            var prefix = new[] { "Base:", "Hypothesis:", "Step:" }[index];
            var targets = manager.slots.Where(s => s.key.StartsWith(prefix)).OrderBy(s => s.key).ToArray();
            for (int number = 0; number < targets.Length; number++)
            {
                var slot = targets[number]; Undo.SetTransformParent(slot.transform, station, "Attach scroll target to replacement altar");
                Undo.RecordObject(slot.transform, "Position scroll above altar");
                float x = bounds.center.x + (targets.Length == 1 ? 0 : number % 2 == 0 ? -1.8f : 1.8f);
                float z = targets.Length == 1 ? bounds.max.z - .7f : bounds.center.z + (number < 2 ? 1.35f : -1.35f);
                float y = bounds.max.y + (targets.Length == 4 && number < 2 ? 1.8f : .35f);
                slot.transform.SetPositionAndRotation(new Vector3(x,y,z), Quaternion.identity);
                slot.name = index == 0 ? "Base Scroll Target" : index == 1 ? "Hypothesis Scroll Target" : "Step Scroll Target " + (number + 1);
                slot.tabletAnchor.localPosition = Vector3.up * .35f;
                slot.label.transform.position = slot.tabletAnchor.position + Vector3.up * 2.5f;
                slot.label.rectTransform.sizeDelta = new Vector2(3.2f,.8f); slot.label.font = manager.readingFont;
                EditorUtility.SetDirty(slot); EditorUtility.SetDirty(slot.tabletAnchor); EditorUtility.SetDirty(slot.label);
            }
            var label = oldStations[index] == null ? null : oldStations[index].GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Station Label");
            if (label != null)
            {
                Undo.SetTransformParent(label.transform, station, "Reconnect station title");
                label.transform.position = new Vector3(bounds.center.x,bounds.max.y + .5f,bounds.min.z - .3f);
                label.rectTransform.sizeDelta = new Vector2(7,.7f);
                if (label.GetComponent<FaceCamera>() == null) Undo.AddComponent<FaceCamera>(label.gameObject);
                EditorUtility.SetDirty(label);
            }
        }
        // Three separated choice scrolls above each of the first two altars leave
        // the step altar's four ordered answer targets unobstructed.
        for (int index = 0; index < manager.tabletSpawns.Length; index++)
        {
            var parent = replacements[index / 3]; var bounds = parent.GetComponent<MeshRenderer>().bounds;
            var home = manager.tabletSpawns[index]; Undo.SetTransformParent(home,parent,"Attach scroll choice to altar");
            Undo.RecordObject(home,"Space formula choices above altar");
            home.SetPositionAndRotation(new Vector3(bounds.center.x + (index % 3 - 1) * 2.6f,bounds.max.y + .35f,bounds.min.z + .65f),Quaternion.identity);
        }
        foreach (var old in oldStations.Where(t => t != null && !replacements.Contains(t))) Undo.DestroyObjectImmediate(old.gameObject);
        manager.tabletPrefab.GetComponent<InductionTablet>().choiceLabelWidth = 1.9f;
        EditorUtility.SetDirty(manager.tabletPrefab.GetComponent<InductionTablet>());
        Undo.CollapseUndoOperations(group);
        InductionInvisibleAnchors.Apply();
        Validate(manager); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Induction replacement altars connected; old Base Case Slot and missing station objects removed.");
    }

    public static void Validate(InductionStageManager manager)
    {
        if (manager.stations.Length != 3 || manager.slots.Length != 6 || manager.slots.Any(s=>s==null) || manager.slots.Select(s=>s.key).Distinct().Count()!=6)
            throw new InvalidOperationException("Induction needs exactly three stations and six unique answer targets.");
        for(int index=0;index<3;index++)
        {
            var station=manager.stations[index];
            if(station==null || station.name!=Names[index] || !PrefabUtility.IsPartOfPrefabInstance(station))
                throw new InvalidOperationException("The replacement altar must remain connected: " + Names[index]);
            var prefix=new[]{"Base:","Hypothesis:","Step:"}[index];
            if(manager.slots.Where(s=>s.key.StartsWith(prefix)).Any(s=>s.transform.parent!=station || s.tabletAnchor.position.y<=InductionInvisibleAnchors.SurfaceHeight(station)))
                throw new InvalidOperationException("Scroll targets must be above their replacement altar.");
        }
    }
}
