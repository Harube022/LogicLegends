using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveItemAssets
    {
        public const string Folder=ArchiveTopicSetup.Folder+"/Art/Rewards/Items";
        public static void Install()
        {
            if(Application.isPlaying||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Use stopped saved scene.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var view=UnityEngine.Object.FindFirstObjectByType<ArchiveRewardView>();if(view==null)throw new Exception("Reward view missing");
            view.itemPrefabs=new GameObject[6];view.itemIcons=new Sprite[6];
            Material dark=Mat("Graphite",new Color(.045f,.07f,.085f)),metal=Mat("Brushed_Metal",new Color(.45f,.55f,.59f)),pale=Mat("Ivory",new Color(.9f,.94f,.84f));
            Material teal=Mat("Teal",new Color(.08f,.55f,.58f)),amber=Mat("Amber",new Color(1,.55f,.10f)),red=Mat("Medical_Red",new Color(.65f,.08f,.13f)),green=Mat("Revive_Green",new Color(.08f,.52f,.29f));
            GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 size,Material material,Vector3 euler=default)
            {var p=GameObject.CreatePrimitive(type);p.name=name;p.transform.SetParent(parent,false);p.transform.localPosition=pos;p.transform.localScale=size;p.transform.localEulerAngles=euler;p.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());return p;}
            for(int i=0;i<6;i++)
            {
                string file=Folder+"/Supply_"+((ArchiveSurvivalItem)i)+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if(prefab==null)
                {
                    var root=new GameObject("Supply_"+((ArchiveSurvivalItem)i));
                    try
                    {
                        var t=root.transform;
                        if(i==0)
                        {
                            Part(t,"Rubber_Grip",PrimitiveType.Cylinder,new Vector3(0,0,.2f),new Vector3(.23f,.25f,.23f),dark,new Vector3(90,0,0));
                            Part(t,"Lamp_Head",PrimitiveType.Cylinder,new Vector3(0,0,.54f),new Vector3(.42f,.14f,.42f),metal,new Vector3(90,0,0));
                            Part(t,"Lens",PrimitiveType.Cylinder,new Vector3(0,0,.69f),new Vector3(.34f,.015f,.34f),pale,new Vector3(90,0,0));
                            Part(t,"Power_Button",PrimitiveType.Cube,new Vector3(0,.13f,.2f),new Vector3(.10f,.025f,.12f),amber);
                            for(int n=0;n<3;n++)Part(t,"Grip_Ring_"+n,PrimitiveType.Cylinder,new Vector3(0,0,.02f+n*.13f),new Vector3(.26f,.02f,.26f),metal,new Vector3(90,0,0));
                        }
                        else if(i==1)
                        {
                            Part(t,"Battery_Cell",PrimitiveType.Cylinder,new Vector3(0,.15f,0),new Vector3(.3f,.24f,.3f),dark);
                            Part(t,"Copper_Cap",PrimitiveType.Cylinder,new Vector3(0,.38f,0),new Vector3(.30f,.06f,.30f),amber);
                            Part(t,"Positive_Terminal",PrimitiveType.Cylinder,new Vector3(0,.46f,0),new Vector3(.13f,.025f,.13f),metal);
                            Part(t,"Charge_Stripe",PrimitiveType.Cube,new Vector3(0,.16f,.151f),new Vector3(.13f,.27f,.02f),teal);
                        }
                        else if(i==2||i==3)
                        {
                            var color=i==2?red:green;
                            Part(t,"Soft_Case",PrimitiveType.Cube,new Vector3(0,.04f,.15f),new Vector3(.68f,.52f,.30f),color);
                            Part(t,"Case_Band",PrimitiveType.Cube,new Vector3(0,.04f,.31f),new Vector3(.12f,.52f,.03f),metal);
                            Part(t,"Carry_Handle_Top",PrimitiveType.Cube,new Vector3(0,.39f,.15f),new Vector3(.34f,.065f,.08f),dark);
                            foreach(int sign in new[]{-1,1})Part(t,"Handle_Riser",PrimitiveType.Cube,new Vector3(sign*.145f,.31f,.15f),new Vector3(.06f,.13f,.08f),dark);
                            Part(t,"Aid_Symbol_H",PrimitiveType.Cube,new Vector3(0,.05f,.34f),new Vector3(.33f,.10f,.045f),pale);
                            Part(t,"Aid_Symbol_V",PrimitiveType.Cube,new Vector3(0,.05f,.34f),new Vector3(.10f,.33f,.045f),pale);
                            if(i==3)foreach(int sign in new[]{-1,1})Part(t,"Revival_Ampoule",PrimitiveType.Cylinder,new Vector3(sign*.42f,.02f,.15f),new Vector3(.12f,.21f,.12f),pale);
                        }
                        else if(i==4)
                        {
                            Part(t,"Pistol_Grip",PrimitiveType.Cube,new Vector3(0,-.16f,.02f),new Vector3(.22f,.43f,.24f),dark,new Vector3(-12,0,0));
                            Part(t,"Receiver",PrimitiveType.Cube,new Vector3(0,.11f,.22f),new Vector3(.30f,.25f,.65f),teal);
                            Part(t,"Dart_Barrel",PrimitiveType.Cylinder,new Vector3(0,.12f,.71f),new Vector3(.15f,.32f,.15f),metal,new Vector3(90,0,0));
                            Part(t,"Muzzle_Cap",PrimitiveType.Cylinder,new Vector3(0,.12f,1.02f),new Vector3(.19f,.04f,.19f),dark,new Vector3(90,0,0));
                            Part(t,"Sight",PrimitiveType.Cube,new Vector3(0,.28f,.38f),new Vector3(.08f,.08f,.28f),amber);
                            Part(t,"Trigger_Guard",PrimitiveType.Cube,new Vector3(0,-.09f,.31f),new Vector3(.1f,.055f,.29f),metal);
                        }
                        else
                        {
                            Part(t,"Dart_Holder",PrimitiveType.Cube,new Vector3(0,-.02f,.13f),new Vector3(.53f,.14f,.22f),dark);
                            for(int n=0;n<3;n++)
                            {
                                float x=(n-1)*.18f;Part(t,"Dart_Shaft_"+n,PrimitiveType.Cylinder,new Vector3(x,.17f,.13f),new Vector3(.07f,.27f,.07f),metal);
                                Part(t,"Dart_Fins_"+n,PrimitiveType.Cube,new Vector3(x,-.02f,.13f),new Vector3(.13f,.16f,.05f),teal);
                                Part(t,"Dart_Cap_"+n,PrimitiveType.Cylinder,new Vector3(x,.46f,.13f),new Vector3(.08f,.04f,.08f),amber);
                            }
                        }
                        prefab=PrefabUtility.SaveAsPrefabAsset(root,file);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(root);}
                }
                view.itemPrefabs[i]=prefab;
                string image=Folder+"/Icon_"+((ArchiveSurvivalItem)i)+".png";if(!File.Exists(image))RenderIcon(prefab,image);
                view.itemIcons[i]=AssetDatabase.LoadAssetAtPath<Sprite>(image);
            }
            string beamPath=Folder+"/Flashlight_Beam.mat";
            var beam=AssetDatabase.LoadAssetAtPath<Material>(beamPath);
            if(beam==null)
            {
                var shader=Shader.Find("LogicLegends/OpenArchive/Flashlight");
                if(shader==null)throw new InvalidOperationException("Import the Open Archive flashlight shader before installing item assets.");
                beam=new Material(shader){name="Flashlight_Beam",enableInstancing=true};
                AssetDatabase.CreateAsset(beam,beamPath);
            }
            view.flashlightBeamMaterial=beam;
            var equipped=view.session.GetComponent<ArchiveEquippedItems>();if(equipped==null)equipped=view.session.gameObject.AddComponent<ArchiveEquippedItems>();equipped.session=view.session;equipped.view=view;EditorUtility.SetDirty(equipped);
            var down=view.session.GetComponent<ArchiveDownedPresentation>();if(down==null)down=view.session.gameObject.AddComponent<ArchiveDownedPresentation>();down.session=view.session;
            EditorUtility.SetDirty(view);EditorUtility.SetDirty(down);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        /// <summary>Refines the existing torch in place; keeps the authored grip, lens and asset GUIDs.</summary>
        public static void UpgradeFlashlight()
        {
            if(Application.isPlaying||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Use stopped saved scene.");
            string path=Folder+"/Supply_Flashlight.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)throw new InvalidOperationException("Install the existing item assets first.");
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var dark=Mat("Graphite",new Color(.045f,.07f,.085f));
                var metal=Mat("Brushed_Metal",new Color(.45f,.55f,.59f));
                var pale=Mat("Ivory",new Color(.9f,.94f,.84f));
                var amber=Mat("Amber",new Color(1,.55f,.10f));
                void Part(string name,PrimitiveType type,Vector3 position,Vector3 size,Material material)
                {
                    var child=root.transform.Find(name);
                    if(child==null)
                    {
                        var part=GameObject.CreatePrimitive(type);part.name=name;
                        part.transform.SetParent(root.transform,false);child=part.transform;
                    }
                    child.localPosition=position;child.localScale=size;
                    child.localEulerAngles=type==PrimitiveType.Cylinder?new Vector3(90,0,0):Vector3.zero;
                    child.GetComponent<Renderer>().sharedMaterial=material;
                    foreach(var collider in child.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(collider);
                }
                // Long narrow handle and stepped reflector distinguish the torch from a camera lens.
                Part("Rubber_Grip",PrimitiveType.Cylinder,new Vector3(0,0,.03f),new Vector3(.175f,.34f,.175f),dark);
                Part("Tail_Cap",PrimitiveType.Cylinder,new Vector3(0,0,-.32f),new Vector3(.205f,.035f,.205f),metal);
                Part("Lamp_Neck",PrimitiveType.Cylinder,new Vector3(0,0,.42f),new Vector3(.195f,.08f,.195f),dark);
                Part("Lamp_Head",PrimitiveType.Cylinder,new Vector3(0,0,.57f),new Vector3(.30f,.075f,.30f),metal);
                Part("Reflector_Shoulder",PrimitiveType.Cylinder,new Vector3(0,0,.50f),new Vector3(.245f,.028f,.245f),dark);
                Part("Protective_Bezel",PrimitiveType.Cylinder,new Vector3(0,0,.65f),new Vector3(.34f,.025f,.34f),dark);
                Part("Lens_Rim",PrimitiveType.Cylinder,new Vector3(0,0,.678f),new Vector3(.29f,.008f,.29f),metal);
                Part("Lens",PrimitiveType.Cylinder,new Vector3(0,0,.69f),new Vector3(.255f,.006f,.255f),pale);
                Part("Power_Button",PrimitiveType.Cube,new Vector3(0,.105f,.28f),new Vector3(.07f,.035f,.105f),amber);
                for(int n=0;n<5;n++)Part("Grip_Ring_"+n,PrimitiveType.Cylinder,new Vector3(0,0,-.22f+n*.105f),new Vector3(.195f,.012f,.195f),dark);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            RenderIcon(AssetDatabase.LoadAssetAtPath<GameObject>(path),Folder+"/Icon_Flashlight.png",new Vector3(3.5f,2.1f,1.6f));
            AssetDatabase.SaveAssets();
        }
        static Material Mat(string name,Color color)
        {string path=Folder+"/Item_"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Item_"+name,enableInstancing=true};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.28f);AssetDatabase.CreateAsset(m,path);return m;}
        static void RenderIcon(GameObject prefab,string path,Vector3 cameraOffset=default)
        {
            var preview=EditorSceneManager.NewPreviewScene();var root=UnityEngine.Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(root,preview);
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var camGo=new GameObject("Item_Icon_Camera");SceneManager.MoveGameObjectToScene(camGo,preview);var cam=camGo.AddComponent<Camera>();cam.scene=preview;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.orthographic=true;cam.orthographicSize=.85f;
            var bounds=new Bounds();bool first=true;foreach(var r in root.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            cam.orthographicSize=Mathf.Max(bounds.extents.magnitude*.95f,.55f);var offset=cameraOffset==default?new Vector3(2,1.3f,3):cameraOffset;cam.transform.SetPositionAndRotation(bounds.center+offset,Quaternion.LookRotation(-offset));
            var lightGo=new GameObject("Icon_Key_Light");SceneManager.MoveGameObjectToScene(lightGo,preview);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.7f;light.transform.rotation=Quaternion.Euler(40,-25,0);light.cullingMask=1<<31;
            var rt=RenderTexture.GetTemporary(192,192,24);var old=RenderTexture.active;var texture=new Texture2D(192,192,TextureFormat.RGBA32,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,192,192),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{RenderTexture.active=old;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(texture);EditorSceneManager.ClosePreviewScene(preview);}
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=256;importer.SaveAndReimport();
        }
    }
}
