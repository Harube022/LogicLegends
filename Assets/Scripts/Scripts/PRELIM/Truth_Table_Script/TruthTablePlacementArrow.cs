using UnityEngine;

/// <summary>The puzzle moves its existing placement target; this visual only follows that exact position.</summary>
public sealed class TruthTablePlacementArrow : MonoBehaviour
{
    public Transform visual;
    [Min(0)] public float height=1.6f;
    [Range(0,.2f)] public float bob=.065f;
    void OnEnable(){if(visual!=null)visual.gameObject.SetActive(true);}
    void OnDisable(){if(visual!=null)visual.gameObject.SetActive(false);}
    void LateUpdate()
    {
        if(visual==null)return;
        visual.position=transform.position+Vector3.up*(height+Mathf.Sin(Time.unscaledTime*2)*bob);
        var camera=Camera.main;if(camera==null)return;
        var direction=camera.transform.position-visual.position;direction.y=0;
        if(direction.sqrMagnitude>.001f)visual.rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
    }
}
