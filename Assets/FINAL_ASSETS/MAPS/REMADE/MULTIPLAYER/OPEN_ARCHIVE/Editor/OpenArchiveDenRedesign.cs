using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    /// <summary>Art pass on the saved island. Does not generate or replace the map.</summary>
    public static class OpenArchiveDenRedesign
    {
        public const string Folder = OpenArchiveBuilder.Folder;
        public const float SleepingRadius = 8.5f;
        public const float SleepingY = .25f;
        static Transform root, nest;
        static Material stone, atlas, water;
        static Vector3[] slots;
        static System.Random rng;
        static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static Vector3 Polar(float a, float r, float y = 0) => Quaternion.Euler(0, a, 0) * new Vector3(0, y, r);
        static readonly Color Rock = new Color(.40f, .44f, .43f);
        static readonly Color RockLight = new Color(.56f, .58f, .53f);
        static readonly Color RockDark = new Color(.27f, .31f, .31f);
        static readonly Color Moss = new Color(.23f, .39f, .14f);
        static readonly Color Iron = new Color(.19f, .24f, .25f);

        [Serializable] public class Baseline
        {
            public string[] objectIDs, protectedMeshPaths, protectedMeshHashes, spawnTransforms, slotTransforms, assetGUIDs;
            public string generatedUtc;
        }

        [MenuItem("Tools/Logic Legends/Open Archive/Redesign Existing Nest And Island Art")]
        public static void Apply()
        {
            OpenArchiveImprovements.RequireSavedEditor();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != OpenArchiveBuilder.ScenePath) scene = EditorSceneManager.OpenScene(OpenArchiveBuilder.ScenePath, OpenSceneMode.Single);
            root = scene.GetRootGameObjects().Single(g => g.name == "OpenArchive").transform;
            if (root.position != Vector3.zero || root.rotation != Quaternion.identity || root.lossyScale != Vector3.one)
                throw new InvalidOperationException("Refine the unit-scale source scene. The independently scaled exploration copy is synchronized afterward.");
            nest = root.Find("UniversalMonsterNest");
            slots = root.Find("FutureStructureSlots").Cast<Transform>().Select(t => t.position).ToArray();
            SaveBaseline();
            stone = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_Meadow.mat");
            atlas = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_Atlas.mat");
            water = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Archive_TurquoiseWater.mat");
            rng = new System.Random(166207);
            MakeModules();
            SleepingHollow();
            RuinedGateways();
            RuggedRim();
            ScratchesRubbleAndRestraints();
            IslandLandmarks();
            ClearingsAndGround();
            WaterAndBridges();
            Atmosphere();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the redesigned environment.");
            AssetDatabase.SaveAssets();
            Debug.Log("Existing Open Archive art redesigned. Sleeping floor Y0.25, clear radius8.5; all four ten-metre entrance ramps retained.");
        }

        static void SaveBaseline()
        {
            string file = Folder + "/Editor/Authoring/DenRedesignBaseline.json";
            if (File.Exists(file)) return;
            var protectedMeshes = root.GetComponentsInChildren<MeshFilter>(true).Where(f =>
            {
                string p = AnimationUtility.CalculateTransformPath(f.transform, root);
                return p.StartsWith("Environment/Terrain/") || p.StartsWith("Environment/MainPaths/") || p.StartsWith("Environment/SecondaryPaths/")
                    || p.StartsWith("Environment/Bridges/") || p.StartsWith("Environment/Rivers/") || p.StartsWith("Environment/Ponds/") || p.StartsWith("Environment/Cliffs/");
            }).ToArray();
            var baseline = new Baseline
            {
                generatedUtc = DateTime.UtcNow.ToString("o"),
                objectIDs = root.GetComponentsInChildren<Transform>(true).Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()).ToArray(),
                protectedMeshPaths = protectedMeshes.Select(f => AnimationUtility.CalculateTransformPath(f.transform, root)).ToArray(),
                protectedMeshHashes = protectedMeshes.Select(f => GeometryHash(f.sharedMesh)).ToArray(),
                spawnTransforms = root.Find("PlayerSpawnArea").GetComponentsInChildren<Transform>(true).Select(TransformStamp).ToArray(),
                slotTransforms = root.Find("FutureStructureSlots").GetComponentsInChildren<Transform>(true).Select(TransformStamp).ToArray(),
                assetGUIDs = AssetDatabase.FindAssets("", new[] { Folder }).Select(g => g + "|" + AssetDatabase.GUIDToAssetPath(g)).ToArray()
            };
            File.WriteAllText(file, JsonUtility.ToJson(baseline, true)); AssetDatabase.ImportAsset(file);
        }

        public static string TransformStamp(Transform t) => GlobalObjectId.GetGlobalObjectIdSlow(t) + "|" + t.localPosition.ToString("R") + "|" + t.localRotation.ToString("R") + "|" + t.localScale.ToString("R");
        public static string GeometryHash(Mesh mesh)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                foreach (var v in mesh.vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                foreach (var i in mesh.triangles) writer.Write(i);
                writer.Flush(); using (var hash = SHA256.Create()) return string.Concat(hash.ComputeHash(stream.ToArray()).Select(b => b.ToString("x2")));
            }
        }

        public static float FloorY(Vector3 p)
        {
            float r = new Vector2(p.x, p.z).magnitude;
            return Mathf.Lerp(SleepingY, .8f, Mathf.InverseLerp(SleepingRadius, 12.5f, r));
        }

        static void MakeModules()
        {
            SaveMesh("Den_Boulder_A", Boulder(731, 1.9f, 2.8f, 1.6f));
            SaveMesh("Den_Boulder_B", Boulder(991, 1.4f, 2.0f, 1.8f));
            SaveMesh("Den_Broken_Pillar", Pillar(114, 9.2f));
            SaveMesh("Den_Broken_Gateway_A", Gateway(42));
            SaveMesh("Den_Broken_Gateway_B", Gateway(76));
            var scribe = new Geometry();
            scribe.Box(new Vector3(0,.25f,0), new Vector3(3.6f,.5f,3.1f), RockDark);
            scribe.Box(new Vector3(0,.65f,0), new Vector3(2.6f,.3f,2.4f), RockLight);
            scribe.Column(new Vector3(0,.8f,0), 4.8f, 1.05f, .72f, 6, Rock);
            scribe.Rock(new Vector3(0,5.4f,0), new Vector3(.95f,1.5f,.8f), 38, RockLight);
            scribe.Box(new Vector3(0,3.4f,-.85f), new Vector3(1.9f,.35f,1.1f), new Color(.36f,.39f,.36f));
            scribe.Box(new Vector3(-.35f,3.64f,-.95f), new Vector3(.65f,.13f,.85f), new Color(.63f,.60f,.44f));
            scribe.Box(new Vector3(.35f,3.64f,-.95f), new Vector3(.65f,.13f,.85f), new Color(.63f,.60f,.44f));
            scribe.Box(new Vector3(0,5.55f,-.68f), new Vector3(.42f,.55f,.06f), RockDark);
            scribe.MossPatch(new Vector3(.4f,.84f,-.4f), 1.0f, 4);
            SaveMesh("Den_Ancient_Scribe_Statue", scribe);
            foreach (string name in new[] { "Den_Boulder_A", "Den_Boulder_B", "Den_Broken_Pillar", "Den_Broken_Gateway_A", "Den_Broken_Gateway_B", "Den_Ancient_Scribe_Statue" })
            {
                string path = Folder + "/Prefabs/Architecture/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var temporary = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                temporary.GetComponent<MeshFilter>().sharedMesh = MeshAsset(name); temporary.GetComponent<MeshRenderer>().sharedMaterial = stone;
                PrefabUtility.SaveAsPrefabAsset(temporary, path); Object.DestroyImmediate(temporary);
            }
        }

        static Geometry Boulder(int seed, float x, float y, float z)
        {
            var g = new Geometry(); g.Rock(Vector3.zero, new Vector3(x,y,z), seed, Rock); return g;
        }

        static Geometry Pillar(int seed, float height)
        {
            var g = new Geometry();
            g.Rock(Vector3.zero, new Vector3(1.65f,.55f,1.5f), seed, RockDark);
            g.Box(new Vector3(0,.55f,0), new Vector3(2.5f,.45f,2.4f), Rock);
            g.FracturedColumn(new Vector3(0,.77f,0),height-.8f,1.12f,seed,RockLight);
            for (int i=0;i<5;i++) g.MossPatch(new Vector3(Mathf.Sin(i*2.4f)*.7f,.82f+i*.035f,Mathf.Cos(i*2.4f)*.8f), .8f, seed+i);
            // Dark fracture running down the front, inset into the pillar surface.
            g.Stroke(new Vector3(-.3f,height-1.3f,-.96f), new Vector3(.18f,height*.54f,-1.05f), .07f, RockDark, Vector3.forward);
            return g;
        }

        static Geometry Gateway(int seed)
        {
            var g = new Geometry(); var random = new System.Random(seed);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 6.35f;
                g.Box(new Vector3(x,.24f,0), new Vector3(2.2f,.48f,2.5f), RockDark);
                g.FracturedColumn(new Vector3(x,.48f,0),7.45f,.97f,seed+side,Rock);
                g.Rock(new Vector3(x,7.85f,0), new Vector3(1.2f,.75f,1.15f), seed+side+3, RockLight);
                g.MossPatch(new Vector3(x,.50f,-.4f), .96f, seed+side);
                for (int j=0;j<3;j++)
                {
                    Vector3 a = new Vector3(x-.5f+j*.27f,4.7f-j*.35f,-.98f), b = a + new Vector3(.38f,-1.4f,0);
                    g.Stroke(a,b,.055f,RockDark,Vector3.forward);
                }
            }
            // Missing voussoirs and chipped caps preserve a broad opening below the broken arch.
            const int pieces = 15;
            for (int i = 0; i < pieces; i++)
            {
                if (i == (seed % 2 == 0 ? 6 : 9)) continue;
                float a = i * Mathf.PI / pieces, b = (i+1) * Mathf.PI / pieces;
                float jitter = (float)random.NextDouble() * .12f;
                Vector3 ia = new Vector3(Mathf.Cos(a)*5.65f,7.75f+Mathf.Sin(a)*3.2f+jitter,0);
                Vector3 ib = new Vector3(Mathf.Cos(b)*5.65f,7.75f+Mathf.Sin(b)*3.2f+jitter,0);
                Vector3 oa = new Vector3(Mathf.Cos(a)*6.95f,8.1f+Mathf.Sin(a)*4.05f+jitter,0);
                Vector3 ob = new Vector3(Mathf.Cos(b)*6.95f,8.1f+Mathf.Sin(b)*4.05f+jitter,0);
                g.Prism(ia,ib,ob,oa, .92f, Color.Lerp(Rock,RockLight,(float)random.NextDouble()*.75f));
                if (i % 4 == 0) g.MossPatch((oa+ob)*.5f + Vector3.back*.35f, .40f, i+seed);
            }
            return g;
        }

        static Color Dirt(Vector3 p)
        {
            float noise = Mathf.PerlinNoise(p.x*.29f+16,p.z*.29f+93);
            float r = new Vector2(p.x,p.z).magnitude;
            Color center = Color.Lerp(new Color(.34f,.275f,.18f),new Color(.48f,.39f,.245f),noise);
            return Color.Lerp(center,new Color(.47f,.46f,.32f),Mathf.Clamp01((r-9)/6)*.6f);
        }

        static void SleepingHollow()
        {
            var floor = new Geometry();
            float[] radii = { 0,3,6,8.5f,9.5f,10.5f,11.5f,12.5f,13.5f,15 };
            for(int band=0;band<radii.Length-1;band++) for(int i=0;i<96;i++)
            {
                Vector3 a=Polar(i*3.75f,radii[band]), b=Polar(i*3.75f,radii[band+1]), c=Polar((i+1)*3.75f,radii[band+1]), d=Polar((i+1)*3.75f,radii[band]);
                a.y=FloorY(a);b.y=FloorY(b);c.y=FloorY(c);d.y=FloorY(d);
                floor.QuadTinted(a,b,c,d,Dirt);
            }
            Update(nest.Find("Nest_StoneFloor_30m").GetComponent<MeshFilter>(),floor,stone);
            nest.Find("MonsterSpawnPoint").localPosition = new Vector3(0,SleepingY,0);

            var joints = new Geometry(); var landings = new Geometry();
            string[] directions={"North","South","East","West"};
            for(int i=0;i<4;i++)
            {
                Vector3 dir = i==0?Vector3.forward:i==1?Vector3.back:i==2?Vector3.right:Vector3.left;
                Vector3 side=Vector3.Cross(Vector3.up,dir);
                var ramp=new Geometry();
                for(int along=0;along<8;along++) for(int lane=0;lane<5;lane++)
                {
                    Func<float,float,Vector3> p=(r,x)=>dir*r+side*x+Vector3.up*(.8f*(23-r)/8);
                    Color color=Color.Lerp(new Color(.50f,.48f,.38f),new Color(.37f,.35f,.26f),(float)rng.NextDouble()*.65f);
                    ramp.Quad(p(15+along,-5+lane*2),p(16+along,-5+lane*2),p(16+along,-3+lane*2),p(15+along,-3+lane*2),color);
                    if((along+lane)%3==0)
                    {
                        Vector3 center=p(15.6f+along,-4.1f+lane*2)+Vector3.up*.014f;
                        joints.Tile(center,1.35f, .7f, Quaternion.LookRotation(dir-Vector3.up*.1f), RockLight);
                        joints.Stroke(center-side*.5f,center+side*.35f+dir*.2f,.025f,RockDark,Vector3.up);
                    }
                }
                Update(nest.Find("Ramp_"+directions[i]+"_Clear10m/RampSurface_"+directions[i]).GetComponent<MeshFilter>(),ramp,stone);
                landings.Quad(dir*12.5f-side*5+Vector3.up*.8f,dir*15-side*5+Vector3.up*.8f,dir*15+side*5+Vector3.up*.8f,dir*12.5f+side*5+Vector3.up*.8f,new Color(.47f,.45f,.34f));
            }
            Update(nest.Find("Four_Ramp_StoneJoints").GetComponent<MeshFilter>(),joints,stone);
            Update(nest.Find("Nest_Four_Wide_Landings").GetComponent<MeshFilter>(),landings,stone);
        }

        static void RuinedGateways()
        {
            int gate=0;
            foreach(var t in nest.Cast<Transform>().Where(t=>t.name=="Arch_Wide"))
            {
                t.GetComponent<MeshFilter>().sharedMesh=MeshAsset(gate++%2==0?"Den_Broken_Gateway_A":"Den_Broken_Gateway_B");
                t.GetComponent<MeshRenderer>().sharedMaterial=stone;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<MeshFilter>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<MeshRenderer>());
            }
            int pillar=0;
            foreach(var t in nest.Cast<Transform>().Where(t=>t.name=="Pillar_Broken"))
            {
                t.GetComponent<MeshFilter>().sharedMesh=MeshAsset("Den_Broken_Pillar");t.GetComponent<MeshRenderer>().sharedMaterial=stone;
                t.localScale=new Vector3(1, .88f+pillar++*.07f,1);
                var collider=t.GetComponent<BoxCollider>();collider.center=new Vector3(0,4.6f,0);collider.size=new Vector3(2.15f,9.2f,2.15f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<MeshFilter>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<MeshRenderer>());PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            // Recolor only nest instances; reusable spawn and northern gateway assets remain intact.
            var wornWall = new Geometry();
            wornWall.Rock(new Vector3(0,0,0),new Vector3(1.45f,1.4f,.65f),72,Rock);
            wornWall.MossPatch(new Vector3(.1f,1.1f,.12f),.9f,16);
            SaveMesh("Den_Worn_Wall_Fragment",wornWall);
            foreach(var t in nest.Cast<Transform>().Where(t=>t.name=="RuinedWall_Short"))
            {
                t.GetComponent<MeshFilter>().sharedMesh=MeshAsset("Den_Worn_Wall_Fragment");t.GetComponent<MeshRenderer>().sharedMaterial=stone;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<MeshFilter>());PrefabUtility.RecordPrefabInstancePropertyModifications(t.GetComponent<MeshRenderer>());
            }
        }

        static bool GateClear(Vector3 p,float radius=0) => Mathf.Abs(p.x)>5.35f+radius && Mathf.Abs(p.z)>5.35f+radius;
        static void RuggedRim()
        {
            var wall=new Geometry();
            for(int quadrant=0;quadrant<4;quadrant++) for(int i=0;i<20;i++)
            {
                float a=quadrant*90+22.3f+i*45.4f/20,b=quadrant*90+22.3f+(i+1)*45.4f/20;
                float outer=15.0f+.15f*Mathf.Sin(i*1.7f), inner=14.05f;
                float h=2.1f+.35f*Mathf.Sin(i*1.8f+quadrant);
                Vector3 ia=Polar(a,inner),ib=Polar(b,inner),oa=Polar(a,outer),ob=Polar(b,outer),up=Vector3.up*h;
                wall.Quad(ia+up,oa+up,ob+up,ib+up,RockLight);wall.Quad(ia,ia+up,ib+up,ib,RockDark);wall.Quad(ob,ob+up,oa+up,oa,Rock);
                if(i==0)wall.Quad(oa,oa+up,ia+up,ia,Rock);if(i==19)wall.Quad(ib,ib+up,ob+up,ob,Rock);
            }
            Update(nest.Find("Nest_Continuous_Quarter_Wall_Plinths").GetComponent<MeshFilter>(),wall,stone);
            var rocks=new Geometry();var moss=new Geometry();
            for(int quadrant=0;quadrant<4;quadrant++) for(int i=0;i<4;i++)
            {
                float a=quadrant*90+31+i*9.3f+Rand(-1.1f,1.1f);
                Vector3 p=Polar(a,Rand(13.5f,14.5f),.5f);
                Vector3 size=new Vector3(Rand(1.15f,1.65f),Rand(1.8f,3.1f),Rand(.95f,1.4f));
                if(!GateClear(p,Mathf.Max(size.x,size.z)))continue;
                rocks.Rock(p,size,quadrant*41+i+6,Color.Lerp(Rock,RockDark,(float)rng.NextDouble()*.45f));
                moss.MossPatch(p+Vector3.up*size.y*.8f,size.x*.72f,quadrant*13+i);
            }
            Put("Den_Irregular_Rock_Rim",nest,rocks,stone,true);
            Put("Den_Rim_Moss",nest,moss,stone,false);
        }

        static void ScratchesRubbleAndRestraints()
        {
            var detail=new Geometry();var marks=new Geometry();var chains=new Geometry();var runes=new Geometry();
            var bed=new Geometry();
            for(int ring=0;ring<4;ring++)for(int i=0;i<48;i++)
            {
                Func<int,int,Vector3> point=(n,r)=>
                {
                    float a=n*360f/48,boundary=6.2f+.6f*Mathf.Sin(a*Mathf.Deg2Rad*3)+.45f*Mathf.Cos(a*Mathf.Deg2Rad*5);
                    Vector3 v=Polar(a,boundary*r/4f);v.x*=1.05f;v.z*=.91f;v+=new Vector3(-.6f,0,.3f);v.y=FloorY(v)+.004f;return v;
                };
                Func<Vector3,Color> tint=p=>Color.Lerp(new Color(.30f,.275f,.185f),Dirt(p),Mathf.Clamp01(new Vector2(p.x+.6f,p.z-.3f).magnitude/6.5f));
                bed.QuadTinted(point(i,ring),point(i,ring+1),point(i+1,ring+1),point(i+1,ring),tint);
            }
            Put("Den_Compacted_Sleeping_Bed",nest,bed,stone,false);
            // Flat buried fragments and gouges never consume the sleeping animation volume.
            foreach(Vector3 p0 in new[]{new Vector3(-4,0,4),new Vector3(3,0,-4),new Vector3(-6,0,-1)})
            {
                Vector3 p=p0;p.y=FloorY(p)+.007f;detail.Tile(p,Rand(1.7f,2.8f),Rand(1.4f,2.1f),Quaternion.Euler(0,Rand(0,180),0),new Color(.43f,.43f,.36f));
                for(int j=0;j<3;j++)
                {
                    Vector3 a=p+new Vector3(-.5f+j*.3f,.005f,-.6f),b=a+new Vector3(.45f,0,1.25f);
                    marks.Stroke(a,b,.035f,RockDark,Vector3.up);
                    marks.Stroke(a+Vector3.right*.055f,b+Vector3.right*.045f,.018f,new Color(.61f,.56f,.40f),Vector3.up);
                }
            }
            for(int quadrant=0;quadrant<4;quadrant++)
            {
                float a=quadrant*90+45;
                for(int i=0;i<12;i++)
                {
                    Vector3 p=Polar(a+Rand(-11,11),Rand(10.5f,13.6f));
                    if(!GateClear(p,.55f))continue;p.y=FloorY(p);
                    detail.Rock(p,new Vector3(Rand(.16f,.55f),Rand(.1f,.35f),Rand(.2f,.6f)),i+quadrant*30,RockLight);
                    if(i%4==0)detail.MossPatch(p+Vector3.up*.015f,.65f,i+51);
                }
                Vector3 rune=Polar(a,10.5f);rune.y=FloorY(rune)+.018f;
                for(int j=0;j<3;j++)
                {
                    Vector3 offset=Polar(a+90,j*.48f-.45f);Vector3 p=rune+offset;
                    runes.Stroke(p-Polar(a,.42f),p+Polar(a,.42f),.042f,Color.white,Vector3.up,32);
                    if(j!=1)runes.Stroke(p,p+Polar(a+90,.25f)+Polar(a,.2f),.035f,Color.white,Vector3.up,32);
                }
                if(quadrant==1)continue;
                Vector3 start=Polar(a-5,13.5f,2.55f),end=Polar(a+5,10.7f);end.y=FloorY(end)+.19f;
                for(int link=0;link<10;link++)
                {
                    float t=link/9f;Vector3 p=Vector3.Lerp(start,end,t);p.y-=Mathf.Sin(t*Mathf.PI)*.43f;
                    Quaternion q=Quaternion.FromToRotation(Vector3.up,(end-start).normalized)*Quaternion.Euler(0,link%2*90,0);
                    chains.Link(p,q,.30f,.47f,.09f,link==9,Iron);
                }
                chains.Link(start,Quaternion.Euler(90,0,a),.65f,.58f,.15f,false,Iron);
            }
            Update(nest.Find("Ancient_Gold_Rune").GetComponent<MeshFilter>(),runes,atlas);
            Put("Den_Buried_Tiles_And_Rubble",nest,detail,stone,false);
            Put("Den_Carved_Claw_Scars",nest,marks,stone,false);
            Put("Den_Broken_Restraint_Chains",nest,chains,stone,false);
        }

        static bool Protected(Vector3 p,float margin)
        {
            float r=new Vector2(p.x,p.z).magnitude;
            if(r<28+margin||r>85-margin&&r<94+margin||r>99-margin)return true;
            if(r>48-margin&&r<60+margin||Mathf.Abs(p.x)<6+margin||Mathf.Abs(p.z)<6+margin)return true;
            if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(0,-83))<11+margin)return true;
            foreach(var s in slots)
            {
                if(Mathf.Abs(p.x-s.x)<13+margin&&Mathf.Abs(p.z-s.z)<13+margin)return true;
                Vector3 a=s.normalized*54,d=s-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude);
                if(Vector3.Distance(p,a+d*t)<3.5f+margin)return true;
            }
            for(int i=0;i<4;i++)
            {
                Vector3 d=Polar(45+i*90,1),side=new Vector3(d.z,0,-d.x);float along=Vector3.Dot(p,d);
                if(along>20-margin&&along<51+margin&&Mathf.Abs(Vector3.Dot(p,side))<5.5f+margin)return true;
            }
            return false;
        }

        static void IslandLandmarks()
        {
            var ruins=root.Find("Environment/Ruins");
            float[] angles={18,56,132,255,315};int index=0;
            foreach(float a in angles)
            {
                Vector3 p=Polar(a,78);
                if(Protected(p,3.7f))continue;
                var go=Module("Reclaimed_Broken_Arch_Pocket_"+index,ruins,"Den_Broken_Gateway_"+(index%2==0?"A":"B"),p,a+Rand(-18,18),Vector3.one*.43f);
                if(go.GetComponents<BoxCollider>().Length==0)foreach(int side in new[]{-1,1})
                {
                    var c=go.AddComponent<BoxCollider>();c.center=new Vector3(side*6.35f,4,0);c.size=new Vector3(2.15f,8,2.2f);
                }
                index++;
            }
            for(int i=0;i<2;i++)
            {
                Vector3 p=Polar(i==0?27:222,96);
                var go=Module("Ancient_Scribe_Statue_"+i,ruins,"Den_Ancient_Scribe_Statue",p,i==0?207:42,Vector3.one);
                var c=go.GetComponent<BoxCollider>();if(c==null)c=go.AddComponent<BoxCollider>();c.center=new Vector3(0,3.25f,0);c.size=new Vector3(2.7f,6.5f,2.3f);
            }
            var rocks=new Geometry();var foliage=new Geometry();
            int accepted=0;
            for(int attempt=0;attempt<450&&accepted<24;attempt++)
            {
                Vector3 p=Polar(Rand(0,360),Rand(64,81));
                if(Protected(p,2.3f))continue;
                rocks.Rock(p,new Vector3(Rand(.65f,1.1f),Rand(.5f,1.3f),Rand(.65f,1.1f)),attempt+51,Rock);
                foliage.MossPatch(p+Vector3.up*.03f,1.3f,attempt);
                accepted++;
            }
            Put("Reclaimed_Natural_Boulder_Groups",root.Find("Environment/Rocks"),rocks,stone,true);
            Put("Reclaimed_Boulder_Moss",root.Find("Environment/Trees"),foliage,stone,false);
        }

        static void ClearingsAndGround()
        {
            // Color-only change: original island/plot/path vertices and indices remain intact.
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string p=AnimationUtility.CalculateTransformPath(f.transform,root);
                if(!(p.StartsWith("Environment/Terrain/")||p.StartsWith("Environment/SecondaryPaths/Empty_Ground_")))continue;
                var mesh=f.sharedMesh;var vertices=mesh.vertices;var colors=new Color[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 v=vertices[i];float n=Mathf.PerlinNoise(v.x*.07f+12,v.z*.07f+74);
                    Color c=Color.Lerp(new Color(.25f,.44f,.18f),new Color(.43f,.57f,.25f),n);
                    foreach(Vector3 slot in slots)
                    {
                        Vector3 d=v-slot;float edge=Mathf.Max(Mathf.Abs(d.x)*.94f,Mathf.Abs(d.z)*1.03f);
                        float softness=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7.5f,14+1.1f*Mathf.Sin(d.x*.4f+d.z*.2f),edge));
                        c=Color.Lerp(c,new Color(.49f,.59f,.29f),softness*.67f);
                    }
                    colors[i]=c.linear;
                }
                mesh.colors=colors;EditorUtility.SetDirty(mesh);
            }
            var foundations=new Geometry();var edgeStones=new Geometry();
            foreach(var s in slots)foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                Vector3 p=s+new Vector3(x*8.5f,.018f,z*8.5f);
                foundations.Tile(p,2.0f,.58f,Quaternion.Euler(0,0,0),new Color(.47f,.52f,.36f));
                foundations.Tile(p+new Vector3(x*.65f,0,-z*.6f),.6f,1.6f,Quaternion.identity,new Color(.43f,.49f,.31f));
                Vector3 outer=s+new Vector3(x*14.4f,0,z*13.8f);
                if(new Vector2(outer.x,outer.z).magnitude<96&&!slots.Any(q=>Mathf.Abs(q.x-outer.x)<13.6f&&Mathf.Abs(q.z-outer.z)<13.6f))
                {
                    edgeStones.Rock(outer,new Vector3(.30f,.20f,.4f),x*30+z+91,Rock);
                    edgeStones.MossPatch(outer+Vector3.right*.6f+Vector3.up*.012f,.6f,32+x+z);
                }
            }
            Put("Clearing_Buried_Foundation_Corners",root.Find("Environment/SecondaryPaths"),foundations,stone,false);
            Put("Clearing_Perimeter_Stones_And_Moss",root.Find("Environment/Rocks"),edgeStones,stone,false);
            var mounds=new Geometry();int count=0;
            for(int attempt=0;attempt<800&&count<8;attempt++)
            {
                Vector3 p=Polar(Rand(0,360),Rand(65,80));if(Protected(p,3.2f))continue;
                for(int ring=0;ring<3;ring++)for(int n=0;n<16;n++)
                {
                    Func<int,int,Vector3> point=(i,r)=>p+Polar(i*22.5f,r*1.0f,.42f*(1-r*r/9f)+.005f);
                    mounds.Quad(point(n,ring),point(n,ring+1),point(n+1,ring+1),point(n+1,ring),new Color(.30f,.47f,.20f));
                }
                count++;
            }
            Put("Gentle_Grove_Earth_Hummocks",root.Find("Environment/Terrain"),mounds,stone,true);
        }

        static void WaterAndBridges()
        {
            foreach(var bridge in root.Find("Environment/Bridges").Cast<Transform>())
            {
                var supports=new Geometry();
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    Vector3 p=new Vector3(x*4.6f,-1.25f,z*8.9f);
                    supports.Column(p,1.23f,.67f,.55f,6,RockDark);
                    supports.MossPatch(new Vector3(p.x,.01f,p.z),.75f,x+z+17);
                }
                Put("Mossy_Ancient_Bridge_Piers",bridge,supports,stone,false);
                bridge.Find("Mossy_Ancient_Bridge_Piers").localScale = new Vector3(1, 1, 7.3f / 8.9f);
            }
            var cascades=new Geometry();var mouths=new Geometry();
            foreach(float a in new[]{22f,112f,202f,292f})
            {
                Vector3 dir=Polar(a,1);float lo=35,hi=46;
                for(int i=0;i<14;i++){float mid=(lo+hi)*.5f;if(OpenArchiveImprovements.WaterField(dir*mid)<0)lo=mid;else hi=mid;}
                Vector3 p=dir*((lo+hi)*.5f);Vector3 side=Polar(a+90,1),end=p-dir*1.0f+Vector3.down*1.12f;
                cascades.Quad(p-side*.8f,p+side*.8f,end+side*.65f,end-side*.65f,new Color(.4f,0,0));
                cascades.Quad(p+side*.8f,p-side*.8f,end-side*.65f,end+side*.65f,new Color(.4f,0,0));
                foreach(int sign in new[]{-1,1})mouths.Rock(p+side*sign*1.05f-Vector3.up*.1f,new Vector3(.55f,.45f,.7f),(int)a+sign,Rock);
                mouths.MossPatch(p+dir*.3f+Vector3.up*.012f,1.1f,(int)a);
            }
            Put("Small_Bank_Cascades",root.Find("Environment/Waterfalls"),cascades,water,false);
            Put("Mossy_Cascade_Stone_Mouths",root.Find("Environment/Rocks"),mouths,stone,false);
        }

        static void Atmosphere()
        {
            var sun=root.Find("Lighting/Warm_Daylight").GetComponent<Light>();sun.color=new Color(1,.94f,.83f);sun.intensity=1.35f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.57f;
            RenderSettings.ambientSkyColor=new Color(.58f,.73f,.86f);RenderSettings.ambientEquatorColor=new Color(.43f,.52f,.43f);RenderSettings.ambientGroundColor=new Color(.28f,.30f,.23f);
            var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            if(pipeline!=null)
            {
                string backup="Library/OpenArchive_Den_Redesign_Backup/UniversalRP.asset";
                if(!File.Exists(backup))File.Copy("Assets/Settings/UniversalRP.asset",backup);
                var serialized=new SerializedObject(pipeline);var enabled=serialized.FindProperty("m_SoftShadowsSupported");var quality=serialized.FindProperty("m_SoftShadowQuality");
                if(enabled!=null)enabled.boolValue=true;if(quality!=null)quality.intValue=1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
            }
        }

        static Mesh MeshAsset(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+name+".asset");
        static Mesh SaveMesh(string name,Geometry data)
        {
            var mesh=MeshAsset(name);if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/"+name+".asset");}
            data.Write(mesh);EditorUtility.SetDirty(mesh);return mesh;
        }
        static void Update(MeshFilter filter,Geometry data,Material material)
        {
            var mesh=filter.sharedMesh;if(!AssetDatabase.GetAssetPath(mesh).StartsWith(Folder+"/Meshes/"))throw new InvalidOperationException("Only owned mesh assets can be edited.");
            var collider=filter.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=null;
            data.Write(mesh);EditorUtility.SetDirty(mesh);if(collider!=null)collider.sharedMesh=mesh;
            filter.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        static GameObject Module(string name,Transform parent,string meshName,Vector3 p,float yaw,Vector3 scale)
        {
            var t=parent.Find(name);var go=t!=null?t.gameObject:new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale;
            go.GetComponent<MeshFilter>().sharedMesh=MeshAsset(meshName);go.GetComponent<MeshRenderer>().sharedMaterial=stone;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);return go;
        }
        static void Put(string name,Transform parent,Geometry data,Material material,bool collide)
        {
            var t=parent.Find(name);if(t!=null&&t.GetComponent<MeshCollider>()!=null)t.GetComponent<MeshCollider>().sharedMesh=null;
            var mesh=SaveMesh(name,data);var go=t!=null?t.gameObject:new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=parent==nest?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=true;
            if(collide){var c=go.GetComponent<MeshCollider>();if(c==null)c=go.AddComponent<MeshCollider>();c.sharedMesh=null;c.sharedMesh=mesh;}
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
        }

        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();readonly List<Color> colors=new List<Color>();readonly List<Vector2> uv=new List<Vector2>();
            public void Tri(Vector3 a,Vector3 b,Vector3 c,Color color,int tile=22)
            {
                if(Vector3.Cross(b-a,c-a).sqrMagnitude<.00000001f)return;int start=vertices.Count;
                foreach(var p in new[]{a,b,c}){vertices.Add(p);colors.Add(color.linear);uv.Add(new Vector2((tile%8+.5f)/8,(tile/8+.5f)/8));}
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,int tile=22){Tri(a,b,c,color,tile);Tri(a,c,d,color,tile);}
            public void QuadTinted(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Func<Vector3,Color> tint)
            {
                int start=vertices.Count;Tri(a,b,c,Color.white);Tri(a,c,d,Color.white);
                for(int i=start;i<vertices.Count;i++)colors[i]=tint(vertices[i]).linear;
            }
            public void FracturedColumn(Vector3 p,float height,float radius,int seed,Color color)
            {
                const int sides=7;var rings=new Vector3[4,sides];
                for(int r=0;r<4;r++)for(int i=0;i<sides;i++)
                {
                    float a=i*360f/sides,f=radius*(1-r*.055f)*(1+.045f*Mathf.Sin(seed+i*2.1f+r));
                    float y=height*r/3f;if(r==3)y+=height*.065f*Mathf.Sin(i*1.9f+seed)-height*.09f*(i==seed%7?1:0);
                    rings[r,i]=p+Polar(a,f,y)+new Vector3(r*.075f,0,r*.025f);
                }
                for(int i=0;i<sides;i++)
                {
                    int next=(i+1)%sides;for(int r=0;r<3;r++)Quad(rings[r,i],rings[r,next],rings[r+1,next],rings[r+1,i],Color.Lerp(color,RockDark,(i+r)%4*.055f));
                    Tri(p+new Vector3(.22f,height-.12f,.07f),rings[3,i],rings[3,next],RockLight);
                }
            }
            public void Box(Vector3 p,Vector3 size,Color color)
            {
                Vector3 a=p-size*.5f,b=p+size*.5f;
                Quad(new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z),color);
                Quad(new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z),color);
                Quad(new Vector3(a.x,a.y,a.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),color);
                Quad(new Vector3(b.x,a.y,b.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),color);
                Quad(new Vector3(b.x,a.y,a.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),color);
                Quad(new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z),color);
            }
            public void Column(Vector3 p,float h,float bottom,float top,int sides,Color color)
            {
                for(int n=0;n<sides;n++)
                {
                    Vector3 a=p+Polar(n*360f/sides,bottom),b=p+Polar((n+1)*360f/sides,bottom),c=p+Polar((n+1)*360f/sides,top,h),d=p+Polar(n*360f/sides,top,h);
                    Quad(a,b,c,d,Color.Lerp(color,RockDark,n%3*.07f));Tri(p+Vector3.up*h,d,c,color);
                }
            }
            public void Rock(Vector3 p,Vector3 size,int seed,Color color)
            {
                var random=new System.Random(Math.Abs(seed)+1);const int sides=7;var rings=new Vector3[3,sides];
                for(int r=0;r<3;r++)for(int n=0;n<sides;n++)
                {
                    float f=(r==0?.8f:r==1?1:.68f)*(.88f+(float)random.NextDouble()*.2f);
                    Vector3 v=Polar(n*360f/sides+(r==1?4:0),f);v.x*=size.x;v.z*=size.z;v.y=size.y*(r==0?0:r==1?.38f:.82f)+(r==0?0:(float)random.NextDouble()*.12f*size.y);rings[r,n]=p+v;
                }
                for(int n=0;n<sides;n++)
                {
                    int next=(n+1)%sides;for(int r=0;r<2;r++)Quad(rings[r,n],rings[r,next],rings[r+1,next],rings[r+1,n],Color.Lerp(color,RockDark,(n%3)*.10f));
                    Tri(p+Vector3.up*size.y,rings[2,n],rings[2,next],Color.Lerp(color,RockLight,n%2*.18f));Tri(p,rings[0,next],rings[0,n],color);
                }
            }
            public void Tile(Vector3 p,float w,float d,Quaternion rotation,Color color)
            {
                var outline=new[]{new Vector3(-.37f,0,-.5f),new Vector3(-.5f,0,-.27f),new Vector3(-.48f,0,.36f),new Vector3(-.22f,0,.5f),new Vector3(.42f,0,.48f),new Vector3(.5f,0,.2f),new Vector3(.48f,0,-.3f),new Vector3(.25f,0,-.48f)};
                for(int i=0;i<outline.Length;i++){Vector3 a=outline[i],b=outline[(i+1)%outline.Length];a.x*=w;a.z*=d;b.x*=w;b.z*=d;Tri(p,p+rotation*a,p+rotation*b,color);}
            }
            public void MossPatch(Vector3 p,float radius,int seed)
            {
                for(int i=0;i<7;i++)Tri(p,p+Polar(i*360f/7,radius*(.8f+.15f*Mathf.Sin(seed+i))),p+Polar((i+1)*360f/7,radius*(.8f+.15f*Mathf.Sin(seed+i+1))),Color.Lerp(Moss,new Color(.34f,.47f,.18f),i%3*.12f));
            }
            public void Stroke(Vector3 a,Vector3 b,float width,Color color,Vector3 normal,int tile=22)
            {
                Vector3 side=Vector3.Cross(normal,(b-a).normalized)*width*.5f;Quad(a-side,b-side,b+side,a+side,color,tile);Quad(a+side,b+side,b-side,a-side,color,tile);
            }
            public void Prism(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float depth,Color color)
            {
                Vector3 z=Vector3.forward*depth;
                Quad(a-z,b-z,c-z,d-z,color);Quad(d+z,c+z,b+z,a+z,color);
                Quad(a-z,a+z,b+z,b-z,color);Quad(b-z,b+z,c+z,c-z,color);Quad(c-z,c+z,d+z,d-z,RockLight);Quad(d-z,d+z,a+z,a-z,color);
            }
            public void Link(Vector3 p,Quaternion q,float rx,float ry,float tube,bool broken,Color color)
            {
                const int around=12,cross=5;
                Func<int,int,Vector3> vertex=(n,k)=>
                {
                    float a=n*Mathf.PI*2/around,b=k*Mathf.PI*2/cross;Vector3 radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                    return p+q*(new Vector3(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry,0)+radial*Mathf.Cos(b)*tube+Vector3.forward*Mathf.Sin(b)*tube);
                };
                for(int i=0;i<(broken?9:around);i++)for(int k=0;k<cross;k++)Quad(vertex(i,k),vertex(i+1,k),vertex(i+1,k+1),vertex(i,k+1),color);
            }
            public void Write(Mesh mesh)
            {
                mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            }
        }
    }
}
