using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a small, texture-free arena around the existing Functions and Relations gameplay.
/// The decorative pieces are combined by material so they add only a few renderers.
/// </summary>
public static class ConfigureFunctionsRelationsEnvironment
{
    private const string Folder = "Assets/Materials/FunctionsRelations/Environment";
    private const string RootName = "FR_Environment";
    private const string PrefabFolder = "Assets/Prefabs/PREFABS/FunctionsRelations";
    private static readonly string[] Names = { "Slate", "Deep Slate", "Sandstone", "Brass", "Arcane Teal" };
    private static readonly Color[] Colors =
    {
        new Color(0.20f, 0.26f, 0.31f),
        new Color(0.075f, 0.12f, 0.17f),
        new Color(0.44f, 0.42f, 0.38f),
        new Color(0.95f, 0.62f, 0.25f),
        new Color(0.20f, 0.77f, 0.82f)
    };

    [MenuItem("Logic Legends/Configure Functions and Relations Environment")]
    public static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Functions and Relations")
            throw new InvalidOperationException("Open the Functions and Relations scene first.");
        GameObject floor = GameObject.Find("Flooring");
        if (floor == null) throw new InvalidOperationException("The stage flooring is missing.");
        Renderer floorRenderer = floor.GetComponentInChildren<Renderer>();
        if (floorRenderer == null) throw new InvalidOperationException("The stage floor has no renderer.");
        GameObject book = GameObject.Find("Book_Statue");
        GameObject managerObject = GameObject.Find("FunctionsRelationsStageManager");
        if (book == null || managerObject == null)
            throw new InvalidOperationException("The Book Statue or stage manager is missing.");

        EnsureFolder();
        GameObject old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        Vector3 origin = new Vector3(floorRenderer.bounds.center.x, floorRenderer.bounds.max.y, floorRenderer.bounds.center.z);
        float bookX = book.transform.position.x - origin.x;
        float bookZ = book.transform.position.z - origin.z;
        Mesh cube = PrimitiveMesh(PrimitiveType.Cube);
        Mesh cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        Mesh ring = RingMesh(64);
        List<CombineInstance>[] parts = new List<CombineInstance>[Names.Length];
        for (int i = 0; i < parts.Length; i++) parts[i] = new List<CombineInstance>();

        // Low border and corner buttresses define the arena without blocking its sight lines.
        // Keep the near edge below eye level: the scene's starting camera sits at floor height.
        AddBox(parts, cube, 1, origin, 0, -.18f, -19.5f, 39f, .36f, .65f);
        AddBox(parts, cube, 1, origin, 0, .23f, 19.5f, 39f, .46f, .65f);
        AddBox(parts, cube, 1, origin, -19.5f, .23f, 0, .65f, .46f, 39f);
        AddBox(parts, cube, 1, origin, 19.5f, .23f, 0, .65f, .46f, 39f);
        AddBox(parts, cube, 3, origin, 0, .47f, 19.5f, 39f, .035f, .09f);

        // The player-facing approach ends at the Book Statue. These pieces are visual
        // inlays only; they cannot change navigation or physical interaction distances.
        for (int z = -18; z <= -8; z += 2)
        {
            AddBox(parts, cube, 0, origin, 0, .025f, z, 5.2f, .045f, 1.82f);
            AddBox(parts, cube, 2, origin, 0, .054f, z, 4.94f, .016f, 1.56f);
        }
        AddBox(parts, cube, 3, origin, -2.54f, .060f, -13, .045f, .014f, 11.7f);
        AddBox(parts, cube, 3, origin, 2.54f, .060f, -13, .045f, .014f, 11.7f);
        AddRing(parts, ring, 1, origin, .052f, bookZ, 4.45f, .23f, bookX);
        AddRing(parts, ring, 3, origin, .060f, bookZ, 3.48f, .065f, bookX);
        AddRing(parts, ring, 4, origin, .062f, bookZ, 5.45f, .055f, bookX);

        // Paired floor medallions give the two diamond towers a deliberate location.
        for (int side = -1; side <= 1; side += 2)
        {
            AddRing(parts, ring, 1, origin, .045f, 7.1f, 2.58f, .24f, side * 16.35f);
            AddRing(parts, ring, 3, origin, .056f, 7.1f, 2.19f, .065f, side * 16.35f);
            AddBox(parts, cube, 4, origin, side * 16.35f, .060f, 4.2f, .075f, .014f, 2.1f);
            AddBox(parts, cube, 4, origin, side * 16.35f, .060f, 10.0f, .075f, .014f, 2.1f);
        }

        // A broken colonnade frames the board. Its posts sit beyond the interaction
        // corridor, and all decorative meshes have no colliders or scripts.
        foreach (float x in new[] { -13.0f, -10.2f, 10.2f, 13.0f })
            Column(parts, cube, cylinder, origin, x, 16.3f, 4.8f, Math.Abs(x) < 12f);
        foreach (float x in new[] { -18.0f, 18.0f })
        {
            Column(parts, cube, cylinder, origin, x, -15.6f, 3.6f, false);
            Column(parts, cube, cylinder, origin, x, -4.5f, 3.1f, false);
        }
        AddBox(parts, cube, 0, origin, -11.6f, 5.10f, 16.3f, 3.8f, .35f, .54f);
        AddBox(parts, cube, 0, origin, 11.6f, 5.10f, 16.3f, 3.8f, .35f, .54f);
        AddBox(parts, cube, 3, origin, -11.6f, 5.30f, 16.3f, 3.4f, .07f, .58f);
        AddBox(parts, cube, 3, origin, 11.6f, 5.30f, 16.3f, 3.4f, .07f, .58f);

        // Broken perimeter slabs provide depth without adding another textured FBX.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                float z = -10f + i * 6.0f;
                if (z > 2f && z < 12f) continue; // keep the diamond towers unobstructed
                AddBox(parts, cube, 0, origin, side * 19.2f, .62f, z, .82f, .8f + (i % 2) * .38f, 3.5f);
                AddBox(parts, cube, 2, origin, side * 19.2f, 1.10f + (i % 2) * .19f, z, .92f, .17f, 3.62f);
            }
        }

        // Staggered masonry seams and two engraved mathematics glyphs keep the
        // existing textured floor readable instead of covering it with new tiles.
        for (int row = 0; row < 5; row++)
        {
            float z = -13f + row * 5.0f;
            foreach (float x in new[] { -8.7f, -6.9f, 6.9f, 8.7f })
                AddBox(parts, cube, 1, origin, x, .018f, z, 1.52f, .012f, .035f);
            AddBox(parts, cube, 1, origin, -7.8f, .018f, z + 2.35f, .035f, .012f, 4.3f);
            AddBox(parts, cube, 1, origin, 7.8f, .018f, z + 2.35f, .035f, .012f, 4.3f);
        }
        FloorGlyphs(parts, cube, origin, bookX, bookZ);

        // Side arches and loose blocks add ruins while staying outside the Book,
        // Domain/Range approach routes, and the board's central sight line.
        foreach (float side in new[] { -1f, 1f })
        {
            AddBox(parts, cube, 0, origin, side * 18.2f, 1.45f, -10.4f, 1.2f, 2.9f, 1.2f);
            AddBox(parts, cube, 0, origin, side * 18.2f, 1.78f, -5.7f, 1.2f, 3.56f, 1.2f);
            AddBox(parts, cube, 2, origin, side * 18.2f, 3.68f, -8.05f, 1.45f, .38f, 5.9f);
            AddBox(parts, cube, 3, origin, side * 18.2f, 3.91f, -8.05f, 1.48f, .07f, 5.2f);
            for (int i = 0; i < 6; i++)
            {
                float z = i < 3 ? -18f + i * 2.0f : 13.2f + (i - 3) * 1.8f;
                float x = side * (16.4f + (i % 3) * .75f);
                AddRotatedBox(parts, cube, i % 2 == 0 ? 0 : 2, x, .16f + .045f * (i % 3), z,
                    .55f + .12f * (i % 2), .30f, .7f, i * 23f);
            }
        }

        GameObject root = new GameObject(RootName);
        root.transform.position = origin;
        GameObject architecture = new GameObject("ArchitectureAndFloor");
        architecture.transform.SetParent(root.transform, false);
        int triangles = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            string path = Folder + "/" + Names[i].Replace(' ', '_') + ".asset";
            Mesh combined = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (combined == null)
            {
                combined = new Mesh();
                AssetDatabase.CreateAsset(combined, path);
            }
            combined.Clear();
            combined.name = Names[i].Replace(' ', '_');
            combined.indexFormat = IndexFormat.UInt16;
            combined.CombineMeshes(parts[i].ToArray(), true, true, false);
            combined.RecalculateBounds();
            EditorUtility.SetDirty(combined);
            triangles += combined.triangles.Length / 3;

            GameObject child = new GameObject(Names[i]);
            child.transform.SetParent(architecture.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = combined;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(i);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        foreach (List<CombineInstance> group in parts)
            foreach (CombineInstance instance in group)
                if (instance.mesh != cube && instance.mesh != cylinder && instance.mesh != ring)
                    UnityEngine.Object.DestroyImmediate(instance.mesh);
        UnityEngine.Object.DestroyImmediate(ring);

        Material cyan = MaterialFor(4);
        Material amber = EnvironmentMaterial("Flame_Amber", "Universal Render Pipeline/Unlit",
            new Color(1.15f, .52f, .12f));
        Mesh crystal = SaveMesh("Arcane_Crystal", OctahedronMesh());
        GameObject distantTrees = BuildGroundAndGrove(root.transform);
        GameObject backgroundDetails = BuildBackground(root.transform, cube, crystal);
        Renderer[] crystalRenderers = BuildPylons(root.transform, cube, cylinder, crystal, cyan, origin);
        Renderer[] flameRenderers = BuildTorches(root.transform, cube, crystal, amber, origin);
        Renderer bookRune = BuildBookRune(root.transform, bookX, bookZ, cyan);
        Renderer[] progressionRunes = BuildProgressionRunes(root.transform, bookX, bookZ, cyan);
        ParticleSystem motes = BuildMotes(root.transform, bookX, bookZ, crystal, cyan);
        MappingChamberEnvironmentController controller = root.AddComponent<MappingChamberEnvironmentController>();
        controller.Configure(managerObject.GetComponent<FunctionsRelationsStageManager>(), book.transform,
            bookRune, progressionRunes, crystalRenderers, flameRenderers, motes,
            backgroundDetails, distantTrees);

        // A texture-free dusk sky and distant-only fog add depth without extra lights
        // or post-processing. The puzzle area stays inside the clear fog distance.
        Material sky = EnvironmentMaterial("Chamber_Sky", "Skybox/Procedural", Color.white);
        sky.SetColor("_SkyTint", new Color(.27f, .38f, .54f));
        sky.SetColor("_GroundColor", new Color(.12f, .17f, .22f));
        sky.SetFloat("_Exposure", .85f);
        sky.SetFloat("_AtmosphereThickness", .68f);
        sky.SetFloat("_SunSize", .018f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.36f, .48f, .60f);
        RenderSettings.fogStartDistance = 62f;
        RenderSettings.fogEndDistance = 125f;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Functions and Relations environment rebuilt: " + triangles +
            " static architecture triangles, meadow ground and reused low-poly nature prefabs.");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Materials/FunctionsRelations", "Environment");
    }

    private static Material MaterialFor(int index)
    {
        string path = Folder + "/" + Names[index].Replace(' ', '_') + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(index >= 3 ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("The URP shader is unavailable: " + index);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", Colors[index]);
        if (index < 3)
        {
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject temporary = GameObject.CreatePrimitive(type);
        Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temporary);
        return mesh;
    }

    private static Mesh RingMesh(int segments)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments * 2];
        int[] triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            vertices[i * 2] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            vertices[i * 2 + 1] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            int next = (i + 1) % segments;
            int t = i * 6;
            triangles[t] = i * 2;
            triangles[t + 1] = next * 2;
            triangles[t + 2] = i * 2 + 1;
            triangles[t + 3] = i * 2 + 1;
            triangles[t + 4] = next * 2;
            triangles[t + 5] = next * 2 + 1;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        return mesh;
    }

    private static void AddRing(List<CombineInstance>[] parts, Mesh ring, int index, Vector3 origin,
        float height, float z, float radius, float width, float x = 0f)
    {
        Mesh shaped = new Mesh();
        Vector3[] vertices = ring.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            float r = radius + (i % 2 == 0 ? -width * .5f : width * .5f);
            vertices[i] *= r;
        }
        shaped.vertices = vertices;
        shaped.triangles = ring.triangles;
        shaped.RecalculateNormals();
        Add(parts, shaped, index, origin, x, height, z, 1, 1, 1);
    }

    private static void Column(List<CombineInstance>[] parts, Mesh cube, Mesh cylinder,
        Vector3 origin, float x, float z, float height, bool bright)
    {
        AddBox(parts, cube, 1, origin, x, .30f, z, 1.65f, .60f, 1.65f);
        AddBox(parts, cube, 2, origin, x, .68f, z, 1.39f, .18f, 1.39f);
        Add(parts, cylinder, 0, origin, x, .84f + (height - 1.36f) * .5f, z, .48f, (height - 1.36f) * .5f, .48f);
        AddBox(parts, cube, 2, origin, x, height - .28f, z, 1.32f, .28f, 1.32f);
        AddBox(parts, cube, 1, origin, x, height, z, 1.62f, .30f, 1.62f);
        AddBox(parts, cube, bright ? 4 : 3, origin, x, height + .17f, z, 1.08f, .045f, 1.08f);
        if (bright)
            AddBox(parts, cube, 4, origin, x, height * .53f, z - .49f, .14f, height * .43f, .035f);
    }

    private static void AddBox(List<CombineInstance>[] parts, Mesh cube, int index, Vector3 origin,
        float x, float y, float z, float sx, float sy, float sz)
    {
        Add(parts, cube, index, origin, x, y, z, sx, sy, sz);
    }

    private static void Add(List<CombineInstance>[] parts, Mesh mesh, int index, Vector3 origin,
        float x, float y, float z, float sx, float sy, float sz)
    {
        parts[index].Add(new CombineInstance
        {
            mesh = mesh,
            transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.identity, new Vector3(sx, sy, sz))
        });
    }

    private static void AddRotatedBox(List<CombineInstance>[] parts, Mesh cube, int index,
        float x, float y, float z, float sx, float sy, float sz, float yaw)
    {
        parts[index].Add(new CombineInstance
        {
            mesh = cube,
            transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, yaw, 0),
                new Vector3(sx, sy, sz))
        });
    }

    private static void FloorGlyphs(List<CombineInstance>[] parts, Mesh cube, Vector3 origin,
        float bookX, float bookZ)
    {
        float left = bookX - 7.7f;
        float right = bookX + 7.7f;
        // A stylized pi and sigma, engraved at the sides of the Book's rune circle.
        AddBox(parts, cube, 3, origin, left, .024f, bookZ - .45f, 1.4f, .014f, .07f);
        AddBox(parts, cube, 3, origin, left - .49f, .024f, bookZ + .11f, .07f, .014f, 1.08f);
        AddBox(parts, cube, 3, origin, left + .49f, .024f, bookZ + .11f, .07f, .014f, 1.08f);
        AddBox(parts, cube, 3, origin, right, .024f, bookZ - .47f, 1.4f, .014f, .07f);
        AddBox(parts, cube, 3, origin, right, .024f, bookZ + .55f, 1.4f, .014f, .07f);
        AddRotatedBox(parts, cube, 3, right - .28f, .024f, bookZ - .19f,
            1.02f, .014f, .07f, 52f);
        AddRotatedBox(parts, cube, 3, right - .28f, .024f, bookZ + .30f,
            1.02f, .014f, .07f, -52f);
    }

    private static GameObject BuildGroundAndGrove(Transform root)
    {
        GameObject landscape = new GameObject("GroundAndGrove");
        landscape.transform.SetParent(root, false);
        Material grass = EnvironmentMaterial("Meadow_Grass", "Universal Render Pipeline/Lit",
            new Color(.31f, .46f, .24f));
        grass.SetFloat("_Smoothness", 0f);
        grass.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(grass);
        Mesh terrain = SaveMesh("Meadow_Ground", MeadowMesh());
        MeshRenderer ground = PlaceMesh(landscape.transform, "Continuous Meadow", terrain,
            grass, Vector3.zero, Vector3.one);
        MeshCollider groundCollider = ground.gameObject.AddComponent<MeshCollider>();
        groundCollider.sharedMesh = terrain;

        // The raised, forested perimeter and four simple blockers keep the player
        // on solid ground even after walking away from the stone platform.
        GameObject limits = new GameObject("Outer Meadow Boundary");
        limits.transform.SetParent(landscape.transform, false);
        Boundary(limits.transform, "West", new Vector3(-62f, 5f, 0), new Vector3(2f, 24f, 128f));
        Boundary(limits.transform, "East", new Vector3(62f, 5f, 0), new Vector3(2f, 24f, 128f));
        Boundary(limits.transform, "South", new Vector3(0, 5f, -62f), new Vector3(128f, 24f, 2f));
        Boundary(limits.transform, "North", new Vector3(0, 5f, 62f), new Vector3(128f, 24f, 2f));

        GameObject near = new GameObject("Near Grove");
        near.transform.SetParent(landscape.transform, false);
        GameObject far = new GameObject("Distant Grove (Medium and High Quality)");
        far.transform.SetParent(landscape.transform, false);
        Vector2[] trees =
        {
            new Vector2(-27,-28), new Vector2(-35,-23), new Vector2(-42,-8),
            new Vector2(-29,-5), new Vector2(-23,15), new Vector2(-43,20),
            new Vector2(-24,26), new Vector2(-15,45), new Vector2(15,45),
            new Vector2(24,26), new Vector2(43,20), new Vector2(23,15),
            new Vector2(29,-5), new Vector2(42,-8), new Vector2(35,-23),
            new Vector2(27,-28), new Vector2(-15,-34), new Vector2(15,-34),
            new Vector2(-46,-36), new Vector2(46,-36), new Vector2(-48,34),
            new Vector2(48,34), new Vector2(-52,0), new Vector2(52,0),
            new Vector2(-18,45), new Vector2(18,45)
        };
        for (int i = 0; i < trees.Length; i++)
        {
            int variant = i >= 6 && i <= 9 ? (i % 2 == 0 ? 4 : 1) : i % 5 + 1;
            float scale = i >= 6 && i <= 9 ? (i % 2 == 0 ? 2.45f : 2.65f) :
                (i == 4 || i == 11 ? 2.2f : .92f + (i % 4) * .12f);
            NaturePrefab(i < 18 ? near.transform : far.transform,
                "Tree_0" + variant, "Grove Tree " + (i + 1), trees[i], scale,
                i * 47f % 360f, true);
        }

        GameObject undergrowth = new GameObject("RocksAndUndergrowth");
        undergrowth.transform.SetParent(landscape.transform, false);
        Vector2[] rocks =
        {
            new Vector2(-24,-17), new Vector2(24,-17), new Vector2(-25,16),
            new Vector2(25,16), new Vector2(-37,-31), new Vector2(37,-31),
            new Vector2(-34,34), new Vector2(34,34)
        };
        for (int i = 0; i < rocks.Length; i++)
            NaturePrefab(undergrowth.transform, "Rock_0" + (i % 5 + 1),
                "Meadow Rock " + (i + 1), rocks[i], 1.5f + (i % 3) * .28f,
                i * 67f % 360f, true);
        Vector2[] plants =
        {
            new Vector2(-24,-23), new Vector2(24,-23), new Vector2(-27,21),
            new Vector2(27,21), new Vector2(-38,4), new Vector2(38,4),
            new Vector2(-31,-35), new Vector2(31,-35), new Vector2(-33,31),
            new Vector2(33,31), new Vector2(-45,-14), new Vector2(45,-14)
        };
        for (int i = 0; i < plants.Length; i++)
            NaturePrefab(undergrowth.transform, i < 6 ? "Bush_01" : "Grass_01",
                "Meadow Plant " + (i + 1), plants[i], i < 6 ? 1.65f : 2.0f,
                i * 53f % 360f, false);
        return far;
    }

    private static Mesh MeadowMesh()
    {
        const int segments = 24;
        const float extent = 64f;
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[(segments + 1) * (segments + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * segments * 6];
        for (int z = 0; z <= segments; z++)
        {
            for (int x = 0; x <= segments; x++)
            {
                float px = -extent + x * extent * 2f / segments;
                float pz = -extent + z * extent * 2f / segments;
                int index = z * (segments + 1) + x;
                vertices[index] = new Vector3(px, MeadowHeight(px, pz), pz);
                uv[index] = new Vector2(x / (float)segments, z / (float)segments);
                if (x == segments || z == segments) continue;
                int a = index, b = index + 1, c = index + segments + 1, d = c + 1;
                int t = (z * segments + x) * 6;
                triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = b;
                triangles[t + 3] = b; triangles[t + 4] = c; triangles[t + 5] = d;
            }
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static float MeadowHeight(float x, float z)
    {
        float edgeDistance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        if (edgeDistance <= 22f) return -.18f;
        float away = edgeDistance - 22f;
        float wave = Mathf.Sin(x * .14f) * Mathf.Cos(z * .11f) * Mathf.Min(away * .012f, .22f);
        return -.18f + away * .06f + wave + Mathf.Max(0f, edgeDistance - 54f) * .40f;
    }

    private static void Boundary(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.size = size;
    }

    private static void NaturePrefab(Transform parent, string prefabName, string name,
        Vector2 position, float scale, float yaw, bool keepCollider)
    {
        string path = "Assets/CHEATCODE/SimpleNaturePack/Prefabs/" + prefabName + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Nature prefab missing: " + path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = new Vector3(position.x, MeadowHeight(position.x, position.y), position.y);
        instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        instance.transform.localScale = Vector3.one * scale;
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        if (!keepCollider)
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
    }

    private static GameObject BuildBackground(Transform root, Mesh cube, Mesh crystal)
    {
        GameObject background = new GameObject("BackgroundEnvironment");
        background.transform.SetParent(root, false);
        Mesh mountain = MountainMesh();
        List<CombineInstance> far = new List<CombineInstance>();
        List<CombineInstance> near = new List<CombineInstance>();
        float[] farX = { -64f, -47f, -29f, -10f, 9f, 28f, 47f, 65f };
        float[] farHeight = { 24f, 30f, 23f, 27f, 24f, 29f, 26f, 23f };
        for (int i = 0; i < farX.Length; i++)
            Peak(far, mountain, farX[i], -10f, 58f + (i % 2) * 7f,
                15f + (i % 3) * 2f, farHeight[i], i * 37f);
        float[] nearX = { -56f, -39f, -22f, 19f, 37f, 55f };
        for (int i = 0; i < nearX.Length; i++)
            Peak(near, mountain, nearX[i], -10f, 43f + (i % 2) * 4f,
                11f + (i % 3) * 1.5f, 15f + (i % 3) * 2f, i * 43f);
        CreateCombinedRenderer(background.transform, "Far Mountains", "Far_Mountains", far,
            EnvironmentMaterial("Far_Haze", "Universal Render Pipeline/Unlit", new Color(.28f, .40f, .51f)));
        CreateCombinedRenderer(background.transform, "Near Mountains", "Near_Mountains", near,
            EnvironmentMaterial("Near_Rock", "Universal Render Pipeline/Unlit", new Color(.16f, .25f, .34f)));
        UnityEngine.Object.DestroyImmediate(mountain);

        GameObject details = new GameObject("Distant Floating Ruins (High Quality)");
        details.transform.SetParent(background.transform, false);
        List<CombineInstance> islands = new List<CombineInstance>();
        List<CombineInstance> islandRunes = new List<CombineInstance>();
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * 43f;
            islands.Add(new CombineInstance { mesh = crystal,
                transform = Matrix4x4.TRS(new Vector3(x, 10f, 38f), Quaternion.Euler(0, 22, 0),
                    new Vector3(5.4f, 2.1f, 4f)) });
            for (int i = 0; i < 3; i++)
                islands.Add(new CombineInstance { mesh = cube,
                    transform = Matrix4x4.TRS(new Vector3(x + (i - 1) * 1.7f, 12.0f + i % 2, 38f),
                        Quaternion.Euler(0, i * 11, 0), new Vector3(.8f, 3.0f + i % 2, .8f)) });
            islandRunes.Add(new CombineInstance { mesh = crystal,
                transform = Matrix4x4.TRS(new Vector3(x, 15.5f, 38f), Quaternion.identity,
                    new Vector3(.45f, .95f, .45f)) });
        }
        CreateCombinedRenderer(details.transform, "Island Silhouettes", "Floating_Islands", islands,
            EnvironmentMaterial("Island_Rock", "Universal Render Pipeline/Unlit", new Color(.13f, .21f, .29f)));
        CreateCombinedRenderer(details.transform, "Island Crystals", "Island_Crystals", islandRunes,
            MaterialFor(4));
        return details;
    }

    private static void Peak(List<CombineInstance> list, Mesh mountain,
        float x, float y, float z, float radius, float height, float yaw)
    {
        list.Add(new CombineInstance { mesh = mountain,
            transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, yaw, 0),
                new Vector3(radius, height, radius * .75f)) });
    }

    private static Mesh MountainMesh()
    {
        const int sides = 7;
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[sides * 3];
        int[] triangles = new int[vertices.Length];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            float b = (i + 1) * Mathf.PI * 2f / sides;
            float r1 = i % 2 == 0 ? 1f : .88f;
            float r2 = (i + 1) % 2 == 0 ? 1f : .88f;
            vertices[i * 3] = new Vector3(Mathf.Cos(a) * r1, 0, Mathf.Sin(a) * r1);
            vertices[i * 3 + 1] = new Vector3(.10f, 1f, -.09f);
            vertices[i * 3 + 2] = new Vector3(Mathf.Cos(b) * r2, 0, Mathf.Sin(b) * r2);
            triangles[i * 3] = i * 3;
            triangles[i * 3 + 1] = i * 3 + 1;
            triangles[i * 3 + 2] = i * 3 + 2;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Renderer[] BuildPylons(Transform root, Mesh cube, Mesh cylinder,
        Mesh crystal, Material cyan, Vector3 origin)
    {
        GameObject group = new GameObject("MagicalCrystals");
        group.transform.SetParent(root, false);
        GameObject template = new GameObject("ArcanePylon");
        PlaceMesh(template.transform, "Stone Base", cube, MaterialFor(1),
            new Vector3(0, .23f, 0), new Vector3(1.35f, .46f, 1.35f));
        PlaceMesh(template.transform, "Stone Socket", cylinder, MaterialFor(0),
            new Vector3(0, .62f, 0), new Vector3(.48f, .20f, .48f));
        PlaceMesh(template.transform, "Brass Rim", cube, MaterialFor(3),
            new Vector3(0, .84f, 0), new Vector3(1.0f, .08f, 1.0f));
        PlaceMesh(template.transform, "Arcane Crystal", crystal, cyan,
            new Vector3(0, 1.69f, 0), new Vector3(.74f, 1.04f, .74f));
        string path = PrefabFolder + "/ArcanePylon.prefab";
        PrefabUtility.SaveAsPrefabAsset(template, path);
        UnityEngine.Object.DestroyImmediate(template);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Renderer[] crystals = new Renderer[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = side < 0 ? "Domain Outer Pylon" : "Range Outer Pylon";
            instance.transform.SetParent(group.transform, false);
            instance.transform.localPosition = new Vector3(side * 18.25f, 0, 7.1f);
            crystals[i] = instance.transform.Find("Arcane Crystal").GetComponent<Renderer>();
        }
        return crystals;
    }

    private static Renderer[] BuildTorches(Transform root, Mesh cube, Mesh crystal,
        Material amber, Vector3 origin)
    {
        GameObject group = new GameObject("DecorativeLighting");
        group.transform.SetParent(root, false);
        GameObject template = new GameObject("TempleTorch");
        PlaceMesh(template.transform, "Bowl", cube, MaterialFor(1),
            new Vector3(0, .12f, 0), new Vector3(.72f, .24f, .72f));
        PlaceMesh(template.transform, "Brass Lip", cube, MaterialFor(3),
            new Vector3(0, .27f, 0), new Vector3(.58f, .07f, .58f));
        PlaceMesh(template.transform, "Magical Flame", crystal, amber,
            new Vector3(0, .73f, 0), new Vector3(.23f, .48f, .23f));
        string path = PrefabFolder + "/TempleTorch.prefab";
        PrefabUtility.SaveAsPrefabAsset(template, path);
        UnityEngine.Object.DestroyImmediate(template);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Renderer[] flames = new Renderer[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = side < 0 ? "Left Board Torch" : "Right Board Torch";
            instance.transform.SetParent(group.transform, false);
            instance.transform.localPosition = new Vector3(side * 10.2f, 5.29f, 16.3f);
            flames[i] = instance.transform.Find("Magical Flame").GetComponent<Renderer>();
        }
        return flames;
    }

    private static Renderer BuildBookRune(Transform root, float x, float z, Material cyan)
    {
        GameObject group = new GameObject("RuneDecorations");
        group.transform.SetParent(root, false);
        Mesh mesh = SaveMesh("Book_Rune", ArcMesh(64, 0f, Mathf.PI * 2f, 3.89f, 4.00f));
        MeshRenderer renderer = PlaceMesh(group.transform, "Book Aura Rune", mesh, cyan,
            new Vector3(x, .072f, z), Vector3.one);
        return renderer;
    }

    private static Renderer[] BuildProgressionRunes(Transform root, float x, float z, Material cyan)
    {
        Transform group = root.Find("RuneDecorations");
        Mesh arc = SaveMesh("Challenge_Rune_Arc", ArcMesh(18, 0f, Mathf.PI * 2f / 5f - .11f,
            5.98f, 6.075f));
        Renderer[] renderers = new Renderer[5];
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = PlaceMesh(group, "Challenge " + (i + 1) + " Rune", arc,
                cyan, new Vector3(x, .075f, z), Vector3.one);
            renderer.transform.localRotation = Quaternion.Euler(0, -i * 72f, 0);
            renderers[i] = renderer;
        }
        return renderers;
    }

    private static Mesh ArcMesh(int segments, float start, float end, float inner, float outer)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(start, end, i / (float)segments);
            vertices[i * 2] = new Vector3(Mathf.Cos(a) * inner, 0, Mathf.Sin(a) * inner);
            vertices[i * 2 + 1] = new Vector3(Mathf.Cos(a) * outer, 0, Mathf.Sin(a) * outer);
            if (i == segments) continue;
            int t = i * 6;
            triangles[t] = i * 2;
            triangles[t + 1] = (i + 1) * 2;
            triangles[t + 2] = i * 2 + 1;
            triangles[t + 3] = i * 2 + 1;
            triangles[t + 4] = (i + 1) * 2;
            triangles[t + 5] = (i + 1) * 2 + 1;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static ParticleSystem BuildMotes(Transform root, float x, float z,
        Mesh crystal, Material cyan)
    {
        GameObject group = new GameObject("AmbientEffects");
        group.transform.SetParent(root, false);
        GameObject effect = new GameObject("Book Motes", typeof(ParticleSystem));
        effect.transform.SetParent(group.transform, false);
        effect.transform.localPosition = new Vector3(x, 1.85f, z);
        ParticleSystem system = effect.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .20f);
        main.startSize = new ParticleSystem.MinMaxCurve(.055f, .095f);
        main.maxParticles = 12;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 3f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2.7f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = crystal;
        renderer.sharedMaterial = cyan;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    private static Mesh OctahedronMesh()
    {
        Vector3 top = new Vector3(0, 1, 0), bottom = new Vector3(0, -1, 0);
        Vector3[] rim = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        Vector3[] vertices = new Vector3[24];
        int[] triangles = new int[24];
        for (int i = 0; i < 4; i++)
        {
            vertices[i * 3] = top;
            vertices[i * 3 + 1] = rim[i];
            vertices[i * 3 + 2] = rim[(i + 1) % 4];
            vertices[12 + i * 3] = bottom;
            vertices[12 + i * 3 + 1] = rim[(i + 1) % 4];
            vertices[12 + i * 3 + 2] = rim[i];
        }
        for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh SaveMesh(string name, Mesh source)
    {
        string path = Folder + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            source.name = name;
            AssetDatabase.CreateAsset(source, path);
            return source;
        }
        mesh.Clear();
        mesh.indexFormat = source.indexFormat;
        mesh.vertices = source.vertices;
        mesh.triangles = source.triangles;
        mesh.normals = source.normals;
        mesh.uv = source.uv;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        UnityEngine.Object.DestroyImmediate(source);
        return mesh;
    }

    private static Material EnvironmentMaterial(string name, string shaderName, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static MeshRenderer PlaceMesh(Transform parent, string name, Mesh mesh,
        Material material, Vector3 localPosition, Vector3 localScale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static MeshRenderer CreateCombinedRenderer(Transform parent, string name,
        string assetName, List<CombineInstance> parts, Material material)
    {
        Mesh source = new Mesh();
        source.indexFormat = IndexFormat.UInt16;
        source.CombineMeshes(parts.ToArray(), true, true, false);
        source.RecalculateBounds();
        Mesh mesh = SaveMesh(assetName, source);
        return PlaceMesh(parent, name, mesh, material, Vector3.zero, Vector3.one);
    }
}
