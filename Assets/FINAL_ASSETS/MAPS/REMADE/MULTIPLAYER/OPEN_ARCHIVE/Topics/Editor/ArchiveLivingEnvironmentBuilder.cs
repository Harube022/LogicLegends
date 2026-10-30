using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveLivingEnvironmentBuilder
    {
        const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/EnvironmentPolish";
        public static ArchiveLivingEnvironment Build(ArchiveTopicSession session,Transform parent,Material foliage,Material glow)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Install ambience in stopped Edit Mode.");
            var previous=parent.Find("Living_Environment");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root=new GameObject("Living_Environment");root.transform.SetParent(parent,false);
            var view=root.AddComponent<ArchiveLivingEnvironment>();view.session=session;view.leafMaterial=foliage;view.fireflyMaterial=glow;
            var map=GameObject.Find("OpenArchive");if(map==null)throw new InvalidOperationException("OpenArchive root is missing.");
            var trees=map.transform.Find("Environment/Trees");if(trees==null)throw new InvalidOperationException("Existing tree group is missing.");
            view.foliageMeshes=trees.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&f.name.StartsWith("Tree_",StringComparison.Ordinal)&&f.name.Contains("_LOD")).ToArray();
            foreach(var filter in view.foliageMeshes)
            {
                // Local vertex wind must remain drawable in a built player. Keep occlusion
                // flags and the existing instanced materials; exclude only static batching.
                var flags=GameObjectUtility.GetStaticEditorFlags(filter.gameObject);
                if((flags&StaticEditorFlags.BatchingStatic)!=0)GameObjectUtility.SetStaticEditorFlags(filter.gameObject,flags&~StaticEditorFlags.BatchingStatic);
                foreach(var material in filter.GetComponent<Renderer>().sharedMaterials)
                    if(material!=null&&!material.enableInstancing)Debug.LogWarning("Open Archive foliage material does not enable GPU instancing: "+material.name);
            }
            // One world-space anchor per grove keeps decorative motes independent of camera movement.
            view.groveCenters=trees.GetComponentsInChildren<LODGroup>(true).Select(g=>{
                var center=g.transform.TransformPoint(g.localReferencePoint);center.y=g.transform.position.y+4.5f;return center;
            }).ToArray();
            if(view.groveCenters.Length==0)view.groveCenters=view.foliageMeshes.Where(f=>f.name.Contains("_LOD0")).Select(f=>{
                var center=f.GetComponent<Renderer>().bounds.center;center.y=f.transform.position.y+4.5f;return center;
            }).ToArray();
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            InstallWindSources(view);
            var mesh=LeafMesh();var motes=new List<Transform>();
            for(int i=0;i<24;i++)
            {
                var go=new GameObject("Grove_Mote_"+(i+1),typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=foliage;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                go.SetActive(false);motes.Add(go.transform);
            }
            view.motes=motes.ToArray();
            InstallAudioAnchors(view);
            EditorUtility.SetDirty(view);return view;
        }
        public static void InstallAudioAnchors(ArchiveLivingEnvironment view)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Author ambience anchors in stopped Edit Mode.");
            var map=GameObject.Find("OpenArchive");if(map==null)throw new InvalidOperationException("OpenArchive root is missing.");
            foreach(var source in view.riverVoices.Concat(view.birdVoices))if(source!=null)UnityEngine.Object.DestroyImmediate(source.gameObject);
            var river=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Ambience/Amb_Mountain_River_1.wav");
            var birds=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/WORLDBGM/ForestBirds.mp3");
            var riverPoints=new List<Vector3>();var waterfalls=map.transform.Find("Environment/Waterfalls");
            if(waterfalls!=null)
                foreach(var renderer in waterfalls.GetComponentsInChildren<Renderer>(true))
                    if(renderer.name.StartsWith("Cliff_Waterfall_",StringComparison.Ordinal))
                    {var position=renderer.bounds.center;position.y=renderer.bounds.max.y-1;riverPoints.Add(position);}
            // A combined cascade mesh has an island-wide AABB whose center is the nest,
            // not a real water location. Use authored waterfall lips and bridge spans.
            var bridges=map.transform.Find("Environment/Bridges");
            if(bridges!=null)
                foreach(var bridge in bridges.Cast<Transform>().Where(t=>t.name.StartsWith("Bridge_Modular",StringComparison.Ordinal)).Take(4))
                {
                    var deck=bridge.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="Wooden_Bridge_Deck");
                    var position=deck!=null?deck.bounds.center:bridge.position;
                    position.y=deck!=null?deck.bounds.min.y-1.5f:bridge.position.y-.7f;riverPoints.Add(position);
                }
            view.riverVoices=Voices(view.transform,"Localized_River",river,Spaced(riverPoints,12,20));
            var birdPoints=new List<Vector3>();
            if(view.groveCenters.Length>0)
            {
                var targets=view.session.stations.Where(s=>s!=null).Select(s=>s.transform.position).ToList();
                var spawn=map.transform.Find("PlayerSpawnArea");targets.Add(spawn!=null?spawn.position:new Vector3(0,0,-205));
                foreach(var target in targets)birdPoints.Add(view.groveCenters.OrderBy(p=>(p-target).sqrMagnitude).First());
            }
            view.birdVoices=Voices(view.transform,"Localized_Day_Birds",birds,Spaced(birdPoints,11,35));
            EditorUtility.SetDirty(view);
        }
        public static void InstallWindSources(ArchiveLivingEnvironment view)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Prepare readable wind sources in stopped Edit Mode.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var originals=view.foliageMeshes.Where(f=>f!=null&&f.sharedMesh!=null).Select(f=>f.sharedMesh).Distinct().ToArray();
            var readable=new List<Mesh>();
            foreach(var original in originals)
            {
                // In Edit Mode Unity permits reading imported mesh data even when the
                // source was uploaded without Read/Write. Persist one readable copy per
                // unique LOD instead of changing hundreds of trees or their source assets.
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out string guid,out long localId);
                string name="Wind_Source_"+original.name+"_"+guid.Substring(0,Mathf.Min(8,guid.Length))+"_"+localId;
                string path=Folder+"/"+name+".asset";
                var copy=new Mesh{name=name,indexFormat=original.indexFormat};
                copy.vertices=original.vertices;
                if(original.HasVertexAttribute(VertexAttribute.Normal))copy.normals=original.normals;
                if(original.HasVertexAttribute(VertexAttribute.Tangent))copy.tangents=original.tangents;
                if(original.HasVertexAttribute(VertexAttribute.Color))copy.colors=original.colors;
                for(int channel=0;channel<8;channel++)
                {
                    var attribute=(VertexAttribute)((int)VertexAttribute.TexCoord0+channel);
                    if(!original.HasVertexAttribute(attribute))continue;
                    int dimensions=original.GetVertexAttributeDimension(attribute);
                    if(dimensions<=2){var values=new List<Vector2>();original.GetUVs(channel,values);copy.SetUVs(channel,values);}
                    else if(dimensions==3){var values=new List<Vector3>();original.GetUVs(channel,values);copy.SetUVs(channel,values);}
                    else{var values=new List<Vector4>();original.GetUVs(channel,values);copy.SetUVs(channel,values);}
                }
                copy.subMeshCount=original.subMeshCount;
                for(int submesh=0;submesh<original.subMeshCount;submesh++)copy.SetIndices(original.GetIndices(submesh),original.GetTopology(submesh),submesh,false);
                copy.bounds=original.bounds;copy.UploadMeshData(false);
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){saved=copy;AssetDatabase.CreateAsset(saved,path);}
                else{EditorUtility.CopySerialized(copy,saved);UnityEngine.Object.DestroyImmediate(copy);}
                EditorUtility.SetDirty(saved);readable.Add(saved);
            }
            view.originalFoliageSources=originals;view.readableFoliageSources=readable.ToArray();EditorUtility.SetDirty(view);
            AssetDatabase.SaveAssets();
        }
        static Vector3[] Spaced(IEnumerable<Vector3> points,int maximum,float spacing)
        {
            var result=new List<Vector3>();
            foreach(var point in points)if(result.All(old=>(point-old).sqrMagnitude>spacing*spacing)){result.Add(point);if(result.Count>=maximum)break;}
            return result.ToArray();
        }
        static AudioSource[] Voices(Transform parent,string name,AudioClip clip,Vector3[] points)
        {
            if(clip==null)return Array.Empty<AudioSource>();
            var result=new List<AudioSource>();
            for(int i=0;i<points.Length;i++)
            {
                var go=new GameObject(name+"_"+(i+1));go.transform.SetParent(parent,false);go.transform.position=points[i];var source=go.AddComponent<AudioSource>();
                source.clip=clip;source.playOnAwake=false;source.loop=true;source.spatialBlend=1;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;
                source.minDistance=5;source.maxDistance=44;source.volume=0;source.priority=235;AudioVolumeSettings.Route(source,GameAudioChannel.SoundFX);result.Add(source);
            }
            return result.ToArray();
        }
        static Mesh LeafMesh()
        {
            string path=Folder+"/Living_Grove_Leaf.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name="Living_Grove_Leaf"};AssetDatabase.CreateAsset(mesh,path);}
            // Two pointed wings, double-sided, shared by every leaf/butterfly/firefly mote.
            mesh.Clear();mesh.vertices=new[]{new Vector3(0,0,-.2f),new Vector3(-1,0,.15f),new Vector3(-.35f,.25f,.55f),new Vector3(0,0,.35f),
                new Vector3(1,0,.15f),new Vector3(.35f,.25f,.55f)};
            mesh.triangles=new[]{0,1,2,0,2,3,0,3,5,0,5,4,2,1,0,3,2,0,5,3,0,4,5,0};mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
    }
}
