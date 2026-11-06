using System.Collections.Generic;
using UnityEngine;

/// <summary>One socket per equipped identity. Unequipped blocks stay safely stored.</summary>
[DefaultExecutionOrder(180), DisallowMultipleComponent]
public sealed class TruthTableCarryStack : MonoBehaviour
{
    sealed class Entry { public TruthBlock block; public Transform socket; public Vector3 velocity; public bool positioned; public UnityEngine.SceneManagement.Scene scene; }
    readonly List<Entry> entries=new List<Entry>();
    InventoryManager inventory;
    public int Count=>entries.Count;
    public CarriedObjectAlignment BottomItem => entries.Count>0&&entries[0].block!=null?entries[0].block.GetComponent<CarriedObjectAlignment>():null;
    public void Synchronize(InventoryManager owner,IReadOnlyList<TruthBlock> blocks)
    {
        inventory=owner;
        for(int i=entries.Count-1;i>=0;i--)if(entries[i].block==null||!owner.IsEquipped(entries[i].block))RemoveAt(i);
        var player=GetComponent<Player>();if(player==null||player.HoldPoint==null)return;
        for(int i=0;i<blocks.Count;i++)
        {
            var block=blocks[i];Entry entry=entries.Find(e=>e.block==block);
            if(!owner.IsEquipped(block))
            {
                block.GetComponent<GrabbableObject>().ConfigureInventoryState(false,null,true);
                block.gameObject.SetActive(false);
                continue;
            }
            if(entry==null)
            {
                var socket=new GameObject("Truth Block Stack Socket").transform;socket.SetParent(player.HoldPoint,false);
                socket.localPosition=Vector3.zero;
                entry=new Entry{block=block,socket=socket,scene=block.gameObject.scene};entries.Add(entry);
                block.gameObject.SetActive(true);block.transform.SetParent(socket,true);
                block.GetComponent<GrabbableObject>().ConfigureInventoryState(true,socket,true);
            }
            CarriedObjectAlignment.FollowStack(block.transform,entry.socket);
        }
    }
    void LateUpdate()
    {
        if(inventory==null){Clear();return;}
        float y=0,previousHeight=0;
        for(int i=0;i<entries.Count;i++)
        {
            var e=entries[i];if(e.block==null||e.socket==null)continue;
            var alignment=e.block.GetComponent<CarriedObjectAlignment>();float height=alignment!=null?alignment.WorldSize.y:1;
            if(i>0)y+=(previousHeight+height)*.5f+inventory.StackGap;
            var target=Vector3.up*y;previousHeight=height;
            if(!e.positioned){e.socket.localPosition=target;e.positioned=true;}
            else if(Time.deltaTime>0)e.socket.localPosition=Vector3.SmoothDamp(e.socket.localPosition,target,ref e.velocity,inventory.CompactionSeconds,Mathf.Infinity,Time.deltaTime);
        }
    }
    public void Release(TruthBlock block)
    { for(int i=entries.Count-1;i>=0;i--)if(entries[i].block==block)RemoveAt(i); }
    void RemoveAt(int index)
    {
        var e=entries[index];
        if(e.block!=null)
        {
            e.block.GetComponent<CarriedObjectAlignment>()?.StopFollowing();
            if(e.socket!=null&&e.block.transform.parent==e.socket)e.block.transform.SetParent(null,true);
            if(e.block.transform.parent==null&&e.scene.IsValid()&&e.scene.isLoaded&&e.block.gameObject.scene!=e.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(e.block.gameObject,e.scene);
        }
        if(e.socket!=null){e.socket.gameObject.SetActive(false);Destroy(e.socket.gameObject);}entries.RemoveAt(index);
    }
    public void Clear(){for(int i=entries.Count-1;i>=0;i--)RemoveAt(i);inventory=null;}
    void OnDestroy(){Clear();}
}

