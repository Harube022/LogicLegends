using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Open, low-profile activity kit. Displays the shared state; owns no puzzle progress.</summary>
    public sealed class ArchiveTopicStation : MonoBehaviour
    {
        [Range(0,9)] public int topic;
        public ArchiveTopicSession session;
        public Transform workRoot;
        public TMP_Text title, board;
        public TMP_FontAsset font;
        public Material labelMaterial;
        public GameObject tokenPrefab, crystalPrefab, padPrefab;
        public Material energyMaterial, neutralMaterial, successMaterial;
        public static readonly Vector3 BookPosition=new Vector3(0,0,8.5f);
        public static readonly Vector2 GraphOffset=new Vector2(0,-1.5f);
        public readonly List<ArchiveTopicPoint> points=new List<ArchiveTopicPoint>();
        readonly Dictionary<int,Transform> tokens=new Dictionary<int,Transform>();
        readonly Dictionary<int,Transform> reusableCarries=new Dictionary<int,Transform>();
        readonly List<GameObject> reusablePlacements=new List<GameObject>();
        readonly List<LineRenderer> lines=new List<LineRenderer>();
        string assemblyKey;
        int shownRevision=-1;
        ArchiveQuestion question;
        void Start()
        {
            var book=new GameObject("Book_Interaction").AddComponent<ArchiveTopicPoint>();book.transform.SetParent(transform,false);book.transform.localPosition=BookPosition;
            book.station=this;book.role=ArchivePointRole.Book;book.caption="Read / start "+ArchiveTopicCatalog.Names[topic];points.Add(book);
            foreach(var text in new[]{title,board})if(text!=null){var bounds=text.gameObject.AddComponent<ArchiveWorldLabel>();bounds.size=text.rectTransform.rect.size;bounds.floating=false;}
        }
        void LateUpdate()
        {
            if(session==null||!session.Ready)return;
            var state=session.State.stations[topic];
            string key=state.testResetSerial+":"+state.round+":"+state.subphase+":"+state.phase;
            if(topic>=0&&topic<=9)key=session.State.seed+":"+key;
            if(key!=assemblyKey){assemblyKey=key;Rebuild(state);shownRevision=-1;}
            if(shownRevision!=state.revision){shownRevision=state.revision;RefreshWork(state);}
            foreach(var held in state.carries)
                if((question.reusableItems?reusableCarries:tokens).TryGetValue(question.reusableItems?held.actor:held.item,out var token))
                {
                    var player=session.ActorPlayer(held.actor);
                    if(player!=null){token.position=player.transform.position+Vector3.up*2.2f+player.transform.forward*1.25f;token.rotation=player.transform.rotation;}
                }
        }
        void Rebuild(ArchiveStationState state)
        {
            foreach(Transform child in workRoot){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            points.RemoveAll(p=>p==null||p.role!=ArchivePointRole.Book);tokens.Clear();lines.Clear();reusableCarries.Clear();reusablePlacements.Clear();
            title.text="T"+(topic+1)+"  "+ArchiveTopicCatalog.Names[topic]+"\n"+ArchiveTopicCatalog.Arenas[topic];
            question=session.Question(topic);
            var tree=topic==9?GetComponent<ArchiveTreePresentation>():null;
            if(tree!=null){tree.Build(this,state,question);return;}
            var graph=topic==8?GetComponent<ArchiveGraphPresentation>():null;
            if(graph!=null){graph.Build(this,state,question);return;}
            var mapping=topic==7?GetComponent<ArchiveMappingPresentation>():null;
            if(mapping!=null){mapping.Build(this,state,question);return;}
            var sets=topic==6?GetComponent<ArchiveSetsPresentation>():null;
            if(sets!=null){sets.Build(this,state,question);return;}
            var induction=topic==5?GetComponent<ArchiveInductionPresentation>():null;
            if(induction!=null){induction.Build(this,state,question);return;}
            var methods=topic==4?GetComponent<ArchiveMethodPresentation>():null;
            if(methods!=null){methods.Build(this,state,question);return;}
            var proofs=topic==3?GetComponent<ArchiveProofPresentation>():null;
            if(proofs!=null){proofs.Build(this,state,question);return;}
            var inference=topic==2?GetComponent<ArchiveInferencePresentation>():null;
            if(inference!=null){inference.Build(this,state,question);return;}
            var truth=topic==1?GetComponent<ArchiveTruthPresentation>():null;
            if(truth!=null){truth.Build(this,state,question);return;}
            var proposition=topic==0?GetComponent<ArchivePropositionPresentation>():null;
            if(proposition!=null)proposition.BuildRunes(this,state,question);
            if(topic==6)
            {
                bool independent=question.slots.Length==5,complement=question.slots.Length==2;
                var a=transform.Find("Set_A_Region");var b=transform.Find("Set_B_Region");
                a.gameObject.SetActive(!independent);b.gameObject.SetActive(!independent&&!complement);
                a.localPosition=new Vector3(complement?-3.4f:-2.2f,.038f,-1.3f);
            }
            if(state.phase!=1){board.text=state.timerExpired?"TIME UP\nSolved challenges kept.\nRead the Book to retry.":state.phase==2?"TOPIC COMPLETE\nShared team success":string.IsNullOrEmpty(state.reveal)?"BOOK STATUE\n"+ArchiveTopicCatalog.Arenas[topic]+"\nInteract to begin":"Correct!\n"+state.reveal+"\nUse the book to continue.";return;}
            for(int i=0;i<question.items.Length&&proposition==null;i++)
            {
                bool choice=question.activity==ArchiveActivity.Choice;
                var item=Instantiate(choice?padPrefab:topic==2&&state.subphase==1?crystalPrefab:tokenPrefab,workRoot);
                item.name=(choice?"Answer_Rune_":"Topic_Item_")+i;item.transform.localPosition=ItemPosition(i,question);
                var point=item.AddComponent<ArchiveTopicPoint>();point.station=this;point.index=i;point.role=choice?ArchivePointRole.Choice:ArchivePointRole.Item;
                point.caption=choice?"Channel answer: "+question.items[i]:"Carry "+question.items[i];points.Add(point);tokens[i]=item.transform;
                Label(item.transform,question.items[i],new Vector3(0,choice?.65f:1.05f,0),new Vector2(choice?3.3f:2.0f,1.15f),choice?7:topic==6?4:6);
                if(!choice)
                {
                    var stand=Instantiate(padPrefab,workRoot);stand.name="Item_Stand_"+i;
                    var p=ItemPosition(i,question);p.y=.015f;stand.transform.localPosition=p;stand.transform.localScale=new Vector3(.6f,6,.6f);
                }
            }
            for(int i=0;i<question.slots.Length;i++)
                Pad("Answer_Socket_"+i,SlotPosition(i,question),question.slots[i],ArchivePointRole.Slot,i,question.regionPlacement?new Vector3(1.7f,1,1.7f):Vector3.one);
            if(question.activity==ArchiveActivity.Method)
                for(int i=0;i<3;i++)Pad("Proof_Method_"+i,new Vector3((i-1)*3.6f,0,4.4f),new[]{"Direct","Contrapositive","Contradiction"}[i],ArchivePointRole.Select,i,Vector3.one);
            if(question.mapping!=null)
            {
                for(int i=0;i<question.inputLabels.Length;i++)Pad("Domain_"+i,InputPosition(i),"Input "+question.inputLabels[i],ArchivePointRole.Input,i,Vector3.one);
                for(int i=0;i<question.outputLabels.Length;i++)Pad("Range_"+i,OutputPosition(i),"Output "+question.outputLabels[i],ArchivePointRole.Output,i,Vector3.one);
                if(question.mapping.questionKind==MappingQuestionKind.ClassifyFunction)
                    for(int i=0;i<3;i++)Pad("Mapping_Class_"+i,ArchiveMappingPresentation.AnswerPosition(i),ArchiveMappingPresentation.ClassificationNames[i],ArchivePointRole.Select,i,Vector3.one);
                if(question.mapping.questionKind==MappingQuestionKind.SelectProperties)
                    for(int i=0;i<3;i++)Pad("Mapping_Property_"+i,new Vector3((i-1)*3.6f,0,-6),new[]{"Injective","Surjective","Bijective"}[i],ArchivePointRole.Select,new[]{2,4,8}[i],Vector3.one);
            }
            if(question.graph!=null||question.tree!=null)
            {
                for(int i=0;i<question.nodes.Length;i++)
                {
                    var p=question.nodes[i]+GraphOffset;
                    var obj=Pad("Vertex_"+question.nodeIds[i],new Vector3(p.x,0,p.y),question.nodeIds[i],ArchivePointRole.Node,i,Vector3.one*1.1f);
                    if(question.graph!=null)obj.GetComponent<ArchiveTopicPoint>().caption="Route vertex "+question.nodeIds[i];
                }
            }
            if((question.graph!=null&&question.graph.type==LogicLegends.Graph.GraphChallengeType.Degree)||(question.tree!=null&&(question.tree.type==LogicLegends.Trees.TreeChallengeType.Depth||question.tree.type==LogicLegends.Trees.TreeChallengeType.Height)))
                for(int i=0;i<=10;i++)Pad("Number_"+i,new Vector3(i<6?-8.4f:8.4f,0,-5.6f+(i%6)*2),i.ToString(),ArchivePointRole.Select,question.tree!=null?100+i:i,Vector3.one*.7f);
        }
        GameObject Pad(string name,Vector3 position,string text,ArchivePointRole role,int index,Vector3 scale)
        {
            var obj=Instantiate(padPrefab,workRoot);obj.name=name;obj.transform.localPosition=position;obj.transform.localScale=scale;
            var point=obj.AddComponent<ArchiveTopicPoint>();point.station=this;point.role=role;point.index=index;point.caption="Interact: "+text;points.Add(point);
            var labelAnchor=new GameObject(name+"_Label_Anchor").transform;labelAnchor.SetParent(workRoot,false);labelAnchor.localPosition=position;
            Label(labelAnchor,text,new Vector3(0,.65f,0),new Vector2(2.5f,1.15f),6);labelAnchor.GetComponentInChildren<ArchiveWorldLabel>().point=point;return obj;
        }
        void Label(Transform parent,string value,Vector3 position,Vector2 size,float fontSize)
        {
            var label=new GameObject("Readable_Label").AddComponent<TextMeshPro>();label.transform.SetParent(parent,false);label.transform.localPosition=position;
            label.font=font;if(labelMaterial!=null)label.fontSharedMaterial=labelMaterial;label.text=value;label.fontSize=fontSize;label.alignment=TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta=size;label.enableAutoSizing=true;label.fontSizeMin=3;label.fontSizeMax=fontSize;
            label.color=new Color(1,.94f,.78f);label.raycastTarget=false;
            label.gameObject.AddComponent<ArchiveTopicLabelFacing>();
            var bounds=label.gameObject.AddComponent<ArchiveWorldLabel>();bounds.size=size;bounds.point=parent.GetComponent<ArchiveTopicPoint>();
        }
        void RefreshWork(ArchiveStationState state)
        {
            var tree=topic==9?GetComponent<ArchiveTreePresentation>():null;
            if(tree!=null){tree.Refresh(state);return;}
            var graph=topic==8?GetComponent<ArchiveGraphPresentation>():null;
            if(graph!=null){graph.Refresh(state);return;}
            var mapping=topic==7?GetComponent<ArchiveMappingPresentation>():null;
            if(mapping!=null){mapping.Refresh(state);return;}
            var sets=topic==6?GetComponent<ArchiveSetsPresentation>():null;
            if(sets!=null){sets.Refresh(state);return;}
            var induction=topic==5?GetComponent<ArchiveInductionPresentation>():null;
            if(induction!=null){induction.Refresh(state);return;}
            var methods=topic==4?GetComponent<ArchiveMethodPresentation>():null;
            if(methods!=null){methods.Refresh(state);return;}
            var proofs=topic==3?GetComponent<ArchiveProofPresentation>():null;
            if(proofs!=null){proofs.Refresh(state);return;}
            var inference=topic==2?GetComponent<ArchiveInferencePresentation>():null;
            if(inference!=null){inference.Refresh(state);return;}
            var truth=topic==1?GetComponent<ArchiveTruthPresentation>():null;
            if(truth!=null){truth.Refresh(state);return;}
            if(state.phase!=1)return;
            board.text=(question.title+"\n"+question.prompt).Replace("\u22EF","...");
            foreach(var pair in tokens)
            {
                if(!question.reusableItems&&state.carries.Any(c=>c.item==pair.Key))continue;
                pair.Value.localPosition=CurrentItemPosition(pair.Key,question,state);pair.Value.localRotation=Quaternion.identity;
            }
            foreach(var obj in reusablePlacements){if(obj!=null){obj.SetActive(false);Destroy(obj);}}reusablePlacements.Clear();
            foreach(var obj in reusableCarries.Values){if(obj!=null){obj.gameObject.SetActive(false);Destroy(obj.gameObject);}}reusableCarries.Clear();
            if(question.reusableItems)
            {
                for(int slot=0;slot<state.placements.Length;slot++)if(state.placements[slot]>=0)
                {
                    var copy=Instantiate(tokenPrefab,workRoot);copy.name="Placed_Truth_Token_"+slot;copy.transform.localPosition=SlotPosition(slot,question)+Vector3.up*.18f;
                    Label(copy.transform,question.items[state.placements[slot]],Vector3.up*.8f,new Vector2(2,1),6);reusablePlacements.Add(copy);
                }
                foreach(var held in state.carries)
                {
                    var copy=Instantiate(tokenPrefab,workRoot);copy.name="Carried_Truth_Token_Actor_"+held.actor;
                    Label(copy.transform,question.items[held.item],Vector3.up*.8f,new Vector2(2,1),6);reusableCarries[held.actor]=copy.transform;
                }
            }
            foreach(var line in lines)if(line!=null)Destroy(line.gameObject);lines.Clear();
            if(question.mapping!=null)
                foreach(int link in state.links)Line(InputPosition(link/100)+Vector3.up*.75f,OutputPosition(link%100)+Vector3.up*.75f,false);
            if(question.graph!=null)
                foreach(var edge in question.graph.edges.Where(e=>e.traversalEnabled))
                    LinkNodes(Array.IndexOf(question.nodeIds,edge.source),Array.IndexOf(question.nodeIds,edge.destination),edge.directed);
            if(question.tree!=null)foreach(int link in state.links)LinkNodes(link/100,link%100,question.tree.Directed);
        }
        void LinkNodes(int a,int b,bool directed)
        {
            var from=question.nodes[a]+GraphOffset;var to=question.nodes[b]+GraphOffset;
            Line(new Vector3(from.x,.12f,from.y),new Vector3(to.x,.12f,to.y),directed);
        }
        void Line(Vector3 a,Vector3 b,bool directed)
        {
            var line=new GameObject("Mathematical_Connection").AddComponent<LineRenderer>();line.transform.SetParent(workRoot,false);line.useWorldSpace=false;
            line.sharedMaterial=energyMaterial;line.startWidth=line.endWidth=.08f;line.positionCount=2;line.SetPositions(new[]{a,b});line.numCapVertices=2;
            line.startColor=line.endColor=new Color(.18f,.66f,.65f);lines.Add(line);
            if(directed)
            {
                var direction=(b-a).normalized;var center=Vector3.Lerp(a,b,.62f);var side=Vector3.Cross(Vector3.up,direction)*.32f;
                var arrow=new GameObject("Direction_Arrow").AddComponent<LineRenderer>();arrow.transform.SetParent(workRoot,false);arrow.useWorldSpace=false;arrow.sharedMaterial=energyMaterial;
                arrow.startWidth=arrow.endWidth=.09f;arrow.positionCount=3;arrow.SetPositions(new[]{center-direction*.5f+side,center,center-direction*.5f-side});lines.Add(arrow);
            }
        }
        public Vector3 ItemPosition(int index,ArchiveQuestion q)
        {
            if(topic==6)return ArchiveSetsPresentation.TokenPosition(index,q);
            if(topic==5)return ArchiveInductionPresentation.TokenPosition(index,q);
            if(topic==4)return ArchiveMethodPresentation.TokenPosition(index,q);
            if(topic==3)return ArchiveProofPresentation.TokenPosition(index,q);
            if(topic==2)return ArchiveInferencePresentation.TokenPosition(index,q);
            if(q.activity==ArchiveActivity.Truth)return ArchiveTruthPresentation.TokenPosition(index);
            if(q.activity==ArchiveActivity.Choice)return new[]{new Vector3(-3.8f,.08f,-3.8f),new Vector3(3.8f,.08f,-3.8f),new Vector3(-3.8f,.08f,3),new Vector3(3.8f,.08f,3)}[index];
            if(q.itemOrder.Length==q.items.Length)index=Array.IndexOf(q.itemOrder,index);
            int count=(q.items.Length+1)/2;int row=index%count;
            return new Vector3(index<count?-9:9,.65f,count<=1?0:Mathf.Lerp(-5.8f,5.8f,row/(float)(count-1)));
        }
        public Vector3 CurrentItemPosition(int index,ArchiveQuestion q,ArchiveStationState s)
        {
            if(topic==6)return ArchiveSetsPresentation.CurrentPosition(index,q,s);
            if(topic==3)return ArchiveProofPresentation.CurrentPosition(index,q,s);
            if(q.reusableItems)return ItemPosition(index,q);
            int slot=q.regionPlacement?(index<s.placements.Length?s.placements[index]:-1):Array.IndexOf(s.placements,index);
            if(slot<0)return ItemPosition(index,q);
            var position=SlotPosition(slot,q)+Vector3.up*.18f;
            if(q.regionPlacement)
            {
                int order=s.placements.Take(index).Count(v=>v==slot);position+=new Vector3((order%4-1.5f)*.8f,0,(order/4)*.8f);
            }
            return position;
        }
        public Vector3 SlotPosition(int index,ArchiveQuestion q)
        {
            if(topic==6)return ArchiveSetsPresentation.RegionPosition(index,q);
            if(topic==5)return ArchiveInductionPresentation.SocketPosition(index,q);
            if(topic==4)return ArchiveMethodPresentation.SocketPosition(index,q);
            if(topic==3)return ArchiveProofPresentation.SocketPosition(index,q);
            if(topic==2)return ArchiveInferencePresentation.SocketPosition(index,q);
            if(q.activity==ArchiveActivity.Truth)return ArchiveTruthPresentation.RowPosition(index);
            if(q.regionPlacement)
            {
                if(q.slots.Length==2)return new Vector3(index==0?-3.4f:3.4f,.08f,-1.3f);
                if(q.slots.Length==3)return new Vector3((index-1)*4.3f,.08f,-1.3f);
                return new Vector3(index<3?(index-1)*4.3f:(index==3?-2.8f:2.8f),.08f,index<3?1.5f:-4.2f);
            }
            if(q.activity==ArchiveActivity.Induction)
            {
                string key=q.slots[index];if(key.StartsWith("Base"))return new Vector3(-4,.08f,3);
                if(key.StartsWith("Hypothesis"))return new Vector3(4,.08f,3);
                int line=int.Parse(key.Split(':')[1]);return new Vector3(-4+(line-1)*2.7f,.08f,-3);
            }
            int columns=Math.Min(5,q.slots.Length);return new Vector3(columns==1?0:Mathf.Lerp(-5,5,(index%columns)/(float)(columns-1)),.08f,-3+(index/columns)*4.2f);
        }
        public Vector3 InputPosition(int index)=>new Vector3(-4.5f,.08f,4.8f-index*3);
        public Vector3 OutputPosition(int index)=>new Vector3(4.5f,.08f,4.8f-index*3);
        public bool OnGraphCorridor(Vector2 point,ArchiveQuestion q)
        {
            if(q.nodes.Any(n=>Vector2.Distance(point,n+GraphOffset)<=1.1f))return true;
            foreach(var edge in q.graph.edges.Where(e=>e.traversalEnabled))
            {
                var a=q.nodes[Array.IndexOf(q.nodeIds,edge.source)]+GraphOffset;var b=q.nodes[Array.IndexOf(q.nodeIds,edge.destination)]+GraphOffset;
                var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(point-a,ab)/Mathf.Max(.001f,ab.sqrMagnitude));
                if(Vector2.Distance(point,a+ab*t)<.8f)return true;
            }
            return false;
        }
    }
    [DefaultExecutionOrder(10000)]
    public sealed class ArchiveTopicLabelFacing:MonoBehaviour
    {
        void LateUpdate(){if(Camera.main!=null)transform.rotation=Camera.main.transform.rotation;}
    }
}
