using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Art-only upgrade; preserves existing prefab GUIDs, item axes and inventory ownership.</summary>
    public static class ArchiveRewardArtPolish
    {
        public const string Folder = ArchiveRewardSetup.Folder + "/Polished";
        static Material wood, brass, steel, dark, liner, teal, ivory, red, green, amber, glow;
        [MenuItem("Tools/Logic Legends/Open Archive/Art/Polish Reward Chest And Supplies")]
        public static void Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use the saved MultiplayerGameplay scene outside Play Mode.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            wood=Mat("Oiled_Timber",new Color(.58f,.34f,.17f),.0f,.24f);
            wood.SetTexture("_BaseMap",TimberAlbedo());
            brass=Mat("Antique_Brass",new Color(.64f,.40f,.13f),.62f,.39f);
            steel=Mat("Satin_Steel",new Color(.38f,.48f,.52f),.65f,.43f);
            dark=Mat("Graphite_Rubber",new Color(.035f,.055f,.065f),.08f,.20f);
            liner=Mat("Velvet_Interior",new Color(.025f,.12f,.13f),0,.12f);
            teal=Mat("Archive_Teal",new Color(.055f,.34f,.36f),.26f,.34f);
            ivory=Mat("Warm_Ivory",new Color(.86f,.90f,.81f),.1f,.35f);
            red=Mat("Medical_Enamel",new Color(.58f,.055f,.085f),.10f,.32f);
            green=Mat("Revival_Enamel",new Color(.045f,.40f,.21f),.10f,.32f);
            amber=Mat("Copper_Amber",new Color(.92f,.40f,.07f),.42f,.36f);
            glow=Mat("Reward_Inlay",new Color(.32f,.78f,.65f),.15f,.32f);
            glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(.17f,.65f,.43f)*1.4f);
            var view=UnityEngine.Object.FindFirstObjectByType<ArchiveRewardView>();
            if(view==null)throw new InvalidOperationException("Existing reward view required.");
            BuildChest(view);
            for(int i=0;i<6;i++)BuildItem(view,i);
            view.chestSpawnSound=AssetDatabase.LoadAssetAtPath<AudioClip>(ArchiveRewardSetup.Folder+"/Audio/Archive_Chest_Materialize.wav");
            if(view.chestSpawnSound==null)throw new InvalidOperationException("Import the chest arrival sound first.");
            var audio=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(view.chestSpawnSound));
            var sample=audio.defaultSampleSettings;sample.loadType=AudioClipLoadType.DecompressOnLoad;sample.compressionFormat=AudioCompressionFormat.Vorbis;sample.quality=.8f;
            audio.defaultSampleSettings=sample;audio.forceToMono=true;audio.SaveAndReimport();
            var world=view.session.worldProgression;world.nightFogColor=new Color(.16f,.19f,.24f);world.nightFogDensity=.023f;
            world.nightFogSkyboxShader=Shader.Find("LogicLegends/OpenArchive/Fog Horizon Skybox");
            EditorUtility.SetDirty(world);EditorUtility.SetDirty(view);EditorUtility.SetDirty(wood);EditorUtility.SetDirty(glow);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Reward art polished: hollow coffer, six fitted supplies, refreshed inventory icons and stronger night-only fog.");
        }
        static Material Mat(string name,Color color,float metallic,float smooth)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
        }
        static Texture2D TimberAlbedo()
        {
            string path=Folder+"/Coffer_Timber_Grain.png";
            if(!File.Exists(path))
            {
                const int size=256;var image=new Texture2D(size,size,TextureFormat.RGB24,true);var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float wave=Mathf.Sin(y*.049f)*3+Mathf.Sin(y*.013f)*5;
                    float grain=Mathf.PerlinNoise((x+wave)*.034f,y*.31f);
                    float fine=Mathf.PerlinNoise(x*.31f,y*.77f);
                    float value=.65f+grain*.29f+fine*.06f;pixels[y*size+x]=new Color(value,value,value);
                }
                image.SetPixels(pixels);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=256;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Transform Anchor(Transform root,string name,Vector3 position)
        {var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;return go.transform;}
        static void BuildChest(ArchiveRewardView view)
        {
            var root=new GameObject("Archive_Solver_Reward_Chest");
            try
            {
                var body=new Draft();
                body.Box(new Vector3(0,.16f,0),new Vector3(2.08f,.18f,1.32f),brass,.025f);
                body.Box(new Vector3(0,.25f,0),new Vector3(1.87f,.04f,1.08f),liner,.015f);
                body.Box(new Vector3(0,.365f,0),new Vector3(1.72f,.21f,.97f),liner,.045f);
                for(int i=0;i<5;i++)foreach(int sign in new[]{-1,1})
                    body.Box(new Vector3((i-2)*.40f,.56f,sign*.625f),new Vector3(.393f,.63f,.13f),wood,.015f);
                foreach(int sign in new[]{-1,1})
                {
                    body.Box(new Vector3(sign*1.0f,.56f,0),new Vector3(.13f,.63f,1.16f),wood,.02f);
                    body.Box(new Vector3(sign*.916f,.56f,0),new Vector3(.025f,.55f,1.08f),liner,.005f);
                    body.Box(new Vector3(0,.56f,sign*.545f),new Vector3(1.84f,.55f,.025f),liner,.005f);
                    body.Box(new Vector3(sign*1.01f,.885f,0),new Vector3(.15f,.06f,1.32f),brass,.018f);
                    body.Box(new Vector3(0,.885f,sign*.625f),new Vector3(1.92f,.06f,.15f),brass,.018f);
                    foreach(int front in new[]{-1,1})
                    {
                        body.Box(new Vector3(sign*.83f,.075f,front*.48f),new Vector3(.27f,.15f,.27f),dark,.035f);
                        body.Box(new Vector3(sign*.96f,.54f,front*.65f),new Vector3(.13f,.61f,.14f),brass,.018f);
                        foreach(float y in new[]{.32f,.72f})body.Tube(new Vector3(sign*.83f,y,front*.705f),.026f,.026f,.018f,steel,front>0?Quaternion.identity:Quaternion.Euler(0,180,0),8);
                    }
                }
                body.Box(new Vector3(0,.60f,.715f),new Vector3(.28f,.33f,.065f),brass,.035f);
                body.Box(new Vector3(0,.60f,.755f),new Vector3(.13f,.13f,.025f),teal,.015f,Quaternion.Euler(0,0,45));
                // A restrained mathematical inlay sits on the interior cushion.
                body.Bar(new Vector3(-.35f,.478f,0),new Vector3(0,.478f,.24f),.018f,glow);
                body.Bar(new Vector3(0,.478f,.24f),new Vector3(.35f,.478f,0),.018f,glow);
                body.Bar(new Vector3(.35f,.478f,0),new Vector3(0,.478f,-.24f),.018f,glow);
                body.Bar(new Vector3(0,.478f,-.24f),new Vector3(-.35f,.478f,0),.018f,glow);
                body.Save(root.transform,"Hollow_Coffer_Body");
                var lid=Anchor(root.transform,"Chest_Lid",new Vector3(0,.91f,-.625f));var roof=new Draft();
                for(int i=0;i<7;i++)
                {
                    float z=(i+.5f)*1.25f/7,y=.05f+.22f*Mathf.Sin(z/1.25f*Mathf.PI);
                    var rotation=Quaternion.Euler(-Mathf.Atan(.22f*Mathf.PI/1.25f*Mathf.Cos(z/1.25f*Mathf.PI))*Mathf.Rad2Deg,0,0);
                    roof.Box(new Vector3(0,y,z),new Vector3(2.07f,.105f,.184f),wood,.018f,rotation);
                    foreach(int side in new[]{-1,1})roof.Box(new Vector3(side*.73f,y+.068f,z),new Vector3(.085f,.032f,.189f),brass,.008f,rotation);
                }
                roof.Box(new Vector3(0,-.025f,.625f),new Vector3(1.92f,.05f,1.14f),liner,.025f);
                foreach(int side in new[]{-1,1})roof.Box(new Vector3(side*1.01f,.025f,.625f),new Vector3(.10f,.10f,1.28f),brass,.02f);
                foreach(float z in new[]{0,1.25f})roof.Box(new Vector3(0,.03f,z),new Vector3(2.07f,.10f,.10f),brass,.02f);
                foreach(int side in new[]{-1,1})roof.Tube(new Vector3(side*.73f,0,0),.055f,.055f,.22f,steel,Quaternion.Euler(0,90,0),10);
                roof.Bar(new Vector3(-.22f,-.066f,.83f),new Vector3(.22f,-.066f,.83f),.025f,glow);
                roof.Bar(new Vector3(.22f,-.066f,.83f),new Vector3(-.08f,-.066f,.625f),.025f,glow);
                roof.Bar(new Vector3(-.08f,-.066f,.625f),new Vector3(.22f,-.066f,.42f),.025f,glow);
                roof.Bar(new Vector3(.22f,-.066f,.42f),new Vector3(-.22f,-.066f,.42f),.025f,glow);
                roof.Save(lid,"Carved_Coffer_Lid");
                var art=root.AddComponent<ArchiveChestArt>();art.lid=lid;art.rewardAnchor=Anchor(root.transform,"Interior_Reward_Anchor",new Vector3(0,.67f,0));
                var lamp=Anchor(root.transform,"Interior_Warm_Glow",new Vector3(0,.85f,0)).gameObject.AddComponent<Light>();
                lamp.type=LightType.Point;lamp.color=new Color(1,.68f,.32f);lamp.intensity=.75f;lamp.range=3.5f;lamp.shadows=LightShadows.None;lamp.bounceIntensity=0;lamp.enabled=false;art.interiorLight=lamp;
                view.chestPrefab=PrefabUtility.SaveAsPrefabAsset(root,ArchiveRewardSetup.Folder+"/Archive_Solver_Reward_Chest.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void BuildItem(ArchiveRewardView view,int item)
        {
            var root=new GameObject("Supply_"+(ArchiveSurvivalItem)item);var d=new Draft();var vertical=Quaternion.Euler(-90,0,0);
            try
            {
                if(item==0)
                {
                    d.Tube(new Vector3(0,0,.04f),.10f,.10f,.62f,dark);
                    d.Tube(new Vector3(0,0,-.30f),.115f,.115f,.065f,steel);
                    for(int i=0;i<7;i++)d.Tube(new Vector3(0,0,-.22f+i*.075f),.111f,.111f,.018f,dark);
                    d.Tube(new Vector3(0,0,.43f),.11f,.11f,.12f,teal);
                    d.Tube(new Vector3(0,0,.56f),.115f,.165f,.17f,steel);
                    d.Ring(new Vector3(0,0,.67f),.17f,.132f,.055f,dark);
                    d.Tube(new Vector3(0,0,.689f),.129f,.129f,.013f,ivory);
                    d.Tube(new Vector3(0,0,.698f),.032f,.032f,.005f,amber);
                    d.Box(new Vector3(0,.109f,.26f),new Vector3(.075f,.026f,.11f),amber,.012f);
                    Anchor(root.transform,"Lens",new Vector3(0,0,.69f));
                }
                else if(item==1)
                {
                    d.Tube(new Vector3(0,.15f,0),.15f,.15f,.53f,dark,vertical,12);
                    d.Tube(new Vector3(0,.405f,0),.155f,.155f,.10f,amber,vertical,12);
                    d.Tube(new Vector3(0,.475f,0),.065f,.065f,.055f,steel,vertical,10);
                    d.Tube(new Vector3(0,-.11f,0),.155f,.155f,.035f,steel,vertical,12);
                    d.Box(new Vector3(0,.15f,.146f),new Vector3(.18f,.33f,.022f),teal,.018f);
                    for(int i=0;i<3;i++)d.Box(new Vector3(0,.07f+i*.09f,.163f),new Vector3(.10f,.043f,.008f),amber,.006f);
                    d.Box(new Vector3(0,.372f,.155f),new Vector3(.065f,.014f,.012f),ivory,.003f);
                    d.Box(new Vector3(0,.372f,.155f),new Vector3(.014f,.065f,.012f),ivory,.003f);
                }
                else if(item==2||item==3)
                {
                    d.Box(new Vector3(0,.04f,.15f),new Vector3(.70f,.53f,.32f),item==2?red:green,.055f);
                    d.Box(new Vector3(0,.04f,.15f),new Vector3(.72f,.028f,.33f),dark,.008f);
                    d.Box(new Vector3(0,.39f,.15f),new Vector3(.34f,.065f,.08f),dark,.017f);
                    foreach(int side in new[]{-1,1})
                    {
                        d.Box(new Vector3(side*.145f,.31f,.15f),new Vector3(.06f,.13f,.08f),dark,.012f);
                        d.Box(new Vector3(side*.245f,.205f,.322f),new Vector3(.08f,.11f,.035f),brass,.012f);
                        foreach(int sign in new[]{-1,1})d.Box(new Vector3(side*.29f,sign*.17f,.15f),new Vector3(.12f,.11f,.34f),dark,.025f);
                    }
                    d.Box(new Vector3(0,.05f,.327f),new Vector3(.37f,.36f,.025f),ivory,.035f);
                    d.Box(new Vector3(0,.05f,.345f),new Vector3(.28f,.09f,.015f),item==2?red:green,.008f);
                    d.Box(new Vector3(0,.05f,.345f),new Vector3(.09f,.28f,.015f),item==2?red:green,.008f);
                    if(item==3)foreach(int side in new[]{-1,1})
                    {
                        d.Tube(new Vector3(side*.405f,.02f,.15f),.057f,.057f,.37f,ivory,vertical,10);
                        d.Tube(new Vector3(side*.405f,.02f,.15f),.061f,.061f,.14f,teal,vertical,10);
                        foreach(int end in new[]{-1,1})d.Tube(new Vector3(side*.405f,.02f+end*.20f,.15f),.067f,.067f,.05f,brass,vertical,10);
                    }
                }
                else if(item==4)
                {
                    d.Box(new Vector3(0,-.16f,.02f),new Vector3(.21f,.43f,.24f),dark,.035f,Quaternion.Euler(-12,0,0));
                    d.Box(new Vector3(0,.11f,.23f),new Vector3(.30f,.25f,.65f),teal,.045f);
                    d.Box(new Vector3(0,.253f,.18f),new Vector3(.26f,.055f,.50f),steel,.018f);
                    d.Tube(new Vector3(0,.12f,.73f),.079f,.079f,.56f,steel);
                    d.Ring(new Vector3(0,.12f,1.035f),.105f,.055f,.09f,dark);
                    d.Tube(new Vector3(0,.12f,1.05f),.054f,.054f,.015f,dark);
                    d.Tube(new Vector3(0,-.035f,.56f),.067f,.067f,.33f,dark);
                    d.Tube(new Vector3(0,-.035f,.72f),.074f,.074f,.035f,amber);
                    d.Box(new Vector3(0,.30f,.40f),new Vector3(.055f,.055f,.13f),amber,.012f);
                    d.Box(new Vector3(0,-.16f,.27f),new Vector3(.09f,.04f,.28f),steel,.01f);
                    d.Box(new Vector3(0,-.09f,.39f),new Vector3(.09f,.15f,.04f),steel,.01f);
                    d.Box(new Vector3(0,-.055f,.22f),new Vector3(.045f,.08f,.04f),dark,.008f,Quaternion.Euler(-20,0,0));
                    foreach(int side in new[]{-1,1})for(int i=0;i<3;i++)d.Box(new Vector3(side*.153f,.11f,.10f+i*.09f),new Vector3(.015f,.08f,.025f),steel,.004f);
                    Anchor(root.transform,"Muzzle_Cap",new Vector3(0,.12f,1.08f));
                }
                else
                {
                    d.Box(new Vector3(0,-.02f,.13f),new Vector3(.55f,.15f,.24f),dark,.025f);
                    d.Box(new Vector3(0,-.065f,.258f),new Vector3(.43f,.025f,.01f),brass,.004f);
                    for(int i=0;i<3;i++)
                    {
                        float x=(i-1)*.18f;
                        d.Tube(new Vector3(x,.20f,.13f),.033f,.033f,.41f,steel,vertical,8);
                        d.Tube(new Vector3(x,.455f,.13f),.042f,0,.10f,amber,vertical,8);
                        d.Box(new Vector3(x,.04f,.13f),new Vector3(.115f,.15f,.025f),teal,.007f);
                        d.Box(new Vector3(x,.04f,.13f),new Vector3(.025f,.15f,.115f),teal,.007f);
                    }
                }
                d.Save(root.transform,"Detail_Mesh_"+(ArchiveSurvivalItem)item);
                string path=ArchiveItemAssets.Folder+"/Supply_"+(ArchiveSurvivalItem)item+".prefab";
                view.itemPrefabs[item]=PrefabUtility.SaveAsPrefabAsset(root,path);
                string icon=ArchiveItemAssets.Folder+"/Icon_"+(ArchiveSurvivalItem)item+".png";
                ArchiveItemAssets.RenderIcon(view.itemPrefabs[item],icon,item==0?new Vector3(3.5f,2.1f,1.6f):new Vector3(2,1.3f,3));
                view.itemIcons[item]=AssetDatabase.LoadAssetAtPath<Sprite>(icon);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        sealed class Draft
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();
            readonly List<Material> materials=new List<Material>();readonly List<List<int>> indices=new List<List<int>>();
            void Face(Vector3[] points,Vector3 expected,Material material)
            {
                int slot=materials.IndexOf(material);if(slot<0){slot=materials.Count;materials.Add(material);indices.Add(new List<int>());}
                bool reverse=Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),expected)<0;
                int first=vertices.Count;
                var normal=expected.normalized;var u=Mathf.Abs(normal.y)>.85f?Vector3.right:Vector3.Cross(Vector3.up,normal).normalized;
                var v=Vector3.Cross(normal,u).normalized;
                for(int i=0;i<points.Length;i++){vertices.Add(points[i]);uv.Add(new Vector2(Vector3.Dot(points[i],u)*2,Vector3.Dot(points[i],v)*2));}
                for(int i=1;i<points.Length-1;i++){indices[slot].Add(first);indices[slot].Add(first+(reverse?i+1:i));indices[slot].Add(first+(reverse?i:i+1));}
            }
            public void Box(Vector3 p,Vector3 size,Material m,float bevel,Quaternion rotation=default)
            {
                if(rotation==default)rotation=Quaternion.identity;
                float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f,b=Mathf.Min(bevel,Mathf.Min(x,Mathf.Min(y,z))*.65f);
                var ring=new[]{new Vector2(-x+b,-y),new Vector2(x-b,-y),new Vector2(x,-y+b),new Vector2(x,y-b),new Vector2(x-b,y),new Vector2(-x+b,y),new Vector2(-x,y-b),new Vector2(-x,-y+b)};
                Vector3 P(int i,float depth,bool inset){var r=ring[i%8];if(inset)r-=new Vector2(Mathf.Sign(r.x),Mathf.Sign(r.y))*b*.55f;return p+rotation*new Vector3(r.x,r.y,depth);}
                for(int i=0;i<8;i++)
                {
                    var n=new Vector3((ring[i].x+ring[(i+1)%8].x)*.5f,(ring[i].y+ring[(i+1)%8].y)*.5f,0);
                    Face(new[]{P(i,-z+b,false),P(i+1,-z+b,false),P(i+1,z-b,false),P(i,z-b,false)},rotation*n,m);
                    foreach(int sign in new[]{-1,1})Face(new[]{P(i,sign*(z-b),false),P(i+1,sign*(z-b),false),P(i+1,sign*z,true),P(i,sign*z,true)},rotation*new Vector3(n.x,n.y,sign),m);
                }
                foreach(int sign in new[]{-1,1})Face(Enumerable.Range(0,8).Select(i=>P(i,sign*z,true)).ToArray(),rotation*Vector3.forward*sign,m);
            }
            public void Tube(Vector3 p,float back,float front,float length,Material m,Quaternion rotation=default,int sides=12)
            {
                if(rotation==default)rotation=Quaternion.identity;
                Vector3 P(int i,float radius,float z)=>p+rotation*new Vector3(Mathf.Cos(i*Mathf.PI*2/sides)*radius,Mathf.Sin(i*Mathf.PI*2/sides)*radius,z);
                for(int i=0;i<sides;i++)Face(new[]{P(i,back,-length*.5f),P(i+1,back,-length*.5f),P(i+1,front,length*.5f),P(i,front,length*.5f)},rotation*new Vector3(Mathf.Cos((i+.5f)*Mathf.PI*2/sides),Mathf.Sin((i+.5f)*Mathf.PI*2/sides),0),m);
                foreach(int sign in new[]{-1,1}){float r=sign<0?back:front;if(r>.0001f)Face(Enumerable.Range(0,sides).Select(i=>P(i,r,sign*length*.5f)).ToArray(),rotation*Vector3.forward*sign,m);}
            }
            public void Ring(Vector3 p,float outer,float inner,float length,Material m)
            {
                Vector3 P(int i,float r,float z)=>p+new Vector3(Mathf.Cos(i*Mathf.PI/6)*r,Mathf.Sin(i*Mathf.PI/6)*r,z);
                for(int i=0;i<12;i++)
                {
                    var n=new Vector3(Mathf.Cos((i+.5f)*Mathf.PI/6),Mathf.Sin((i+.5f)*Mathf.PI/6),0);
                    foreach(float r in new[]{outer,inner})Face(new[]{P(i,r,-length*.5f),P(i+1,r,-length*.5f),P(i+1,r,length*.5f),P(i,r,length*.5f)},n*(r==outer?1:-1),m);
                    foreach(int sign in new[]{-1,1})Face(new[]{P(i,inner,sign*length*.5f),P(i+1,inner,sign*length*.5f),P(i+1,outer,sign*length*.5f),P(i,outer,sign*length*.5f)},Vector3.forward*sign,m);
                }
            }
            public void Bar(Vector3 a,Vector3 b,float width,Material material)=>Box((a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),material,width*.2f,Quaternion.LookRotation(b-a));
            public void Save(Transform parent,string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=materials.Count;
                for(int i=0;i<materials.Count;i++)mesh.SetTriangles(indices[i],i);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=materials.ToArray();renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
        }
    }
}
