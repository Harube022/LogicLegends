using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Shared non-clickable hand bubbles, reconciled against the actual current stations.</summary>
[DisallowMultipleComponent]
public sealed class ProofsWorldInteractionIndicators : MonoBehaviour
{
    ProofsLibraryStageController stage;
    readonly Dictionary<Transform,HandThoughtBubble> bubbles=new Dictionary<Transform,HandThoughtBubble>();
    readonly HashSet<Transform> current=new HashSet<Transform>();
    readonly List<Transform> removed=new List<Transform>();
    public IReadOnlyDictionary<Transform,HandThoughtBubble> Bubbles=>bubbles;
    public void Initialize(ProofsLibraryStageController owner){stage=owner;Refresh();}
    void LateUpdate(){Refresh();}
    public void Refresh()
    {
        if(stage==null)return;
        current.Clear();foreach(var target in stage.InteractionIndicatorTargets)if(target!=null)current.Add(target);
        removed.Clear();
        foreach(var entry in bubbles)if(entry.Key==null||!current.Contains(entry.Key))
        {if(entry.Value!=null)Destroy(entry.Value.gameObject);removed.Add(entry.Key);}
        foreach(var target in removed)bubbles.Remove(target);
        Transform nearby=stage.NearbyIndicatorTarget;
        foreach(var target in current)
        {
            if(!bubbles.TryGetValue(target,out var bubble))
            {bubble=HandThoughtBubble.Create(transform,"Proof Hand Bubble — "+target.name,yellowMarker:true);bubbles.Add(target,bubble);}
            bool available=isActiveAndEnabled&&stage.IsIndicatorInteractionAvailable(target);
            var bounds=VisualBounds(target);
            bubble.Show(available,nearby==target,new Vector3(bounds.center.x,bounds.max.y+.45f,bounds.center.z));
            if(available&&nearby==target)bubble.KeepInView();
        }
    }
    public static Bounds VisualBounds(Transform target)
    {
        var bounds=new Bounds(target.position+Vector3.up*1.5f,Vector3.zero);bool found=false;
        foreach(var renderer in target.GetComponentsInChildren<Renderer>())
        {
            if(!renderer.enabled)continue;
            if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }
    void OnDisable(){foreach(var bubble in bubbles.Values)if(bubble!=null)bubble.Show(false,false,Vector3.zero);}
    void OnDestroy(){foreach(var bubble in bubbles.Values)if(bubble!=null)Destroy(bubble.gameObject);}
}

public sealed partial class ProofsLibraryStageController
{
    public IEnumerable<Transform> InteractionIndicatorTargets
    {
        get
        {
            foreach(var source in scrollSources)if(source!=null)yield return source.transform;
            foreach(var altar in pedestals)if(altar!=null)yield return altar;
            if(worldBoard!=null)
            {
                foreach(var source in worldBoard.SourceStations)if(source!=null)yield return source.transform;
                foreach(var slot in worldBoard.PlacementSlots)if(slot!=null)yield return slot.transform;
            }
            if(Tutorial!=null&&Tutorial.PracticeSource!=null)yield return Tutorial.PracticeSource.transform;
        }
    }
    public Transform NearbyIndicatorTarget
    {
        get
        {
            if(Tutorial!=null)return Tutorial.PracticeSourceAvailable?Tutorial.PracticeSource.transform:null;
            int target=NearestTarget();
            if(target>=1&&target<=4)return scrollSources[target-1].transform;
            if(target>=5&&target<=6)return pedestals[target-5];
            return target==8&&nearestWorldPedestal!=null?nearestWorldPedestal.transform:null;
        }
    }
    public bool IsIndicatorInteractionAvailable(Transform target)
    {
        if(target==null||!target.gameObject.activeInHierarchy||!isActiveAndEnabled||HasReaderOpen||HasCarriedScroll)return false;
        if(Tutorial!=null)return Tutorial.PracticeSourceAvailable&&Tutorial.PracticeSource.transform==target;
        if(!started||finished||transitioning||flashing||InputSuppressed||input==null||input.GameplayInputBlocked||player==null||!player.enabled)return false;
        if(Current.kind==ProofChallengeKind.SortScrolls)
        {
            for(int i=0;i<scrollSources.Length;i++)
                if(scrollSources[i].transform==target)return i<Current.scrolls.Length&&placements[i]<0&&scrolls[i].gameObject.activeInHierarchy;
            for(int i=0;i<pedestals.Length;i++)if(pedestals[i]==target)return CountOnPedestal(i)>0;
            return false;
        }
        var station=target.GetComponent<ProofWorldPedestal>();
        return station!=null&&(worldBoard.TryReadScroll(station,out _,out _)||worldBoard.CanRemoveFromSlot(station));
    }
}
