using System;
using LogicLegends.OpenArchive.Topics;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Meshes and effects for the arrival, the exit gate and the restored Pillar.</summary>
internal sealed class SoloTrialCinematicVisuals : IDisposable
{
    static readonly Color Teal = new Color(.25f, .9f, .84f), Gold = new Color(1, .73f, .28f);
    static readonly int Tint = Shader.PropertyToID("_Tint"), Formation = Shader.PropertyToID("_Formation"),
        ReviewTime = Shader.PropertyToID("_ReviewTime"), Restored = Shader.PropertyToID("_Restored");
    readonly SoloTrialCinematicAssets assets;
    readonly GameObject root, gate;
    readonly ArchiveMatchCinematicFx glyphs;
    readonly Vector3[] summonFeet = new Vector3[1];
    readonly Mesh faceMesh, ringMesh;
    readonly Material faceMaterial, ringMaterial;
    readonly Renderer face, rim;
    readonly MaterialPropertyBlock paint = new MaterialPropertyBlock();
    GameObject sanctuary;
    Renderer pillar, backdropSeal;
    Transform symbols, innerHoop, outerHoop;
    Vector3 sanctuaryBase;
    Light restorationLight;
    Material crystalMaterial;

    public SoloTrialCinematicVisuals(Transform owner, SoloTrialCinematicAssets source)
    {
        assets = source;
        root = new GameObject("Solo Cinematic Art");
        root.transform.SetParent(owner, false);
        glyphs = new ArchiveMatchCinematicFx(root.transform, assets.summonEnergy);
        gate = new GameObject("Trial Exit Gate");
        gate.transform.SetParent(root.transform, false);
        faceMesh = Ring("Portal Face", 0, 1);
        ringMesh = Ring("Portal Rim", .88f, 1f);
        faceMaterial = new Material(assets.portalShader);
        ringMaterial = new Material(assets.portalShader) { renderQueue = faceMaterial.renderQueue + 1 };
        ringMaterial.SetFloat("_Membrane", 0);
        face = Part("Swirling Passage", gate.transform, faceMesh, faceMaterial);
        rim = Part("Runic Rim", gate.transform, ringMesh, ringMaterial);
        rim.transform.localScale = Vector3.one * 1.05f;
        Hide();
    }

    public void Summon(Vector3 feet, float progress)
    {
        summonFeet[0] = feet;
        glyphs.ShowSummon(summonFeet, progress, Teal);
    }

    public void Portal(Vector3 feet, Vector3 forward, float height, float amount, float time)
    {
        if (!gate.activeSelf) gate.SetActive(true);
        gate.transform.SetPositionAndRotation(feet + Vector3.up * (height * .62f), Quaternion.LookRotation(-forward, Vector3.up));
        gate.transform.localScale = new Vector3(height * .45f, height * .65f, 1) * Mathf.Lerp(.06f, 1, amount);
        rim.transform.localRotation = Quaternion.Euler(0, 0, time * 42);
        PortalPaint(face, new Color(.22f, .95f, 1.25f), amount, time);
        PortalPaint(rim, new Color(1.45f, .96f, .27f), amount, time);
        summonFeet[0] = feet;
        glyphs.ShowSummon(summonFeet, time / 3.5f, Gold);
    }

    void PortalPaint(Renderer target, Color color, float amount, float time)
    {
        paint.Clear();
        paint.SetColor(Tint, color);
        paint.SetFloat(Formation, amount);
        paint.SetFloat(ReviewTime, time);
        target.SetPropertyBlock(paint);
    }

    public void BuildSanctuary(Vector3 position)
    {
        sanctuaryBase = position;
        sanctuary = new GameObject("Pillar Sanctuary");
        sanctuary.transform.SetParent(root.transform, false);
        sanctuary.transform.position = position;
        for (int i = 0; i < 3; i++)
            Primitive("Sanctuary Terrace", PrimitiveType.Cylinder, new Vector3(0, .28f * i, 0),
                new Vector3(13 - i * 2.4f, .28f, 13 - i * 2.4f), assets.templeStone);

        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.Lerp(-1.26f, 1.26f, i / 4f);
            var basePoint = new Vector3(Mathf.Sin(angle) * 6.6f, 0, Mathf.Cos(angle) * 6.6f);
            float top = i % 2 == 0 ? 4.6f : 3.75f;
            Primitive("Sanctuary Column", PrimitiveType.Cylinder, basePoint + Vector3.up * (top * .5f),
                new Vector3(.7f, top * .5f, .7f), assets.templeStone);
            Primitive("Column Capital", PrimitiveType.Cylinder, basePoint + Vector3.up * top,
                new Vector3(1.1f, .15f, 1.1f), assets.antiqueGold);
        }
        backdropSeal = Part("Sanctuary Halo", sanctuary.transform, ringMesh, ringMaterial);
        backdropSeal.transform.localPosition = new Vector3(0, 3.55f, 3.8f);
        backdropSeal.transform.localScale = Vector3.one * 3.7f;

        crystalMaterial = new Material(assets.pillarShader);
        crystalMaterial.SetVector("_Center", assets.pillarCrystal.bounds.center);
        crystalMaterial.SetVector("_HalfSize", assets.pillarCrystal.bounds.extents);
        pillar = Part("Pillar of Logic", sanctuary.transform, assets.pillarCrystal, crystalMaterial);
        float scale = 5.3f / Mathf.Max(.01f, assets.pillarCrystal.bounds.size.y);
        pillar.transform.localScale = Vector3.one * scale;
        pillar.transform.localPosition = Vector3.up * 3.35f - assets.pillarCrystal.bounds.center * scale;
        innerHoop = Hoop("Lower Seal", 2.15f, 1.92f);
        outerHoop = Hoop("Upper Seal", 4.25f, 2.18f);
        symbols = new GameObject("Discrete Mathematics Orbit").transform;
        symbols.SetParent(sanctuary.transform, false);
        symbols.localPosition = Vector3.up * 3.5f;
        string[] signs = { "P", "Q", "f(x)", "A", "B", "n + 1", "V", "E" };
        for (int i = 0; i < signs.Length; i++)
        {
            var angle = i * Mathf.PI * 2 / signs.Length;
            var sign = new GameObject("Pillar Rune " + signs[i]).AddComponent<TextMeshPro>();
            sign.transform.SetParent(symbols, false);
            sign.transform.localPosition = new Vector3(Mathf.Sin(angle) * 2.85f, i % 2 == 0 ? -.63f : .65f, Mathf.Cos(angle) * 2.85f);
            sign.rectTransform.sizeDelta = new Vector2(2, 1);
            sign.font = StageUITheme.ReadingFont;
            sign.fontSize = 4.6f;
            sign.alignment = TextAlignmentOptions.Center;
            sign.text = signs[i];
        }
        restorationLight = new GameObject("Restored Crystal Light").AddComponent<Light>();
        restorationLight.transform.SetParent(sanctuary.transform, false);
        restorationLight.transform.localPosition = new Vector3(0, 4, -2);
        restorationLight.type = LightType.Point;
        restorationLight.color = Teal;
        restorationLight.range = 10;
        restorationLight.shadows = LightShadows.None;
    }

    public void RestorePillar(float time, float repaired)
    {
        paint.Clear();
        paint.SetFloat(Restored, repaired);
        pillar.SetPropertyBlock(paint);
        restorationLight.intensity = Mathf.Lerp(.6f, 7, repaired);
        PortalPaint(backdropSeal, new Color(.14f, .59f, .71f), .30f * repaired, time);
        float orbit = Mathf.Max(0, time - 4.6f) * Mathf.Lerp(4, 32, repaired);
        innerHoop.localRotation = Quaternion.Euler(11 + orbit * .1f, orbit, 18);
        outerHoop.localRotation = Quaternion.Euler(-16, -orbit, -12);
        symbols.localRotation = Quaternion.Euler(0, orbit, 0);
        var cameraView = Camera.main;
        foreach (Transform child in symbols)
        {
            child.GetComponent<TMP_Text>().color = Color.Lerp(new Color(.43f, .48f, .58f), Gold, repaired);
            if (cameraView != null) child.rotation = Quaternion.LookRotation(cameraView.transform.forward);
        }
        summonFeet[0] = sanctuaryBase + Vector3.up * .82f;
        glyphs.ShowSummon(summonFeet, Mathf.InverseLerp(2.4f, 7.6f, time), Gold);
    }

    Transform Hoop(string name, float height, float radius)
    {
        var hoop = Part(name, sanctuary.transform, assets.pillarHoop, assets.antiqueGold).transform;
        hoop.localPosition = Vector3.up * height;
        hoop.localScale = Vector3.one * (radius / Mathf.Max(.01f, assets.pillarHoop.bounds.extents.x));
        return hoop;
    }

    void Primitive(string name, PrimitiveType kind, Vector3 localPosition, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(kind);
        part.name = name;
        part.transform.SetParent(sanctuary.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        part.GetComponent<Collider>().enabled = false;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Renderer Part(string name, Transform parent, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    static Mesh Ring(string name, float inner, float outer)
    {
        const int segments = 48;
        var vertices = new Vector3[(segments + 1) * 2];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            var point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            vertices[i * 2] = point * inner;
            vertices[i * 2 + 1] = point * outer;
            uv[i * 2] = (Vector2)vertices[i * 2] * .5f + Vector2.one * .5f;
            uv[i * 2 + 1] = (Vector2)vertices[i * 2 + 1] * .5f + Vector2.one * .5f;
            if (i == segments) continue;
            int v = i * 2, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 3;
            triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }
        var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }

    public void Hide()
    {
        gate.SetActive(false);
        glyphs.Hide();
        if (sanctuary != null) sanctuary.SetActive(false);
    }

    public void Dispose()
    {
        glyphs.Dispose();
        Object.Destroy(root);
        Object.Destroy(faceMesh);
        Object.Destroy(ringMesh);
        Object.Destroy(faceMaterial);
        Object.Destroy(ringMaterial);
        if (crystalMaterial != null) Object.Destroy(crystalMaterial);
    }
}
