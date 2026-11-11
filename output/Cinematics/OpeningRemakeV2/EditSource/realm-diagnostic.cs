OpeningRemakeV2.Bind();
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Cinematics/OpeningRemakeV2/LogicLegends_Opening_Remake.unity");
var root=scene.GetRootGameObjects().Single(r=>r.name=="Logic Realm - Sculpted 3D World");
var land=root.transform.Find("Raised continents").GetComponent<UnityEngine.MeshFilter>().sharedMesh;
var verts=land.vertices;var tris=land.triangles;int count=0,up=0,down=0;float low=10000,high=-10000;
for(int i=0;i<tris.Length;i+=3){var a=verts[tris[i]];var b=verts[tris[i+1]];var c=verts[tris[i+2]];float n=UnityEngine.Vector3.Cross(b-a,c-a).y;if(n>0)up++;else down++;if(a.x>50&&a.x<150&&a.z>-50&&a.z<50){count++;low=UnityEngine.Mathf.Min(low,a.y);high=UnityEngine.Mathf.Max(high,a.y);}}
root.transform.Find("Animated three dimensional ocean").gameObject.SetActive(false);
OpeningRemakeV2.Preview(24,"realm-diagnostic-no-ocean",1280,720);
root.transform.Find("Animated three dimensional ocean").gameObject.SetActive(true);
return new {vertices=verts.Length,triangles=tris.Length/3,up,down,centralTriangles=count,low,high};
