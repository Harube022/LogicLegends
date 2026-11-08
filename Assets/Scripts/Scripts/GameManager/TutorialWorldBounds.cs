using TMPro;
using UnityEngine;

/// <summary>Camera framing uses authored rigid geometry, never transient skinned animation bounds.</summary>
public static class TutorialWorldBounds
{
    public static Bounds Stable(Transform target)
    {
        if(target==null)return new Bounds(Vector3.zero,Vector3.one);
        var bounds=new Bounds(target.position,Vector3.one);bool found=false;
        foreach(var filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=filter.GetComponent<MeshRenderer>();
            if(filter.sharedMesh==null||renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy||filter.GetComponent<TMP_Text>()!=null||filter.GetComponent<TMP_SubMesh>()!=null)continue;
            var local=filter.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                var point=filter.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
}
