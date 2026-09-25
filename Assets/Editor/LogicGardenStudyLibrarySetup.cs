#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LogicGardenStudyLibrarySetup
{
    private const string RootName = "LogicGarden Study Library";

    [MenuItem("Logic Legends/Setup LogicGarden Study Library")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "LogicGarden")
        {
            Debug.LogError("Open the LogicGarden scene before running the study library setup.");
            return;
        }

        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create LogicGarden Study Library");
        StudyLibraryController controller = root.AddComponent<StudyLibraryController>();

        Transform[] allTransforms = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
            .ToArray();

        string[] shelfNames = { "BOOKSHELVES", "BOOKSHELVES (1)", "BOOKSHELVES (2)", "BOOKSHELVES (3)" };
        string[] periods = { "PRELIM", "MIDTERMS", "PRE-FINALS", "FINALS" };
        List<Transform> allBooks = allTransforms
            .Where(t => t.name.StartsWith("Book") && t.GetComponent<Renderer>() != null)
            .ToList();

        for (int shelfIndex = 0; shelfIndex < shelfNames.Length; shelfIndex++)
        {
            Transform shelf = allTransforms.FirstOrDefault(t => t.name == shelfNames[shelfIndex]);
            if (shelf == null)
            {
                Debug.LogError("Missing shelf: " + shelfNames[shelfIndex]);
                continue;
            }

            List<Transform> books = allBooks
                .OrderBy(t => HorizontalDistance(t.position, shelf.position))
                .Take(4)
                .OrderBy(t => t.position.x)
                .ToList();

            Vector3 bookCenter = Vector3.zero;
            foreach (Transform book in books) bookCenter += book.position;
            bookCenter /= Mathf.Max(1, books.Count);

            Vector3 outward = Mathf.Cos(shelf.eulerAngles.y * Mathf.Deg2Rad) < 0f ? Vector3.back : Vector3.forward;
            Vector3 focusPoint = new Vector3(bookCenter.x, bookCenter.y + 0.05f, bookCenter.z);

            GameObject cameraObject = new GameObject(periods[shelfIndex] + " Shelf Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = focusPoint + outward * 3.1f;
            cameraObject.transform.rotation = Quaternion.LookRotation(focusPoint - cameraObject.transform.position, Vector3.up);
            CinemachineCamera focusCamera = cameraObject.AddComponent<CinemachineCamera>();
            focusCamera.Priority.Value = -10;
            LensSettings lens = focusCamera.Lens;
            lens.FieldOfView = 40f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 5000f;
            focusCamera.Lens = lens;

            GameObject zoneObject = new GameObject(periods[shelfIndex] + " Shelf Interaction Zone");
            zoneObject.transform.SetParent(root.transform, false);
            zoneObject.transform.position = new Vector3(bookCenter.x, bookCenter.y + 0.8f, bookCenter.z) + outward * 4f;
            BoxCollider trigger = zoneObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(11f, 5.5f, 8f);
            Rigidbody body = zoneObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            StudyShelfZone zone = zoneObject.AddComponent<StudyShelfZone>();

            Vector3[] cardPositions = new Vector3[books.Count];
            for (int i = 0; i < books.Count; i++)
                cardPositions[i] = books[i].position + Vector3.up * 0.35f + outward * 0.72f;

            Vector3 labelPosition = new Vector3(bookCenter.x, bookCenter.y + 4.98f, bookCenter.z) + outward * 0.9f;
            Vector3 canvasRotation = outward == Vector3.back ? Vector3.zero : new Vector3(0f, 180f, 0f);
            zone.Configure(controller, focusCamera, periods[shelfIndex], StudyLibraryCatalog.GetShelf(periods[shelfIndex]),
                labelPosition, cardPositions, canvasRotation);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log("LogicGarden study library created for PRELIM, MIDTERMS, PRE-FINALS, and FINALS.");
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector2 delta = new Vector2(a.x - b.x, a.z - b.z);
        return delta.sqrMagnitude;
    }
}
#endif
