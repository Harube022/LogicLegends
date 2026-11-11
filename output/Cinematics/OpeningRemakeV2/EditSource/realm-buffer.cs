OpeningRemakeV2.Bind();
var s=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Cinematics/OpeningRemakeV2/LogicLegends_Opening_Remake.unity");
var root=s.GetRootGameObjects().Single(r=>r.name=="Logic Realm - Sculpted 3D World");
var mf=root.transform.Find("Raised continents").GetComponent<UnityEngine.MeshFilter>();
var old=mf.sharedMesh;var fresh=new UnityEngine.Mesh();fresh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;fresh.vertices=old.vertices;fresh.normals=old.normals;fresh.colors=old.colors;fresh.triangles=old.triangles;fresh.RecalculateBounds();mf.sharedMesh=fresh;
OpeningRemakeV2.Preview(24,"realm-fresh-buffer",1280,720);
return fresh.vertexCount;
