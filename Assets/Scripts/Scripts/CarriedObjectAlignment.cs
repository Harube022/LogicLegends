using UnityEngine;

/// <summary>Centers the original mesh at the socket, preserving its pickup world scale.</summary>
[DefaultExecutionOrder(250), DisallowMultipleComponent]
public sealed class CarriedObjectAlignment : MonoBehaviour
{
    Transform anchor;
    GrabbableObject grabber;
    Bounds localBounds;
    Vector3 worldScale;
    bool parentedVisual, cached;

    public Vector3 WorldSize
    {
        get
        {
            CacheGeometry();
            var scale=anchor!=null?worldScale:transform.lossyScale;
            return Vector3.Scale(localBounds.size,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
        }
    }
    public float FittedHeight => WorldSize.y; // Compatibility: this is now the unscaled item height.

    public static void Follow(Transform item,Transform socket,bool requireParent=false)
    {
        if(item==null)return;
        var alignment=item.GetComponent<CarriedObjectAlignment>();
        if(socket==null||socket.GetComponentInParent<PlayerCarryRig>()==null)
        {if(alignment!=null)alignment.StopFollowing();return;}
        if(alignment==null)alignment=item.gameObject.AddComponent<CarriedObjectAlignment>();
        if(alignment.anchor==null)alignment.worldScale=item.lossyScale;
        alignment.anchor=socket;alignment.parentedVisual=requireParent;
        alignment.grabber=item.GetComponent<GrabbableObject>();
        alignment.CacheGeometry();alignment.Apply();
    }
    public static void FollowStack(Transform item,Transform socket) => Follow(item,socket);

    void CacheGeometry()
    {
        if(cached)return;cached=true;
        // Old temporary presentations are never used to measure or display an item.
        var legacy=transform.Find("__CarryPresentation");
        if(legacy!=null)legacy.gameObject.SetActive(false);
        bool found=false;localBounds=new Bounds();
        foreach(var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if(renderer.GetComponentInParent<TMPro.TMP_Text>()!=null||renderer is ParticleSystemRenderer||
                legacy!=null&&renderer.transform.IsChildOf(legacy))continue;
            if(legacy!=null)renderer.enabled=true;
            var filter=renderer.GetComponent<MeshFilter>();
            bool meshSpace=filter!=null&&filter.sharedMesh!=null;
            var bounds=meshSpace?filter.sharedMesh.bounds:renderer.bounds;
            for(int i=0;i<8;i++)
            {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var point=transform.InverseTransformPoint(meshSpace?renderer.transform.TransformPoint(corner):corner);
                if(!found){localBounds=new Bounds(point,Vector3.zero);found=true;}else localBounds.Encapsulate(point);
            }
        }
    }
    void PreserveWorldScale()
    {
        var scale=transform.parent!=null?transform.parent.lossyScale:Vector3.one;
        transform.localScale=new Vector3(worldScale.x/Nonzero(scale.x),worldScale.y/Nonzero(scale.y),worldScale.z/Nonzero(scale.z));
    }
    static float Nonzero(float value)=>Mathf.Abs(value)<.00001f?1:value;
    public void Apply()
    {
        if(anchor==null||!gameObject.activeInHierarchy)return;
        if(parentedVisual?!transform.IsChildOf(anchor):grabber==null||!grabber.IsHeld){StopFollowing();return;}
        transform.rotation=anchor.rotation;
        PreserveWorldScale();
        transform.position=anchor.position-transform.TransformVector(localBounds.center);
    }
    public void Restore(){if(anchor!=null)PreserveWorldScale();}
    public void StopFollowing(){Restore();anchor=null;}
    void OnDisable(){StopFollowing();}
    void LateUpdate(){Apply();}
}
