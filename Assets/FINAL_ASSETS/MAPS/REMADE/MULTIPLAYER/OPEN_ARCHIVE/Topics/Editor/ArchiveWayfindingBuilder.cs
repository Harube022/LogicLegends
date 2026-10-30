using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Creates only owned decorative geometry; never adds collision or changes navigation.</summary>
    public static class ArchiveWayfindingBuilder
    {
        const string Folder = ArchiveTopicSetup.Folder + "/Art/EnvironmentPolish";

        public static ArchiveNestAtmosphere Build(ArchiveTopicSession session, Transform parent, Material stone, Material trim, Material glow, TMP_FontAsset font)
        {
            if (session == null || parent == null) throw new ArgumentNullException("A saved session and decoration parent are required.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var old = parent.Find("Archive_Wayfinding_And_Nest");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Archive_Wayfinding_And_Nest").transform; root.SetParent(parent, false);
            var atmosphere = root.gameObject.AddComponent<ArchiveNestAtmosphere>(); atmosphere.session = session;
            atmosphere.nestAnchor = session.hunting != null ? session.hunting.monsterSpawnPoint : null;
            atmosphere.waymarkRunes = new Renderer[session.stations.Length]; atmosphere.waymarkColors = new Color[session.stations.Length];

            var frame = new Geometry();
            frame.Box(new Vector3(0, 3.7f, 0), new Vector3(4.23f, .13f, .34f));
            frame.Box(new Vector3(0, 2, 0), new Vector3(4.23f, .13f, .34f));
            frame.Box(new Vector3(-2.05f, 2.85f, 0), new Vector3(.13f, 1.7f, .34f));
            frame.Box(new Vector3(2.05f, 2.85f, 0), new Vector3(.13f, 1.7f, .34f));
            Mesh postMesh = SignPostMesh(), frameMesh = Save("Waymark_Brass_Frame", frame);
            var topicPlacement=new ArchiveWayfindingPlacement(GameObject.Find("OpenArchive/Environment").transform);
            for (int i = 0; i < session.stations.Length; i++)
            {
                var station = session.stations[i];
                var marker = new GameObject("T" + (station.topic + 1) + "_Path_Waymark").transform; marker.SetParent(root, false);
                marker.position = topicPlacement.BesideTopic(station);
                marker.rotation = station.transform.rotation; marker.localScale = Vector3.one * 1.2f;
                // Sample close to the authored ground so distant bridge caps and overhead
                // architecture cannot lift an offroad sign above the grass.
                var position = marker.position; position.y = station.transform.position.y + .025f;
                var floors = Physics.RaycastAll(position + Vector3.up * 2, Vector3.down, 8, ~(1 << 7), QueryTriggerInteraction.Ignore)
                    .Where(h => h.normal.y > .75f).OrderBy(h => h.distance).ToArray();
                if (floors.Length > 0) position.y = floors[0].point.y + .025f;
                marker.position = position;
                MeshPart(marker, "Stone_Post", postMesh, stone);
                MeshPart(marker, "Brass_Frame", frameMesh, trim);
                var tint = ArchiveCrystalPillar.Palette[Mathf.Clamp(station.topic, 0, 9)];
                var material = TintedMaterial("Waymark_T" + (station.topic + 1), glow, tint);
                atmosphere.waymarkRunes[i] = MeshPart(marker, "Topic_Glyph_And_Guide", TopicGuideMesh(station,marker), material);
                atmosphere.waymarkColors[i] = tint;
                Label(marker, "Topic_Number_Front", "T" + (station.topic + 1), font, new Vector3(-.95f, 2.84f, -.19f), false);
                Label(marker, "Topic_Number_Back", "T" + (station.topic + 1), font, new Vector3(.95f, 2.84f, .19f), true);
            }
            BuildRouteChoices(root, postMesh, frameMesh, stone, trim, glow, font);

            atmosphere.warningRunes = new Renderer[4]; atmosphere.embers = new Transform[4]; atmosphere.emberRenderers = new Renderer[4];
            atmosphere.chains = new Transform[4]; atmosphere.chainRenderers = new Renderer[4];
            var seal = new Geometry(); seal.Ring(Vector3.zero, 1.15f, .075f, 32, Quaternion.identity);
            seal.Ring(Vector3.zero, .78f, .035f, 24, Quaternion.identity);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                seal.Line(radial * 1.25f, radial * 1.47f, .09f);
            }
            seal.Line(new Vector3(-.38f, 0, -.22f), new Vector3(0, 0, .4f), .055f);
            seal.Line(new Vector3(0, 0, .4f), new Vector3(.38f, 0, -.22f), .055f);
            seal.Line(new Vector3(.38f, 0, -.22f), new Vector3(-.38f, 0, -.22f), .055f);
            var chain = new Geometry();
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * .29f;
                chain.Ring(new Vector3(x, -.18f * (1 - Mathf.Abs(i - 3) / 3f), 0), .20f, .055f, 12,
                    Quaternion.Euler(i % 2 == 0 ? 90 : 0, 0, 0));
            }
            var embers = new Geometry();
            for (int i = 0; i < 5; i++)
            {
                float a = i * 2.399963f;
                embers.Octahedron(new Vector3(Mathf.Cos(a) * .65f, .6f + (i % 3) * .33f, Mathf.Sin(a) * .65f), .065f);
            }
            var sealMesh = Save("Nest_Warning_Seal", seal); var chainMesh = Save("Nest_Chain_Ornament", chain); var emberMesh = Save("Nest_Ember_Cluster", embers);
            var center = atmosphere.nestAnchor != null ? atmosphere.nestAnchor.position : Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                float angle = (45 + i * 90) * Mathf.Deg2Rad;
                var sector = new GameObject("Nest_Diagonal_Warning_" + i).transform; sector.SetParent(root, false);
                sector.position = center + new Vector3(Mathf.Cos(angle) * 22, .16f, Mathf.Sin(angle) * 22);
                sector.rotation = Quaternion.Euler(0, -45 - i * 90, 0);
                atmosphere.warningRunes[i] = MeshPart(sector, "Warning_Rune", sealMesh, glow);
                atmosphere.emberRenderers[i] = MeshPart(sector, "Warning_Embers", emberMesh, glow); atmosphere.embers[i] = atmosphere.emberRenderers[i].transform;
                var chainRenderer = MeshPart(sector, "Chain_Ornament", chainMesh, trim);
                chainRenderer.transform.localPosition = new Vector3(0, .32f, 1.8f);
                atmosphere.chainRenderers[i] = chainRenderer; atmosphere.chains[i] = chainRenderer.transform;
            }
            EditorUtility.SetDirty(atmosphere); return atmosphere;
        }
        static void BuildRouteChoices(Transform parent, Mesh postMesh, Mesh frameMesh, Material stone, Material trim, Material glow, TMP_FontAsset font)
        {
            var map = GameObject.Find("OpenArchive");
            var bridges = map != null ? map.transform.Find("Environment/Bridges") : null;
            if (bridges == null) return;
            var placement=new ArchiveWayfindingPlacement(map.transform.Find("Environment"));
            var arrows = new Geometry();
            foreach (float z in new[] { -.19f, .19f })
            {
                // With this board turned 90 degrees to the bridge, local +X points
                // inward along its deck and local -X points toward the outer topic loop.
                arrows.Line(new Vector3(1.3f, 3.2f, z), new Vector3(1.85f, 3.2f, z), .07f);
                arrows.Line(new Vector3(1.85f, 3.2f, z), new Vector3(1.62f, 3.4f, z), .07f);
                arrows.Line(new Vector3(1.85f, 3.2f, z), new Vector3(1.62f, 3, z), .07f);
                arrows.Line(new Vector3(-1.3f, 2.5f, z), new Vector3(-1.85f, 2.5f, z), .07f);
                arrows.Line(new Vector3(-1.85f, 2.5f, z), new Vector3(-1.62f, 2.7f, z), .07f);
                arrows.Line(new Vector3(-1.85f, 2.5f, z), new Vector3(-1.62f, 2.3f, z), .07f);
            }
            var glyphMesh = Save("Waymark_Route_Choice_Arrows", arrows);
            var material = TintedMaterial("Waymark_Route_Teal", glow, new Color(.12f, .74f, .69f));
            var readingMaterial=TintedMaterial("Waymark_Route_ReadingFace",glow,new Color(.035f,.10f,.095f));
            var face=new Geometry();face.Box(new Vector3(0,2.85f,-.137f),new Vector3(3.9f,1.43f,.02f));face.Box(new Vector3(0,2.85f,.137f),new Vector3(3.9f,1.43f,.02f));
            var faceMesh=Save("Waymark_Route_ReadingFaces",face);
            int index = 0;
            foreach (Transform bridge in bridges)
            {
                if (!bridge.name.StartsWith("Bridge_Modular_", StringComparison.Ordinal)) continue;
                var point=placement.BesideBridge(bridge);
                var sign = new GameObject("Bridge_" + (++index) + "_Route_Choice").transform; sign.SetParent(parent, false);
                sign.position = point; sign.rotation = bridge.rotation * Quaternion.Euler(0, 90, 0); sign.localScale = Vector3.one * 1.2f;
                MeshPart(sign, "Stone_Post", postMesh, stone); MeshPart(sign, "Brass_Frame", frameMesh, trim);
                MeshPart(sign,"Readable_Direction_Faces",faceMesh,readingMaterial);
                MeshPart(sign, "Inward_Bridge_And_Outer_Loop_Arrows", glyphMesh, material);
                RouteLabel(sign, font, false); RouteLabel(sign, font, true);
            }
        }
        static void RouteLabel(Transform parent, TMP_FontAsset font, bool back)
        {
            for(int row=0;row<2;row++)
            {
                var text = new GameObject((back ? "Route_Name_Back_" : "Route_Name_Front_")+row).AddComponent<TextMeshPro>();
                text.transform.SetParent(parent, false); text.transform.localPosition = new Vector3(0,row==0?3.2f:2.5f,back ? .19f : -.19f);
                text.transform.localRotation = Quaternion.Euler(0, back ? 180 : 0, 0);
                text.font = font; text.text = row==0?"BRIDGE / NEST":"TOPIC LOOP"; text.fontSize = 3.6f; text.enableAutoSizing = true;
                text.fontSizeMin = 2.2f; text.fontSizeMax = 3.6f; text.rectTransform.sizeDelta = new Vector2(2.4f,.55f);
                text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;
                text.alignment = TextAlignmentOptions.Center; text.color = new Color(.98f, .94f, .78f);
                text.richText = false; text.raycastTarget = false;
                var renderer = text.GetComponent<Renderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
        }
        public static void RefreshRouteSigns()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Refresh signs in stopped Edit Mode.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            var root=GameObject.Find(ArchiveEnvironmentPolishSetup.RootName+"/Archive_Wayfinding_And_Nest").transform;
            foreach(var old in root.Cast<Transform>().Where(t=>t.name.StartsWith("Bridge_",StringComparison.Ordinal)&&t.name.EndsWith("_Route_Choice",StringComparison.Ordinal)).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            var stone=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_Stone.mat");
            var trim=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_BrassInlay.mat");
            var glow=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Archive_Environment_Rune.mat");
            var placement=new ArchiveWayfindingPlacement(GameObject.Find("OpenArchive/Environment").transform);
            foreach(var station in session.stations)
            {
                var marker=root.Find("T"+(station.topic+1)+"_Path_Waymark");
                if(marker!=null)
                {
                    marker.position=placement.BesideTopic(station);
                    marker.Find("Topic_Glyph_And_Guide").GetComponent<MeshFilter>().sharedMesh=TopicGuideMesh(station,marker);
                }
            }
            BuildRouteChoices(root,SignPostMesh(),AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Waymark_Brass_Frame.asset"),stone,trim,glow,session.stations[0].font);
            AssetDatabase.SaveAssets();
        }
        static Mesh SignPostMesh()
        {
            var post=new Geometry();
            post.Box(new Vector3(0,.13f,0),new Vector3(1.45f,.26f,1.1f));
            // The stem ends underneath the board; it must never obscure the lower row.
            post.Box(new Vector3(0,1.05f,0),new Vector3(.38f,1.9f,.38f));
            post.Box(new Vector3(0,2.85f,0),new Vector3(4.05f,1.65f,.24f));
            return Save("Waymark_Stone",post);
        }
        static Mesh TopicGuideMesh(ArchiveTopicStation station,Transform marker)
        {
            var glyph=new Geometry();DrawTopicGlyph(glyph,station.topic,false);DrawTopicGlyph(glyph,station.topic,true);
            var direction=marker.InverseTransformDirection(station.transform.position-marker.position);
            direction.y=0;direction.Normalize();var side=Vector3.Cross(Vector3.up,direction);
            var tip=direction*1.65f+Vector3.up*.055f;
            glyph.Line(tip,tip-direction*.55f+side*.30f,.075f);
            glyph.Line(tip,tip-direction*.55f-side*.30f,.075f);
            return Save("Waymark_Glyph_T"+(station.topic+1),glyph);
        }
        static void Label(Transform parent, string name, string value, TMP_FontAsset font, Vector3 position, bool back)
        {
            var text = new GameObject(name).AddComponent<TextMeshPro>(); text.transform.SetParent(parent, false);
            text.transform.localPosition = position; text.transform.localRotation = Quaternion.Euler(0, back ? 180 : 0, 0);
            text.text = value; text.font = font; text.fontSize = 11; text.enableAutoSizing = true; text.fontSizeMin = 7; text.fontSizeMax = 11;
            text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(1.65f, 1.2f);
            text.color = new Color(.98f, .94f, .78f); text.richText = false; text.raycastTarget = false;
            var renderer = text.GetComponent<Renderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        static void DrawTopicGlyph(Geometry geometry, int topic, bool back)
        {
            float z = back ? .19f : -.19f;
            Vector3 P(float x, float y) => new Vector3((back ? -1 : 1) * (.85f + x * .62f), 2.85f + y * .62f, z);
            void L(float x, float y, float x2, float y2) => geometry.Line(P(x, y), P(x2, y2), .075f);
            void Dot(float x, float y) => geometry.Octahedron(P(x, y), .12f);
            void Ring(float x, float y, float radius) => geometry.Ring(P(x, y), radius * .62f, .055f, 20, Quaternion.Euler(90, 0, 0));
            switch (topic)
            {
                case 0: L(-.65f,-.55f,0,.55f);L(0,.55f,.65f,-.55f);break;
                case 1:
                    for(int n=0;n<3;n++){float v=-.6f+n*.6f;L(v,-.6f,v,.6f);L(-.6f,v,.6f,v);}Dot(-.3f,.3f);Dot(.3f,-.3f);break;
                case 2: Dot(-.6f,.5f);Dot(-.6f,-.5f);Dot(.6f,0);L(-.4f,.4f,.45f,.08f);L(-.4f,-.4f,.45f,-.08f);break;
                case 3: L(-.65f,-.55f,-.65f,0);L(-.65f,0,0,0);L(0,0,0,.55f);L(0,.55f,.65f,.55f);break;
                case 4: L(0,-.6f,0,0);L(0,0,-.65f,.6f);L(0,0,.65f,.6f);Dot(-.65f,.6f);Dot(.65f,.6f);break;
                case 5: L(-.7f,-.55f,-.2f,-.55f);L(-.2f,-.55f,-.2f,0);L(-.2f,0,.3f,0);L(.3f,0,.3f,.55f);L(.3f,.55f,.75f,.55f);break;
                case 6: Ring(-.32f,0,.62f);Ring(.32f,0,.62f);break;
                case 7: Dot(-.65f,.5f);Dot(-.65f,-.5f);Dot(.65f,.5f);Dot(.65f,-.5f);L(-.5f,.5f,.5f,-.5f);L(-.5f,-.5f,.5f,.5f);break;
                case 8: Dot(-.6f,-.5f);Dot(.6f,-.5f);Dot(0,.6f);L(-.6f,-.5f,.6f,-.5f);L(-.6f,-.5f,0,.6f);L(.6f,-.5f,0,.6f);break;
                default: Dot(0,.6f);Dot(-.6f,0);Dot(.6f,0);Dot(-.8f,-.6f);Dot(.8f,-.6f);L(0,.6f,-.6f,0);L(0,.6f,.6f,0);L(-.6f,0,-.8f,-.6f);L(.6f,0,.8f,-.6f);break;
            }
        }
        static Renderer MeshPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; return renderer;
        }
        static Material TintedMaterial(string name, Material source, Color tint)
        {
            string path = Folder + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source) { name = name, enableInstancing = true }; AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", tint); material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", tint * .18f); EditorUtility.SetDirty(material); return material;
        }
        static Mesh Save(string name, Geometry geometry)
        {
            string path = Folder + "/" + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.SetVertices(geometry.vertices); mesh.SetTriangles(geometry.indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // The stone/trim materials may already contain texture maps. Face-local box
            // projection keeps those textures legible instead of sampling one UV pixel.
            var normals = mesh.normals; var uv = new List<Vector2>(geometry.vertices.Count);
            for (int i = 0; i < geometry.vertices.Count; i++)
            {
                var p = geometry.vertices[i]; var n = normals[i];
                var axes = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                uv.Add((axes.y >= axes.x && axes.y >= axes.z ? new Vector2(p.x, p.z) :
                    axes.x >= axes.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) * .4f);
            }
            mesh.SetUVs(0, uv);
            EditorUtility.SetDirty(mesh); return mesh;
        }
        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>(); public readonly List<int> indices = new List<int>();
            void Face(Vector3 a, Vector3 b, Vector3 c) { int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); indices.Add(n); indices.Add(n + 1); indices.Add(n + 2); }
            public void Box(Vector3 center, Vector3 size, Quaternion rotation = default)
            {
                if (rotation.Equals(default(Quaternion))) rotation = Quaternion.identity;
                var corners = new Vector3[8];
                for (int i = 0; i < 8; i++) corners[i] = center + rotation * Vector3.Scale(new Vector3((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f), size);
                int[] faces = {0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};
                for(int i=0;i<faces.Length;i+=3)Face(corners[faces[i]],corners[faces[i+1]],corners[faces[i+2]]);
            }
            public void Line(Vector3 from, Vector3 to, float width)
            {
                var d = to - from; if (d.sqrMagnitude < .000001f) return;
                Box((from + to) * .5f, new Vector3(width, width, d.magnitude + width * .5f), Quaternion.LookRotation(d));
            }
            public void Octahedron(Vector3 p, float size)
            {
                var r = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
                for (int i = 0; i < 4; i++) { Face(p + Vector3.up * size, p + r[i] * size, p + r[(i+1)%4] * size); Face(p - Vector3.up * size, p + r[(i+1)%4] * size, p + r[i] * size); }
            }
            public void Ring(Vector3 p, float radius, float thickness, int segments, Quaternion rotation)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                    var start = p + rotation * new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                    var end = p + rotation * new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius);
                    Line(start, end, thickness);
                }
            }
        }
    }
}
