using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>A grounded, weathered sanctuary over the existing nest. Presentation only.</summary>
    public static class ArchiveNestSanctuaryBuilder
    {
        const string Folder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";
        const string Map="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
        public const string RootName="Nest_Sanctuary_Dressing";
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int renderers,triangles,lowStoneGroups,legacyRuneRenderersHidden;
            public float sealRadius=12.4f,cardinalCorridorWidth=25;
            public string scope="Scene-only nest materials and support-projected opaque inlays. Original floor meshes, colliders, four 25-unit corridors, MonsterSpawnPoint, rigs, navigation and existing species warning effects are unchanged.";
        }
        [Serializable] sealed class SourceEntry{public string path,material;public bool enabled;}
        [Serializable] sealed class SourceLedger{public List<SourceEntry> sources=new List<SourceEntry>();}
        public static Report LastReport {get;private set;}

        public static Transform Build(ArchiveTopicSession session,Transform parent,Material stone,Material trim,Material glow)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Author the sanctuary in stopped Edit Mode.");
            if(session==null||parent==null||stone==null||trim==null||glow==null)throw new ArgumentNullException("Existing scene and materials are required.");
            var map=GameObject.Find("OpenArchive");var nest=map==null?null:map.transform.Find("UniversalMonsterNest");
            if(nest==null||session.hunting==null||session.hunting.monsterSpawnPoint==null)throw new InvalidOperationException("Existing nest and MonsterSpawnPoint are required.");
            var floor=nest.Find("Nest_StoneFloor_30m")?.GetComponent<Renderer>();
            var bed=nest.Find("Den_Compacted_Sleeping_Bed")?.GetComponent<Renderer>();
            if(floor==null||bed==null)throw new InvalidOperationException("Existing nest floor and compacted bed are required.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var ledgerPath=Folder+"/NestSanctuarySources.json";
            var ledger=File.Exists(ledgerPath)?JsonUtility.FromJson<SourceLedger>(File.ReadAllText(ledgerPath)):new SourceLedger();
            var legacy=nest.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Ancient_Gold_Rune",StringComparison.Ordinal)).ToArray();
            foreach(var renderer in new[]{floor,bed}.Concat(legacy))
            {
                var path=AnimationUtility.CalculateTransformPath(renderer.transform,map.transform);
                if(!ledger.sources.Any(s=>s.path==path))ledger.sources.Add(new SourceEntry{path=path,enabled=renderer.enabled,material=AssetDatabase.GetAssetPath(renderer.sharedMaterial)});
            }
            File.WriteAllText(ledgerPath,JsonUtility.ToJson(ledger,true));

            // Keep the old floor geometry and collision exactly in place. World-space
            // detail textures provide visual depth without adding a second ground sheet.
            // Use the world-space stone detail shader, not the river-rock palette
            // shader: the old floor and the new stones have different palette UVs.
            var sourceStone=AssetDatabase.LoadAssetAtPath<Material>(Map+"/Art/Materials/Archive_WeatheredStone_Vertex.mat");
            var sourceEarth=AssetDatabase.LoadAssetAtPath<Material>(Map+"/Art/Materials/Archive_NestDarkEarth.mat");
            if(sourceStone==null||sourceEarth==null)throw new InvalidOperationException("Existing textured stone and nest earth materials are required.");
            var center=session.hunting.monsterSpawnPoint.position;
            var outer=Surface("Nest_Sanctuary_WeatheredFloor",sourceStone,new Color(.52f,.55f,.52f),1,3.1f,.10f);
            var ground=GroundSurface(sourceStone,center);
            // One world-space material spans both original meshes. The visible bed
            // outline cannot introduce a hard earth/stone color boundary.
            floor.sharedMaterial=ground;bed.sharedMaterial=ground;
            PrefabUtility.RecordPrefabInstancePropertyModifications(floor);PrefabUtility.RecordPrefabInstancePropertyModifications(bed);
            EditorUtility.SetDirty(floor);EditorUtility.SetDirty(bed);
            // The legacy gold motif was authored for an older, higher floor. Replace
            // that visual once instead of stacking another floating seal below it.
            foreach(var renderer in legacy){renderer.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);EditorUtility.SetDirty(renderer);}
            var old=parent.Find(RootName);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(RootName).transform;root.SetParent(parent,false);
            var report=new Report{legacyRuneRenderersHidden=legacy.Length};LastReport=report;
            Shape grooves=new Shape(root),metal=new Shape(root),masonry=new Shape(root);
            var dark=Opaque("Nest_Sanctuary_DarkIncisions",new Color(.075f,.095f,.082f));
            var bronze=Opaque("Nest_Sanctuary_WornBronze",new Color(.43f,.30f,.12f));
            Vector3 Ground(Vector3 p,float extra=.105f)
            {
                var hits=Physics.RaycastAll(new Vector3(p.x,center.y+7,p.z),Vector3.down,12,~(1<<7),QueryTriggerInteraction.Ignore)
                    .Where(h=>h.normal.y>.75f&&h.point.y>=center.y-.5f&&h.point.y<=center.y+2&&
                        !h.collider.transform.IsChildOf(session.hunting.transform)&&h.collider.GetComponentInParent<Player>()==null&&
                        (session.worldProgression==null||session.worldProgression.secondary==null||!h.collider.transform.IsChildOf(session.worldProgression.secondary.transform)))
                    .OrderBy(h=>h.distance).ToArray();
                float y=hits.Length>0?hits[0].point.y:center.y;
                return new Vector3(p.x,y+extra,p.z);
            }
            Vector3 Polar(float angle,float radius)=>center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            void Ring(Shape shape,float radius,float width,int segments,float lift,bool broken=false)
            {
                for(int i=0;i<segments;i++)
                {
                    if(broken&&(i%12==9||i%12==10))continue;
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    shape.Quad(Ground(Polar(a,radius-width*.5f),lift),Ground(Polar(b,radius-width*.5f),lift),Ground(Polar(b,radius+width*.5f),lift),Ground(Polar(a,radius+width*.5f),lift));
                }
            }
            void Inscribe(Shape shape,Vector3 a,Vector3 b,float width,float lift=.105f)
            {
                var direction=b-a;direction.y=0;if(direction.sqrMagnitude<.000001f)return;
                var side=Vector3.Cross(Vector3.up,direction.normalized)*width*.5f;
                shape.Quad(Ground(a-side,lift),Ground(b-side,lift),Ground(b+side,lift),Ground(a+side,lift));
            }
            Ring(grooves,12.4f,.16f,96,.105f);Ring(grooves,10.4f,.11f,96,.105f);Ring(grooves,7.8f,.11f,72,.105f);
            Ring(metal,12.16f,.043f,96,.112f,true);Ring(metal,10.16f,.035f,96,.112f,true);
            // Twenty-four worn ticks and eight mathematical forms read as an engraved
            // archive seal, not an animated challenge surface or another bright portal.
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12;Inscribe(grooves,Polar(a,11.05f),Polar(a,i%3==0?11.75f:11.40f),i%3==0?.10f:.055f);
                if(i%3!=0)continue;
                var tangent=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a));var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var p=Polar(a,9.1f);
                if(i%6==0)
                {
                    Inscribe(metal,p-tangent*.42f-radial*.38f,p+radial*.40f,.055f,.112f);
                    Inscribe(metal,p+radial*.40f,p+tangent*.42f-radial*.38f,.055f,.112f);
                }
                else
                {
                    Inscribe(metal,p-tangent*.42f+radial*.38f,p-radial*.40f,.055f,.112f);
                    Inscribe(metal,p-radial*.40f,p+tangent*.42f+radial*.38f,.055f,.112f);
                }
            }
            // A restrained graph motif under the sleeping creature, with a visible
            // outer ring beyond its body. It contains no puzzle solution or trigger.
            Ring(grooves,2.1f,.085f,48,.105f);
            for(int i=0;i<6;i++)
            {
                float a=(i+.5f)*Mathf.PI/3;var node=Polar(a,5.25f);
                Inscribe(grooves,Polar(a,2.1f),node,.09f);
                for(int n=0;n<12;n++)
                {
                    float b=n*Mathf.PI/6,c=(n+1)*Mathf.PI/6;
                    var p=node+new Vector3(Mathf.Cos(b)*.35f,0,Mathf.Sin(b)*.35f);var q=node+new Vector3(Mathf.Cos(c)*.35f,0,Mathf.Sin(c)*.35f);
                    Inscribe(metal,p,q,.055f,.112f);
                }
            }
            // Broken stones occupy only the diagonal margins. Full 25-unit cardinal
            // corridors and the central sleeping/carry volume remain visually open.
            var random=new System.Random(91873);
            for(int quadrant=0;quadrant<4;quadrant++)for(int i=0;i<5;i++)
            {
                float angle=(quadrant*90+34+i*5.5f)*Mathf.Deg2Rad;
                float radius=28.4f+(i%2)*1.8f;
                var p=Polar(angle,radius);var local=nest.InverseTransformPoint(p);
                if(Mathf.Abs(local.x*nest.lossyScale.x)<14.8f||Mathf.Abs(local.z*nest.lossyScale.z)<14.8f)continue;
                p=Ground(p,.025f);
                float length=1.7f+(float)random.NextDouble()*1.05f,width=.55f+(float)random.NextDouble()*.28f,height=.12f+(float)random.NextDouble()*.13f;
                var rotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
                masonry.Block(p+Vector3.up*height*.5f,new Vector3(length,height,width),rotation);
                if(i%2==0)
                {
                    var tangent=rotation*Vector3.right;
                    Inscribe(metal,p-tangent*.38f,p+tangent*.38f,.032f,height+.04f);
                }
                report.lowStoneGroups++;
            }
            var shapes=new[]{grooves,metal,masonry};var names=new[]{"Support_Projected_Math_Incisions","Worn_Bronze_Seal_Inlay","Diagonal_Broken_Courtyard_Stones"};var materials=new[]{dark,bronze,outer};
            for(int i=0;i<shapes.Length;i++)
            {
                var mesh=shapes[i].Save("Nest_Sanctuary_"+names[i]);
                var go=new GameObject(names[i],typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=materials[i];renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                report.renderers++;report.triangles+=mesh.triangles.Length/3;
            }
            report.passed=root.GetComponentsInChildren<Collider>(true).Length==0&&report.renderers==3;
            Directory.CreateDirectory("Temp/ArchiveNestSanctuary");File.WriteAllText("Temp/ArchiveNestSanctuary/Build.json",JsonUtility.ToJson(report,true));
            AssetDatabase.SaveAssets();return root;
        }
        static Material GroundSurface(Material source,Vector3 center)
        {
            var shader=Shader.Find("LogicLegends/OpenArchive/NestGround");
            if(shader==null)throw new InvalidOperationException("Import the owned ArchiveNestGround shader before building the sanctuary.");
            const string name="Nest_Sanctuary_ContinuousGround";string path=Folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            material.enableInstancing=true;material.renderQueue=2000;
            material.SetColor("_StoneColor",new Color(.43f,.46f,.43f));material.SetColor("_EarthColor",new Color(.36f,.33f,.27f));
            material.SetVector("_GroundCenter",new Vector4(center.x,center.y,center.z,0));material.SetVector("_BlendRadii",new Vector4(10,22,0,0));
            material.SetFloat("_StoneRepeat",7);material.SetFloat("_GrainStrength",.5f);
            foreach(var property in new[]{"_DetailAtlas","_MacroMap","_StoneMap"})material.SetTexture(property,source.GetTexture(property));
            EditorUtility.SetDirty(material);return material;
        }
        static Material Surface(string name,Material source,Color color,float style,float repeat,float moss)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(source){name=name};AssetDatabase.CreateAsset(material,path);}
            else {material.shader=source.shader;material.CopyPropertiesFromMaterial(source);}
            material.name=name;material.enableInstancing=true;material.SetColor("_BaseColor",color);
            if(material.HasProperty("_BaseMap")){material.SetTexture("_BaseMap",Texture2D.whiteTexture);material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);}
            if(material.HasProperty("_VertexColor"))material.SetFloat("_VertexColor",0);
            if(material.HasProperty("_SurfaceStyle"))material.SetFloat("_SurfaceStyle",style);
            if(material.HasProperty("_TileMeters"))material.SetFloat("_TileMeters",repeat);
            if(material.HasProperty("_DetailStrength"))material.SetFloat("_DetailStrength",1);
            if(material.HasProperty("_StoneTextureStrength"))material.SetFloat("_StoneTextureStrength",style<2.5f?.92f:0);
            if(material.HasProperty("_MossAmount"))material.SetFloat("_MossAmount",moss);
            if(material.HasProperty("_EmissionStrength"))material.SetFloat("_EmissionStrength",0);
            EditorUtility.SetDirty(material);return material;
        }
        static Material Opaque(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(material,path);}
            material.enableInstancing=true;material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.17f);material.SetFloat("_Surface",0);material.renderQueue=2000;EditorUtility.SetDirty(material);return material;
        }
        sealed class Shape
        {
            readonly Transform root;readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            public Shape(Transform owner){root=owner;}
            public void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int n=vertices.Count;vertices.Add(root.InverseTransformPoint(a));vertices.Add(root.InverseTransformPoint(b));vertices.Add(root.InverseTransformPoint(c));indices.AddRange(new[]{n,n+1,n+2});}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
            public void Block(Vector3 center,Vector3 size,Quaternion rotation)
            {
                float x=size.x*.5f,z=size.z*.5f,cut=.16f,h=size.y*.5f;
                var ring=new[]{new Vector3(-x+cut,0,-z),new Vector3(x-cut,0,-z),new Vector3(x,0,-z+cut),new Vector3(x,0,z-cut),new Vector3(x-cut,0,z),new Vector3(-x+cut,0,z),new Vector3(-x,0,z-cut),new Vector3(-x,0,-z+cut)};
                for(int i=0;i<8;i++){int j=(i+1)%8;var a=center+rotation*(ring[i]-Vector3.up*h);var b=center+rotation*(ring[j]-Vector3.up*h);var c=center+rotation*(ring[i]*.95f+Vector3.up*h);var d=center+rotation*(ring[j]*.95f+Vector3.up*h);Quad(b,a,c,d);Triangle(center+rotation*Vector3.up*h,d,c);}
            }
            public Mesh Save(string name)
            {
                string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();var normals=mesh.normals;var uv=new List<Vector2>(vertices.Count);
                for(int i=0;i<vertices.Count;i++){var p=vertices[i];var n=normals[i];var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));uv.Add((a.y>=a.x&&a.y>=a.z?new Vector2(p.x,p.z):a.x>=a.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.4f);}
                mesh.SetUVs(0,uv);EditorUtility.SetDirty(mesh);return mesh;
            }
        }
    }
}
