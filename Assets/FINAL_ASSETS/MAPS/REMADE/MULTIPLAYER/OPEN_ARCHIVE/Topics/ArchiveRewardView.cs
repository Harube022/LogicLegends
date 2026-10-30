using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Checkpoint-driven chest, completed-board farewell and local supply presentation. Owns no rewards.</summary>
    [DefaultExecutionOrder(10042), DisallowMultipleComponent]
    public sealed partial class ArchiveRewardView : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public GameObject chestPrefab;
        public GameObject[] itemPrefabs=new GameObject[6];
        public Sprite[] itemIcons=new Sprite[6];
        [Header("Flashlight illumination and VFX")]
        public Material flashlightBeamMaterial;
        public Color flashlightColor=new Color(1,.93f,.78f);
        [Min(0)] public float flashlightIntensity=200;
        [Range(4,32)] public float flashlightRange=30;
        [Range(20,65)] public float flashlightSpotAngle=42;
        [Range(0,.15f)] public float flashlightBeamOpacity=.045f;
        [Range(0,.4f)] public float flashlightImpactOpacity=.14f;
        public Mesh sparkMesh;
        public Material glowMaterial;
        public AudioClip chestOpenSound, formationSound, dartSound;
        [Min(.2f)] public float disappearSeconds=1.35f;
        public static readonly Vector3 ChestLocalPosition=ArchiveTopicStation.BookPosition+new Vector3(3.4f,0,-.8f);
        public static ArchiveRewardView Current {get;private set;}
        sealed class TopicVisual
        {
            public ArchiveTopicStation station;
            public readonly List<Transform> boards=new List<Transform>();
            public readonly List<Vector3> scales=new List<Vector3>();
            public readonly List<Vector3> positions=new List<Vector3>();
            public GameObject chest, sparks, preview;
            public int previewItem=-1;
            public Transform lid;
            public Quaternion lidClosed;
            public TMP_Text label;
            public Transform[] flecks;
            public int reset=-1;
            public bool observed, complete, played, observedClaimed;
        }
        readonly Dictionary<int,TopicVisual> topics=new Dictionary<int,TopicVisual>();
        readonly Dictionary<int,Light> lights=new Dictionary<int,Light>();
        Canvas canvas;
        RectTransform root,pack,contents,claimPanel,crosshair,defeatPanel;
        TMP_Text packTitle,claimText,info,defeatText;
        Button claimButton;
        readonly Button[] itemButtons=new Button[6];
        readonly TMP_Text[] itemTexts=new TMP_Text[6];
        bool expanded;
        int focusedTopic=-1;
        float nextRead;
        Vector2 screen;
        Rect safe;
        AudioSource audioSource;
        ArchiveTranquilizerVfx dartVfx;
        static readonly Color Ink=new Color(.025f,.10f,.12f), Cream=new Color(1,.94f,.78f), Gold=new Color(1,.68f,.2f);

        void Awake(){Current=this;}
        void Start()
        {
            if(session==null)session=GetComponent<ArchiveTopicSession>();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.volume=.35f;
            AudioVolumeSettings.Route(audioSource,GameAudioChannel.SoundFX);
            BuildUI();
            foreach(var station in session.stations)
            {
                var v=new TopicVisual{station=station};topics.Add(station.topic,v);
                foreach(Transform child in station.transform)
                {
                    if(!IsBoard(child.name))continue;
                    v.boards.Add(child);v.scales.Add(child.localScale);v.positions.Add(child.localPosition);
                }
            }
        }
        static bool IsBoard(string name)=>name.StartsWith("Question_Board_",StringComparison.Ordinal)||name=="Board_Support"||name=="Topic_Header_Backplate"||name=="Question_And_Instructions"||name=="Topic_Identity";
        public static bool HandlesHand(ArchiveTopicSession owner)=>Current!=null&&Current.session==owner&&Current.focusedTopic>=0&&Current.ClaimAvailable(Current.focusedTopic);
        public static bool HandleHand(ArchiveTopicSession owner)
        {
            if(!HandlesHand(owner))return false;
            owner.RequestChest(Current.focusedTopic);return true;
        }
        public bool BoardHidden(int topic)=>topics.TryGetValue(topic,out var v)&&v.complete&&v.boards.All(t=>!t.gameObject.activeSelf);
        public GameObject ChestObject(int topic)=>topics.TryGetValue(topic,out var v)?v.chest:null;
        bool ClaimAvailable(int topic)
        {
            return session!=null&&session.CanClaimChest(session.LocalActor,topic);
        }
        void Update()
        {
            if(session==null||!session.Ready)return;
            if(screen!=new Vector2(Screen.width,Screen.height)||safe!=Screen.safeArea)Place();
            TickHotbarInput();
            if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.15f;
            focusedTopic=-1;
            foreach(var pair in topics)if(ClaimAvailable(pair.Key)){focusedTopic=pair.Key;break;}
            bool canUse=Player.LocalInstance!=null&&session.CanAct(session.LocalActor)&&Time.timeScale>0;
            bool reading=ArchiveTopicUI.Current!=null&&ArchiveTopicUI.Current.ReadingStation!=null;
            claimPanel.gameObject.SetActive(canUse&&!reading&&focusedTopic>=0);
            if(focusedTopic>=0)claimText.text="T"+(focusedTopic+1)+(session.Chest(focusedTopic).orphaned?" TEAM SALVAGE":" REWARD RESERVED FOR YOU")+"\nOpen chest · Hand / E";
            defeatPanel.gameObject.SetActive(session.TeamDefeated);
            if(session.TeamDefeated)defeatText.text=session.State.survival.defeatReason==ArchiveTopicSession.MatchTimeoutReason?
                "TIME'S UP — THE ARCHIVE HAS SEALED\nReturning to the party in "+Mathf.CeilToInt(session.DefeatRemaining)+"...":
                "THE TEAM HAS FALLEN\nNo teammate remains to rescue you. Returning to the party in "+Mathf.CeilToInt(session.DefeatRemaining)+"...";
            RefreshHotbar(canUse,reading);
            RefreshLights();
        }
        void LateUpdate()
        {
            if(session==null||!session.Ready)return;
            foreach(var pair in topics)
            {
                var v=pair.Value;var state=session.State.stations[pair.Key];var reward=session.Chest(pair.Key);
                bool complete=state.phase==2;
                if(v.reset!=state.testResetSerial||v.complete!=complete)
                {
                    v.reset=state.testResetSerial;v.complete=complete;v.played=false;v.observedClaimed=false;
                    if(v.chest!=null)Destroy(v.chest);if(v.sparks!=null)Destroy(v.sparks);
                    v.chest=null;v.sparks=null;
                    Restore(v);
                }
                if(!complete){v.observed=true;continue;}
                double elapsed=reward==null?double.MaxValue:Math.Max(0,ArchiveTopicSession.Clock-reward.completedAt);
                float progress=Mathf.Clamp01((float)elapsed/disappearSeconds);
                AnimateBoard(v,progress);
                // Completion snapshots arriving later show the result immediately; effects never replay.
                bool near=Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,v.station.transform.position)<45;
                if(reward!=null&&progress<1&&v.observed&&near)
                {
                    if(v.sparks==null)MakeSparks(v);
                    AnimateSparks(v,progress);
                    if(!v.played){v.played=true;if(formationSound!=null)audioSource.PlayOneShot(formationSound,.35f);}
                }
                else if(v.sparks!=null){v.sparks.SetActive(false);}
                if(reward!=null&&progress>=1)
                {
                    if(v.chest==null)MakeChest(v,pair.Key);
                    float appearance=Mathf.SmoothStep(0,1,Mathf.Clamp01(((float)elapsed-disappearSeconds)/.5f));
                    v.chest.transform.localScale=Vector3.one*appearance;
                    float lid=Mathf.SmoothStep(0,1,reward.claimed?Mathf.Clamp01((float)(ArchiveTopicSession.Clock-reward.claimedAt)/.45f):0);
                    if(v.lid!=null)v.lid.localRotation=v.lidClosed*Quaternion.Euler(-72*lid,0,0);
                    if(reward.claimed&&!v.observedClaimed)
                    {
                        v.observedClaimed=true;
                        if(near&&ArchiveTopicSession.Clock-reward.claimedAt<1&&chestOpenSound!=null)audioSource.PlayOneShot(chestOpenSound,.7f);
                    }
                    bool labelNear=Player.LocalInstance!=null&&Vector3.Distance(Player.LocalInstance.transform.position,v.chest.transform.position)<22;
                    v.label.gameObject.SetActive(labelNear);
                    UpdateChestPreview(v,reward);
                    if(labelNear)
                    {
                        v.label.text=reward.claimed?"T"+(pair.Key+1)+" · CLAIMED\n"+ItemName(reward.item):"T"+(pair.Key+1)+" · "+ItemName(reward.item).ToUpperInvariant()+"\n"+(reward.orphaned?"Team salvage":reward.owner==session.LocalActor?"Reserved for you":session.ActorName(reward.owner));
                        if(Camera.main!=null)v.label.transform.rotation=Camera.main.transform.rotation;
                    }
                }
                v.observed=true;
            }
            ShowDart();
            foreach(var pair in lights)
            {
                if(pair.Value==null||!pair.Value.enabled)continue;
                var player=session.ActorPlayer(pair.Key);if(player==null)continue;
                var direction=pair.Key==session.LocalActor&&Camera.main!=null?Camera.main.transform.forward:player.transform.forward;
                var origin=player.transform.position+Vector3.up*2.8f+player.transform.forward*.35f;
                // Run after hand posing (10040), so the beam follows the real lens even
                // when the free-orbit camera faces back toward the character.
                var held=GetComponent<ArchiveEquippedItems>();
                if(held!=null&&held.TryGetMuzzle(pair.Key,out var lens,out var barrel)){origin=lens;direction=barrel;}
                pair.Value.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(direction,Vector3.up));
            }
        }
        void ShowDart()
        {
            if(dartVfx==null){dartVfx=GetComponent<ArchiveTranquilizerVfx>();if(dartVfx==null)dartVfx=gameObject.AddComponent<ArchiveTranquilizerVfx>();}
            if(dartVfx.Present(this)&&dartSound!=null)audioSource.PlayOneShot(dartSound,.75f);
        }
        static void Restore(TopicVisual v)
        {
            for(int i=0;i<v.boards.Count;i++){var t=v.boards[i];t.gameObject.SetActive(true);t.localScale=v.scales[i];t.localPosition=v.positions[i];}
            if(v.station.workRoot!=null){v.station.workRoot.gameObject.SetActive(true);v.station.workRoot.localScale=Vector3.one;}
        }
        void AnimateBoard(TopicVisual v,float p)
        {
            float shrink=1-Mathf.SmoothStep(0,1,p);
            for(int i=0;i<v.boards.Count;i++)
            {
                var t=v.boards[i];t.gameObject.SetActive(p<1);t.localScale=Vector3.Scale(v.scales[i],new Vector3(shrink,Mathf.Sqrt(shrink),shrink));t.localPosition=v.positions[i]+Vector3.up*(p*.75f);
            }
            // Answer plaques live here; the Book, ground seals and restored crystal are separate roots.
            if(v.station.workRoot!=null)v.station.workRoot.gameObject.SetActive(false);
        }
        void MakeSparks(TopicVisual v)
        {
            v.sparks=new GameObject("Completed_Board_Dispersal");v.sparks.transform.SetParent(v.station.transform,false);v.flecks=new Transform[12];
            for(int i=0;i<12;i++)
            {
                var go=new GameObject("Rising_Rune_Fragment_"+i,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(v.sparks.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=sparkMesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=glowMaterial;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;v.flecks[i]=go.transform;
            }
        }
        static void AnimateSparks(TopicVisual v,float p)
        {
            for(int i=0;i<v.flecks.Length;i++)
            {
                float x=(i%6-2.5f)*1.1f;
                v.flecks[i].localPosition=new Vector3(x*(1+p*.5f),1.5f+(i/6)*1.7f+p*(2+i%3),-10.1f+Mathf.Sin(i*2.4f+p*4)*p);
                v.flecks[i].localScale=Vector3.one*(.13f*(1-p));v.flecks[i].localRotation=Quaternion.Euler(i*23+p*170,i*43+p*210,45);
            }
        }
        void MakeChest(TopicVisual v,int topic)
        {
            v.chest=Instantiate(chestPrefab,v.station.transform);v.chest.name="T"+(topic+1)+"_Solver_Reward_Chest";v.chest.transform.localPosition=ChestLocalPosition;v.chest.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var collider in v.chest.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            v.lid=v.chest.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.IndexOf("Lid",StringComparison.OrdinalIgnoreCase)>=0);
            if(v.lid!=null)v.lidClosed=v.lid.localRotation;
            var go=new GameObject("Chest_Owner_Label");go.transform.SetParent(v.chest.transform,false);go.transform.localPosition=new Vector3(0,2.25f,0);
            v.label=go.AddComponent<TextMeshPro>();v.label.font=StageUITheme.ReadingFont;v.label.fontSize=.9f;v.label.rectTransform.sizeDelta=new Vector2(5.8f,1.5f);v.label.alignment=TextAlignmentOptions.Center;v.label.color=Cream;v.label.richText=false;v.label.raycastTarget=false;
        }
        void UpdateChestPreview(TopicVisual visual,ArchiveTopicChest reward)
        {
            if(reward.claimed){if(visual.preview!=null)visual.preview.SetActive(false);return;}
            int item=(int)reward.item;
            if(itemPrefabs==null||item>=itemPrefabs.Length||itemPrefabs[item]==null)return;
            if(visual.preview==null||visual.previewItem!=item)
            {
                if(visual.preview!=null)Destroy(visual.preview);
                visual.preview=new GameObject("Chest_3D_Drop_Preview");visual.preview.transform.SetParent(visual.chest.transform,false);visual.previewItem=item;
                var model=Instantiate(itemPrefabs[item],visual.preview.transform,false);model.name="Preview_"+reward.item;
                var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length>0){var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);model.transform.localPosition-=visual.preview.transform.InverseTransformPoint(bounds.center);}
                visual.preview.transform.localScale=Vector3.one*.8f;
            }
            visual.preview.SetActive(true);visual.preview.transform.localPosition=new Vector3(0,1.45f+Mathf.Sin(Time.unscaledTime*1.8f)*.07f,0);
            visual.preview.transform.localRotation=Quaternion.Euler(0,Time.unscaledTime*24,0);
        }
        public GameObject ChestPreview(int topic)=>topics.TryGetValue(topic,out var visual)?visual.preview:null;
        void RefreshLights()
        {
            foreach(var light in lights.Values)if(light!=null)light.enabled=false;
            foreach(var player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                var view=player.GetComponent<PhotonView>();int actor=PhotonNetwork.InRoom&&view!=null?view.OwnerActorNr:player.GetComponent<ArchivePracticeAvatar>()!=null?-2:-1;
                var inv=session.Inventory(actor);if(inv==null||inv.equippedItem!=(int)ArchiveSurvivalItem.Flashlight||!inv.flashlightOn||session.FlashlightRemaining(actor)<=0||!session.CanAct(actor))continue;
                if(!lights.TryGetValue(actor,out var lamp)||lamp==null)
                {
                    var go=new GameObject("Actor_"+actor+"_Flashlight");go.transform.SetParent(transform,false);lamp=go.AddComponent<Light>();lights[actor]=lamp;
                    lamp.type=LightType.Spot;lamp.shadows=LightShadows.None;lamp.renderMode=LightRenderMode.ForcePixel;
                }
                lamp.color=flashlightColor;lamp.intensity=flashlightIntensity;lamp.range=flashlightRange;
                lamp.spotAngle=flashlightSpotAngle;lamp.innerSpotAngle=flashlightSpotAngle*.55f;
                var fx=lamp.GetComponent<ArchiveFlashlightVfx>();if(fx==null)fx=lamp.gameObject.AddComponent<ArchiveFlashlightVfx>();
                fx.Configure(lamp,player,flashlightBeamMaterial,flashlightRange,flashlightBeamOpacity,flashlightImpactOpacity);
                lamp.enabled=true;
            }
        }
        void BuildUI()
        {
            var go=new GameObject("Archive_Survival_Pack",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=117;root=go.GetComponent<RectTransform>();
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            BuildHotbar();
            claimPanel=Rect("Nearby_Reward_Claim",root,Vector2.one*.5f,Vector2.one*.5f);claimPanel.sizeDelta=new Vector2(510,90);
            var claimBg=claimPanel.gameObject.AddComponent<Image>();claimBg.color=new Color(.16f,.27f,.25f,.97f);claimButton=claimPanel.gameObject.AddComponent<Button>();claimButton.targetGraphic=claimBg;
            claimText=Text("Claim_Reserved_Item",claimPanel,23);claimText.alignment=TextAlignmentOptions.Center;claimText.color=Gold;
            claimButton.onClick.AddListener(()=>{if(focusedTopic>=0)session.RequestChest(focusedTopic);});
            crosshair=Rect("Tranquilizer_Aim",root,Vector2.one*.5f,Vector2.one*.5f);crosshair.sizeDelta=new Vector2(28,28);
            Stroke(crosshair,"Aim_Top",new Vector2(.46f,.7f),new Vector2(.54f,1),Cream);Stroke(crosshair,"Aim_Bottom",new Vector2(.46f,0),new Vector2(.54f,.3f),Cream);
            Stroke(crosshair,"Aim_Left",new Vector2(0,.46f),new Vector2(.3f,.54f),Cream);Stroke(crosshair,"Aim_Right",new Vector2(.7f,.46f),new Vector2(1,.54f),Cream);crosshair.gameObject.SetActive(false);
            defeatPanel=Rect("Team_Defeat_Return",root,new Vector2(.5f,.7f),new Vector2(.5f,.7f));defeatPanel.sizeDelta=new Vector2(780,150);
            var defeatBg=defeatPanel.gameObject.AddComponent<Image>();defeatBg.color=new Color(.12f,.025f,.025f,.97f);defeatBg.raycastTarget=false;
            defeatText=Text("Defeat_Countdown",defeatPanel,27);defeatText.alignment=TextAlignmentOptions.Center;defeatPanel.gameObject.SetActive(false);
            claimPanel.gameObject.SetActive(false);Place();
        }
        void Place()
        {
            if(root==null)return;Canvas.ForceUpdateCanvases();screen=new Vector2(Screen.width,Screen.height);safe=Screen.safeArea;float scale=Mathf.Max(.01f,canvas.scaleFactor);
            PlaceHotbar();
            claimPanel.sizeDelta=new Vector2(Mathf.Min(510,safe.width/scale*.5f),90);
            if(defeatPanel!=null)defeatPanel.sizeDelta=new Vector2(Mathf.Min(780,safe.width/scale*.85f),150);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(root,new Vector2(safe.center.x,safe.yMin+safe.height*.33f),null,out var c))claimPanel.anchoredPosition=c;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;}
        static TMP_Text Text(string name,Transform parent,float size)
        {var rect=Rect(name,parent,Vector2.zero,Vector2.one);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=StageUITheme.ReadingFont;text.fontSize=size;text.color=Cream;text.richText=false;text.raycastTarget=false;return text;}
        static void Stroke(Transform parent,string name,Vector2 min,Vector2 max,Color color)
        {var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;}
        static void Icon(Transform parent,ArchiveSurvivalItem item)
        {
            var pale=new Color(.92f,.96f,.82f);var teal=new Color(.27f,.8f,.79f);
            if(item==ArchiveSurvivalItem.Medkit){Stroke(parent,"Medical_Case",new Vector2(0,.1f),new Vector2(1,.9f),new Color(.67f,.19f,.20f));Stroke(parent,"Cross_V",new Vector2(.40f,.22f),new Vector2(.6f,.78f),pale);Stroke(parent,"Cross_H",new Vector2(.22f,.4f),new Vector2(.78f,.6f),pale);}
            else if(item==ArchiveSurvivalItem.Battery){Stroke(parent,"Battery",new Vector2(.12f,.15f),new Vector2(.88f,.8f),teal);Stroke(parent,"Terminal",new Vector2(.34f,.8f),new Vector2(.66f,.95f),pale);Stroke(parent,"Charge",new Vector2(.34f,.38f),new Vector2(.66f,.57f),Ink);}
            else if(item==ArchiveSurvivalItem.Flashlight){Stroke(parent,"Handle",new Vector2(.32f,.05f),new Vector2(.68f,.6f),pale);Stroke(parent,"Lamp_Head",new Vector2(.14f,.6f),new Vector2(.86f,.82f),Gold);Stroke(parent,"Beam",new Vector2(.08f,.88f),new Vector2(.92f,.98f),Cream);}
            else if(item==ArchiveSurvivalItem.RevivalKit){Stroke(parent,"Revival_Case",new Vector2(.05f,.1f),new Vector2(.95f,.9f),new Color(.25f,.65f,.43f));Stroke(parent,"Twin_Heart_L",new Vector2(.2f,.4f),new Vector2(.4f,.75f),Cream);Stroke(parent,"Twin_Heart_R",new Vector2(.6f,.4f),new Vector2(.8f,.75f),Cream);Stroke(parent,"Link",new Vector2(.28f,.25f),new Vector2(.72f,.4f),Gold);}
            else if(item==ArchiveSurvivalItem.Tranquilizer){Stroke(parent,"Barrel",new Vector2(.05f,.5f),new Vector2(1,.72f),teal);Stroke(parent,"Grip",new Vector2(.15f,.04f),new Vector2(.4f,.52f),Cream);Stroke(parent,"Dart",new Vector2(.68f,.44f),new Vector2(.88f,.78f),Gold);}
            else {Stroke(parent,"Dart_One",new Vector2(.15f,.1f),new Vector2(.38f,.9f),Cream);Stroke(parent,"Dart_Two",new Vector2(.62f,.1f),new Vector2(.85f,.9f),Cream);Stroke(parent,"Dart_Tips",new Vector2(.05f,.8f),new Vector2(.95f,.98f),teal);}
        }
        public static string ItemName(ArchiveSurvivalItem item)
        {switch(item){case ArchiveSurvivalItem.Flashlight:return "Flashlight";case ArchiveSurvivalItem.Battery:return "Battery";case ArchiveSurvivalItem.Medkit:return "Medkit";case ArchiveSurvivalItem.RevivalKit:return "Revival kit";case ArchiveSurvivalItem.Tranquilizer:return "Tranquilizer gun";default:return "Ammo";}}
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
