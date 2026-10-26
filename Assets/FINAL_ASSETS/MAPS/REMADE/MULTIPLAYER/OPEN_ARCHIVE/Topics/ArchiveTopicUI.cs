using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local reading/edit controls. Never pauses the room, locks a player or owns puzzle state.</summary>
    public sealed class ArchiveTopicUI : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public Sprite panelArt, statusArt, buttonArt;
        public TMP_FontAsset textFont, headingFont;
        public static ArchiveTopicUI Current { get; private set; }
        RectTransform panel, content;
        TMP_Text heading, status, hint, feedback, networkNotice;
        Button submitButton,startButton,returnButton;
        ScrollRect scroll;
        ArchiveTopicStation openStation;
        ArchiveTopicPoint focus;
        ArchivePropositionUI proposition;
        ArchiveTruthUI truth;
        ArchiveInferenceUI inference;
        ArchiveProofUI proofs;
        ArchiveMethodUI methods;
        ArchiveInductionUI induction;
        ArchiveSetsUI sets;
        ArchiveMappingUI mapping;
        ArchiveGraphUI graph;
        ArchiveTreeUI tree;
        public ArchiveTopicPoint FocusedPoint => focus;
        public ArchiveTopicStation ReadingStation => openStation;
        GameInput input;
        int nodeA=-1,nodeB=-1;
        bool dirty,resetScrollOnRefresh;
        int displayedTeamRevision=-1;
        static readonly Color Ink=new Color(.16f,.11f,.07f),Cream=new Color(1,.94f,.79f);
        void Awake(){Current=this;Build();BuildNetworkNotice();proposition=gameObject.AddComponent<ArchivePropositionUI>();proposition.Initialize(this);truth=gameObject.AddComponent<ArchiveTruthUI>();truth.Initialize(this);inference=gameObject.AddComponent<ArchiveInferenceUI>();inference.Initialize(this);proofs=gameObject.AddComponent<ArchiveProofUI>();proofs.Initialize(this);methods=gameObject.AddComponent<ArchiveMethodUI>();methods.Initialize(this);induction=gameObject.AddComponent<ArchiveInductionUI>();induction.Initialize(this);sets=gameObject.AddComponent<ArchiveSetsUI>();sets.Initialize(this);mapping=gameObject.AddComponent<ArchiveMappingUI>();mapping.Initialize(this);graph=gameObject.AddComponent<ArchiveGraphUI>();graph.Initialize(this);tree=gameObject.AddComponent<ArchiveTreeUI>();tree.Initialize(this);ArchiveReadingScrollbars.Install(transform);gameObject.AddComponent<ArchiveWorldLabelLayout>();}
        void BuildNetworkNotice()
        {
            var go=new GameObject("Topic_Command_Feedback",typeof(RectTransform),typeof(TextMeshProUGUI));
            go.transform.SetParent(transform,false);networkNotice=go.GetComponent<TMP_Text>();
            networkNotice.font=textFont!=null?textFont:StageUITheme.ReadingFont;networkNotice.fontSize=23;
            networkNotice.alignment=TextAlignmentOptions.Center;networkNotice.color=Color.white;networkNotice.raycastTarget=false;
            var rect=networkNotice.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.18f);rect.sizeDelta=new Vector2(850,70);
        }
        IEnumerator Start()
        {
            while(GameInput.Instance==null)yield return null;
            input=GameInput.Instance;input.OnInteractAction+=Hand;
        }
        void OnDestroy(){if(input!=null)input.OnInteractAction-=Hand;if(Current==this)Current=null;}
        void Hand(object sender,EventArgs args)
        {
            if(!session.Ready||!session.CanAct(session.LocalActor)||Time.timeScale<=0)return;
            if(ArchiveCoopActivityView.HandlesHand(session))return;
            focus?.Interact();
        }
        void Update()
        {
            if(networkNotice!=null)networkNotice.text=session!=null?session.LocalNotice:"";
            bool available=session!=null&&session.Ready&&Player.LocalInstance!=null&&Time.timeScale>0&&session.CanAct(session.LocalActor);
            if(!available){focus=null;hint.text="";if(openStation!=null)Close();return;}
            var player=Player.LocalInstance;focus=null;float best=5.6f;
            foreach(var station in session.stations)
            {
                if(Vector3.Distance(player.transform.position,station.transform.position)>45)continue;
                bool carrying=session.State.stations[station.topic].carries.Any(c=>c.actor==session.LocalActor);
                foreach(var point in station.points)
                {
                    if(point==null||!point.gameObject.activeInHierarchy)continue;
                    if(station.topic==0)
                    {
                        var state=session.State.stations[0];
                        if(point.role==ArchivePointRole.Choice&&(state.phase!=1||state.channelActor!=0||!session.Near(session.LocalActor,0,point.transform.localPosition,ArchiveTopicSession.ChoiceUseRadius)))continue;
                        if(point.role==ArchivePointRole.Book&&!session.Near(session.LocalActor,0,ArchiveTopicStation.BookPosition,ArchiveTopicSession.BookUseRadius))continue;
                    }
                    if(carrying&&point.role==ArchivePointRole.Item)continue;
                    if(!carrying&&point.role==ArchivePointRole.Slot)continue;
                    var delta=point.transform.position-player.transform.position;delta.y=0;
                    float dot=delta.sqrMagnitude<.01f?1:Vector3.Dot(player.transform.forward,delta.normalized);
                    if(dot<-.2f&&delta.magnitude>2)continue;
                    float score=delta.magnitude+(1-dot)*.65f;if(score>=best)continue;
                    best=score;focus=point;
                }
            }
            hint.text=focus==null||(openStation!=null&&openStation.topic<=9)?"":"Hand / E: "+focus.caption;
            if(focus!=null&&focus.role==ArchivePointRole.Book&&!string.IsNullOrEmpty(session.TopicLockNotice(focus.station.topic)))hint.text=session.TopicLockNotice(focus.station.topic);
            if(focus!=null&&focus.station.topic==0&&proposition!=null&&proposition.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==1&&truth!=null&&truth.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==2&&inference!=null&&inference.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==3&&proofs!=null&&proofs.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==4&&methods!=null&&methods.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==5&&induction!=null&&induction.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==6&&sets!=null&&sets.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==7&&mapping!=null&&mapping.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==8&&graph!=null&&graph.IsObjectiveVisible)hint.text="";
            if(focus!=null&&focus.station.topic==9&&tree!=null&&tree.IsObjectiveVisible)hint.text="";
            if(displayedTeamRevision!=session.State.revision)
            {
                displayedTeamRevision=session.State.revision;
                int completed=session.State.stations.Count(s=>s.phase==2);
                status.text="OPEN ARCHIVE  |  TEAM TOPICS "+completed+" / 10  |  MISTAKES "+session.State.mistakes.Count;
                dirty=true;
            }
            if(openStation!=null)
            {
                if(Vector3.Distance(player.transform.position,openStation.transform.position)>40){Close();return;}
                if(openStation.topic>=0&&openStation.topic<=9)return;
                if(dirty){dirty=false;Refresh();}
                var state=session.State.stations[openStation.topic];
                if(state.channelActor!=0)feedback.text="Channeling "+session.ActorName(state.channelActor)+"  "+Mathf.Clamp01((float)((ArchiveTopicSession.Clock-state.channelStarted)/session.answerChannelSeconds)).ToString("P0");
            }
        }
        public void Open(ArchiveTopicStation station)
        {
            if(!session.Ready||!session.CanAct(session.LocalActor))return;
            if(mapping!=null)mapping.Close();
            if(graph!=null)graph.Close();
            if(tree!=null)tree.Close();
            if(sets!=null)sets.Close();
            if(induction!=null)induction.Close();
            if(methods!=null)methods.Close();
            if(proofs!=null)proofs.Close();
            if(inference!=null)inference.Close();
            if(station.topic==9){if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);tree.Open();return;}
            if(station.topic==8){if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);graph.Open();return;}
            if(station.topic==7){if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);mapping.Open();return;}
            if(station.topic==6){if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);sets.Open();return;}
            if(station.topic==5){if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);induction.Open();return;}
            if(station.topic==4){if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);methods.Open();return;}
            if(station.topic==3){if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);proofs.Open();return;}
            if(station.topic==2){if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);inference.Open();return;}
            if(station.topic==0){if(truth!=null)truth.Close();openStation=station;panel.gameObject.SetActive(false);proposition.Open();return;}
            if(station.topic==1){if(proposition!=null)proposition.Close();openStation=station;panel.gameObject.SetActive(false);truth.Open();return;}
            if(truth!=null)truth.Close();
            if(proposition!=null)proposition.Close();
            if(openStation!=station||!panel.gameObject.activeSelf){nodeA=nodeB=-1;resetScrollOnRefresh=true;}
            openStation=station;panel.gameObject.SetActive(true);Refresh();
        }
        public void Close(){openStation=null;panel.gameObject.SetActive(false);if(proposition!=null)proposition.Close();if(truth!=null)truth.Close();if(inference!=null)inference.Close();if(proofs!=null)proofs.Close();if(methods!=null)methods.Close();if(induction!=null)induction.Close();if(sets!=null)sets.Close();if(mapping!=null)mapping.Close();if(graph!=null)graph.Close();if(tree!=null)tree.Close();}
        public void PickNode(ArchiveTopicStation station,int index)
        {
            Open(station);var q=session.Question(station.topic);
            if(station.topic==9){if(!ArchiveTreeRules.NumberQuestion(q))session.Request(9,ArchiveAction.Select,index);return;}
            if(q.tree==null)return;
            if(q.tree.type==LogicLegends.Trees.TreeChallengeType.Identify){session.Request(station.topic,ArchiveAction.Select,index);return;}
            if(nodeA<0||nodeB>=0){nodeA=index;nodeB=-1;}else nodeB=index;
            Refresh();
        }
        void Build()
        {
            var canvas=gameObject.GetComponent<Canvas>();if(canvas==null)canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;
            var scale=gameObject.GetComponent<CanvasScaler>();if(scale==null)scale=gameObject.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
            if(GetComponent<GraphicRaycaster>()==null)gameObject.AddComponent<GraphicRaycaster>();
            var hud=Rect("Shared_Topic_Status",transform,new Vector2(.5f,1),new Vector2(.5f,1));hud.pivot=new Vector2(.5f,1);hud.sizeDelta=new Vector2(790,72);hud.anchoredPosition=new Vector2(0,-16);Surface(hud,statusArt);
            status=Text("Team_Progress",hud,"Explore a topic clearing",25,headingFont);Stretch(status.rectTransform,new Vector2(.04f,.14f),new Vector2(.96f,.86f));status.alignment=TextAlignmentOptions.Center;
            var cue=Rect("Topic_Interaction_Hint",transform,new Vector2(.5f,0),new Vector2(.5f,0));cue.sizeDelta=new Vector2(800,74);cue.anchoredPosition=new Vector2(0,140);
            hint=Text("Hand_Action",cue,"",28,headingFont);Stretch(hint.rectTransform,Vector2.zero,Vector2.one);hint.alignment=TextAlignmentOptions.Center;hint.color=Cream;hint.outlineWidth=.2f;hint.outlineColor=Color.black;
            panel=Rect("Topic_Reading_And_Controls",transform,new Vector2(.18f,.19f),new Vector2(.82f,.87f));Surface(panel,panelArt);
            heading=Text("Topic_Title",panel,"",32,headingFont);Stretch(heading.rectTransform,new Vector2(.05f,.77f),new Vector2(.73f,.88f));
            var close=Button(panel,"Close",Close);close.anchorMin=close.anchorMax=new Vector2(.85f,.91f);close.sizeDelta=new Vector2(240,88);
            var view=Rect("Reading_Scroll",panel,new Vector2(.05f,.29f),new Vector2(.95f,.75f));view.gameObject.AddComponent<RectMask2D>();
            scroll=view.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            var hit=view.gameObject.AddComponent<Image>();hit.color=new Color(0,0,0,.001f);
            content=Rect("Content",view,new Vector2(0,1),new Vector2(1,1));content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(12,12,8,18);layout.spacing=14;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;scroll.viewport=view;
            feedback=Text("Shared_Feedback",panel,"",25,textFont);Stretch(feedback.rectTransform,new Vector2(.05f,.15f),new Vector2(.95f,.27f));
            var submit=Button(panel,"Submit",()=>{if(openStation!=null)session.Request(openStation.topic,ArchiveAction.Submit);});submit.anchorMin=submit.anchorMax=new Vector2(.5f,.08f);
            submitButton=submit.GetComponent<Button>();
            var returnItem=Button(panel,"Return Item",()=>{if(openStation!=null)session.Request(openStation.topic,ArchiveAction.ReturnItem);});returnItem.anchorMin=returnItem.anchorMax=new Vector2(.22f,.08f);
            returnButton=returnItem.GetComponent<Button>();
            var start=Button(panel,"Start / Read",()=>{if(openStation!=null)session.Request(openStation.topic,ArchiveAction.Start);});start.anchorMin=start.anchorMax=new Vector2(.78f,.08f);
            startButton=start.GetComponent<Button>();
            panel.gameObject.SetActive(false);
        }
        void Refresh()
        {
            if(openStation==null||!session.Ready)return;
            var s=session.State.stations[openStation.topic];var q=session.Question(openStation.topic);
            heading.text="T"+(openStation.topic+1)+"  "+ArchiveTopicCatalog.Names[openStation.topic]+"\n"+Math.Min(s.round+1,ArchiveTopicCatalog.RoundCount(s.topic))+" / "+ArchiveTopicCatalog.RoundCount(s.topic);
            if(s.topic==6)
            {
                int round=Math.Min(s.round,ArchiveTopicCatalog.RoundCount(s.topic)-1);
                heading.text="T7  Sets\n"+(s.phase==2?"All 5 challenges complete":"Challenge "+(round/ArchiveTopicCatalog.SetProblemsPerChallenge+1)+" / "+ArchiveTopicCatalog.SetChallengeCount+" · Problem "+(round%ArchiveTopicCatalog.SetProblemsPerChallenge+1)+" / "+ArchiveTopicCatalog.SetProblemsPerChallenge);
            }
            string lockNotice=session.TopicLockNotice(s.topic);
            feedback.text=string.IsNullOrEmpty(lockNotice)?s.feedback:lockNotice;
            startButton.interactable=session.CanStartTopic(s.topic);
            submitButton.interactable=s.phase==1&&q.activity!=ArchiveActivity.Choice&&s.carries.Count==0;
            returnButton.interactable=s.carries.Any(c=>c.actor==session.LocalActor);
            startButton.gameObject.SetActive(s.phase==0);startButton.GetComponent<RectTransform>().anchorMin=startButton.GetComponent<RectTransform>().anchorMax=new Vector2(.5f,.08f);
            submitButton.gameObject.SetActive(s.phase==1&&q.activity!=ArchiveActivity.Choice);
            returnButton.gameObject.SetActive(returnButton.interactable);
            float scrollPosition=resetScrollOnRefresh?1:scroll.verticalNormalizedPosition;resetScrollOnRefresh=false;
            foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            if(s.phase!=1){Paragraph(!string.IsNullOrEmpty(lockNotice)?lockNotice:s.phase==2?"Completed for the whole team.":"Approach the Book Statue and use Hand / E or Start to begin the next challenge. The world remains active while you read.");if(!string.IsNullOrEmpty(s.reveal))Paragraph(s.reveal);Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;return;}
            Paragraph(q.title+"\n"+q.prompt);Paragraph(q.instructions);
            if(q.activity==ArchiveActivity.Truth)
                Paragraph(string.Join("\n",q.slots.Select((name,i)=>name+"  →  "+(s.placements[i]<0?"[empty]":q.items[s.placements[i]]))));
            if(q.graph!=null||q.tree!=null)Diagram(q,s);
            if(q.tree!=null&&q.tree.Editable)
            {
                Paragraph("Parent: "+(nodeA<0?"—":q.nodeIds[nodeA])+"    Child: "+(nodeB<0?"—":q.nodeIds[nodeB]));
                Row(new[]{"Connect","Remove"},i=>{if(nodeA>=0&&nodeB>=0)session.Request(s.topic,i==0?ArchiveAction.Connect:ArchiveAction.Remove,nodeA,nodeB);});
            }
            if(q.graph!=null)
            {
                Paragraph("Route: "+string.Join(" → ",s.route.Select(i=>q.nodeIds[i]))+"\nWalker: "+session.ActorName(s.routeActor)+(s.continuous?"":"\nRoute left a connection; use Retry Route."));
                Row(new[]{"Retry Route"},i=>session.Request(s.topic,ArchiveAction.RetryRoute));
            }
            if(q.mapping!=null)
            {
                Paragraph("Connections: "+string.Join(", ",ArchiveTopicQuestions.Pairs(q,s.links)));
                var own=session.State.actors.FirstOrDefault(a=>a.actor==session.LocalActor);
                if(q.mapping.questionKind==MappingQuestionKind.BuildExactRelation)
                {
                    Paragraph("Selected input: "+(own!=null&&own.mappingRound==s.round&&own.mappingInput>=0&&own.mappingInput<q.inputLabels.Length?q.inputLabels[own.mappingInput]:"none")+". Walk to a Domain marker, then a Range marker, using Hand / E at both.");
                    Row(new[]{"Undo Laser"},i=>session.Request(s.topic,ArchiveAction.Undo));
                }
                else if(q.mapping.questionKind==MappingQuestionKind.ClassifyFunction)Row(ArchiveMappingPresentation.ClassificationNames,i=>session.Request(s.topic,ArchiveAction.Select,i));
                else Row(new[]{"Injective","Surjective","Bijective"},i=>session.Request(s.topic,ArchiveAction.Select,new[]{2,4,8}[i]));
            }
            if(q.activity==ArchiveActivity.Method)Row(new[]{"Direct","Contrapositive","Contradiction"},i=>session.Request(s.topic,ArchiveAction.Select,i));
            if(q.tree!=null&&(q.tree.type==LogicLegends.Trees.TreeChallengeType.Depth||q.tree.type==LogicLegends.Trees.TreeChallengeType.Height)||q.graph!=null&&q.graph.type==LogicLegends.Graph.GraphChallengeType.Degree)
            {
                for(int start=0;start<=10;start+=4){int first=start;var labels=Enumerable.Range(first,Math.Min(4,11-first)).Select(i=>i.ToString()).ToArray();Row(labels,i=>session.Request(s.topic,ArchiveAction.Select,(q.tree!=null?100:0)+first+i),130);}
            }
            if(q.activity==ArchiveActivity.Method)Paragraph("Selected method: "+(s.selection<0?"none":new[]{"Direct","Contrapositive","Contradiction"}[s.selection]));
            if(q.mapping!=null&&q.mapping.questionKind==MappingQuestionKind.SelectProperties)Paragraph("Selected properties: "+string.Join(", ",new[]{2,4,8}.Where(v=>(s.flags&v)!=0).Select(v=>v==2?"Injective":v==4?"Surjective":"Bijective")));
            if(q.mapping!=null&&q.mapping.questionKind==MappingQuestionKind.ClassifyFunction)Paragraph("Selected category: "+(s.selection<0?"none":ArchiveMappingPresentation.ClassificationNames[s.selection]));
            if(q.graph!=null&&q.graph.type==LogicLegends.Graph.GraphChallengeType.Degree)Paragraph("Selected degree: "+(s.selection<0?"none":s.selection.ToString()));
            if(q.tree!=null&&(q.tree.type==LogicLegends.Trees.TreeChallengeType.Depth||q.tree.type==LogicLegends.Trees.TreeChallengeType.Height))Paragraph("Selected number: "+(s.flags<0?"none":s.flags.ToString()));
            if(q.slots.Length>0&&q.activity!=ArchiveActivity.Truth)
                Paragraph("Answer stations\n"+string.Join("\n",q.slots.Select((label,i)=>label+": "+(q.regionPlacement?string.Join(", ",s.placements.Select((r,n)=>new{r,n}).Where(v=>v.r==i).Select(v=>q.items[v.n])):s.placements[i]<0?"[empty]":q.items[s.placements[i]]))));
            foreach(int i in q.itemOrder)
                Paragraph(q.items[i]+(i<q.details.Length?"\n"+q.details[i]:""));
            var held=s.carries.FirstOrDefault(c=>c.actor==session.LocalActor);if(held!=null)Paragraph("Carrying: "+q.items[held.item]);
            if(session.State.mistakes.Count>0)Paragraph("Team mistake record\n"+string.Join("\n",session.State.mistakes.TakeLast(8).Select(m=>session.ActorName(m.actor)+" · T"+(m.topic+1)+" · challenge "+(m.round+1))));
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=scrollPosition;
        }
        void Diagram(ArchiveQuestion q,ArchiveStationState s)
        {
            var field=Rect("Mathematical_Diagram",content,Vector2.zero,Vector2.one);field.gameObject.AddComponent<LayoutElement>().preferredHeight=420;
            Func<int,Vector2> position=i=>new Vector2((q.nodes[i].x+5)/10*760+70,(q.nodes[i].y+5)/10*300+60);
            var edges=q.graph!=null?q.graph.edges.Where(e=>e.traversalEnabled).Select(e=>Array.IndexOf(q.nodeIds,e.source)*100+Array.IndexOf(q.nodeIds,e.destination)).ToArray():s.links.ToArray();
            foreach(int link in edges)
            {
                var a=position(link/100);var b=position(link%100);var edge=Rect("Branch",field,Vector2.zero,Vector2.zero);edge.pivot=new Vector2(0,.5f);edge.anchoredPosition=a;edge.sizeDelta=new Vector2((b-a).magnitude,5);edge.localEulerAngles=new Vector3(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);edge.gameObject.AddComponent<Image>().color=new Color(.23f,.40f,.36f);
                bool directed=q.graph!=null?q.graph.edges.Any(e=>e.directed&&e.source==q.nodeIds[link/100]&&e.destination==q.nodeIds[link%100]):q.tree.Directed;
                if(directed)
                {
                    Vector2 direction=(b-a).normalized,tip=Vector2.Lerp(a,b,.6f),side=new Vector2(-direction.y,direction.x)*11;
                    foreach(Vector2 end in new[]{tip-direction*22+side,tip-direction*22-side})
                    {
                        var arrow=Rect("Direction_Arrow",field,Vector2.zero,Vector2.zero);arrow.pivot=new Vector2(0,.5f);arrow.anchoredPosition=tip;arrow.sizeDelta=new Vector2((end-tip).magnitude,5);arrow.localEulerAngles=new Vector3(0,0,Mathf.Atan2(end.y-tip.y,end.x-tip.x)*Mathf.Rad2Deg);arrow.gameObject.AddComponent<Image>().color=new Color(.23f,.40f,.36f);
                    }
                }
            }
            for(int i=0;i<q.nodes.Length;i++)
            {
                int index=i;var button=Button(field,q.nodeIds[i],()=>{if(q.tree!=null)PickNode(openStation,index);});button.anchorMin=button.anchorMax=Vector2.zero;button.anchoredPosition=position(i);button.sizeDelta=new Vector2(100,88);
            }
        }
        void Paragraph(string value)
        {
            var text=Text("Readable_Content",content,value,28,textFont);text.alignment=TextAlignmentOptions.TopLeft;
            text.margin=new Vector4(0,0,24,0);
            var size=text.gameObject.AddComponent<LayoutElement>();size.preferredHeight=Mathf.Max(48,text.GetPreferredValues(value,930,0).y+12);
        }
        void Row(string[] labels,Action<int> action,float width=280)
        {
            var row=Rect("Actions",content,Vector2.zero,Vector2.one);row.gameObject.AddComponent<LayoutElement>().preferredHeight=94;
            var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleCenter;layout.spacing=12;layout.childControlWidth=false;layout.childControlHeight=false;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
            for(int i=0;i<labels.Length;i++){int index=i;var button=Button(row,labels[i],()=>action(index));button.sizeDelta=new Vector2(width,88);}
        }
        RectTransform Button(Transform parent,string label,UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(label.Replace(' ','_'),parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f));rect.sizeDelta=new Vector2(280,88);
            var image=rect.gameObject.AddComponent<Image>();image.sprite=buttonArt;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;image.color=Color.white;
            var button=rect.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;button.onClick.AddListener(action);
            var text=Text("Label",rect,label,27,headingFont);Stretch(text.rectTransform,new Vector2(.08f,.1f),new Vector2(.92f,.9f));text.color=Cream;text.alignment=TextAlignmentOptions.Center;return rect;
        }
        TMP_Text Text(string name,Transform parent,string value,float size,TMP_FontAsset font)
        {
            var text=new GameObject(name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();text.transform.SetParent(parent,false);text.font=font;text.text=value;text.fontSize=size;text.color=Ink;text.raycastTarget=false;text.richText=false;return text;
        }
        void Surface(RectTransform rect,Sprite art){var image=rect.gameObject.AddComponent<Image>();image.sprite=art;image.type=Image.Type.Sliced;image.color=Color.white;}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);Stretch(r,min,max);return r;}
        static void Stretch(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
