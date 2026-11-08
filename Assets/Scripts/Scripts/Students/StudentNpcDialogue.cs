using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Students
{
    // Screen-sized speech follows the speaker after gameplay camera movement.
    // It owns no player input, camera control, or countdown pause.
    [DefaultExecutionOrder(10000)]
    public sealed class StudentNpcDialogue : MonoBehaviour
    {
        public static readonly Color HintColor=new Color(.025f,.18f,.70f);
        public static readonly Color BubbleColor=new Color(.78f,.80f,.82f,.90f);
        public bool IsOpen {get;private set;}
        public bool IsVisible=>progress>.001f;
        public bool IsHint {get;private set;}
        public StudentNpcAgent Speaker {get;private set;}
        public TMP_Text Text=>line;
        public RectTransform BubbleRect=>panel;
        public float PopProgress=>progress;
        RectTransform safe,panel,viewport,tail;
        TMP_Text line;
        CanvasGroup group;
        ScrollRect scroll;
        Image scrollTarget;
        Sprite rounded;
        Texture2D roundedTexture;
        StudentNpcDirector owner;
        float progress,lastWidth=-1,lastHeight=-1,nextControls;
        int placement=-1;
        Vector2 size;
        readonly Vector3[] corners=new Vector3[4];
        readonly List<Rect> controls=new List<Rect>();

        public void Show(StudentNpcAgent speaker,string value,bool hint)
        {
            if(speaker==null||string.IsNullOrWhiteSpace(value))return;
            if(IsOpen&&Speaker==speaker)return;
            CloseImmediate();Build();
            Speaker=speaker;IsHint=hint;IsOpen=true;progress=0;
            line.text=value;line.color=hint?HintColor:Color.black;
            lastWidth=-1;nextControls=0;placement=-1;
            group.alpha=0;panel.localScale=Vector3.one*.86f;panel.gameObject.SetActive(true);
            speaker.PauseForConversation(Player.LocalInstance.transform);
            Canvas.ForceUpdateCanvases();Measure();Place();
        }
        void Build()
        {
            if(panel!=null)return;
            owner=GetComponent<StudentNpcDirector>();
            var root=new GameObject("Student speech bubbles",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=220;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            safe=StageUITheme.Rect("Speech safe area",root.transform);StageUITheme.Stretch(safe,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<Crystal.SafeArea>();
            panel=StageUITheme.Rect("NPC dialogue bubble",safe);panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.pivot=new Vector2(.5f,0);
            group=panel.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            var background=panel.gameObject.AddComponent<Image>();background.sprite=Rounded();background.type=Image.Type.Sliced;background.color=BubbleColor;background.raycastTarget=false;
            var shadow=panel.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.24f);shadow.effectDistance=new Vector2(2,-3);
            var outline=panel.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.19f,.24f,.25f,.65f);outline.effectDistance=Vector2.one;
            tail=StageUITheme.Rect("Speech pointer",panel);tail.sizeDelta=Vector2.one*19;tail.localRotation=Quaternion.Euler(0,0,45);
            var point=tail.gameObject.AddComponent<Image>();point.color=BubbleColor;point.raycastTarget=false;
            viewport=StageUITheme.Rect("Speech text viewport",panel);StageUITheme.Stretch(viewport,Vector2.zero,Vector2.one);viewport.offsetMin=new Vector2(30,26);viewport.offsetMax=new Vector2(-30,-26);
            scrollTarget=viewport.gameObject.AddComponent<Image>();scrollTarget.color=Color.clear;scrollTarget.raycastTarget=false;viewport.gameObject.AddComponent<RectMask2D>();
            line=StageUITheme.Text("NPC spoken line",viewport,"",36,true);line.color=Color.black;line.fontStyle=FontStyles.Bold;line.richText=false;line.alignment=TextAlignmentOptions.TopLeft;line.lineSpacing=3;
            line.textWrappingMode=TextWrappingModes.Normal;line.overflowMode=TextOverflowModes.Overflow;line.raycastTarget=false;
            var content=line.rectTransform;content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            panel.gameObject.SetActive(false);
        }
        Sprite Rounded()
        {
            const int pixels=64,radius=14;
            roundedTexture=new Texture2D(pixels,pixels,TextureFormat.RGBA32,false){name="Student bubble rounded corners",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var colors=new Color[pixels*pixels];
            for(int y=0;y<pixels;y++)for(int x=0;x<pixels;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);var nearest=new Vector2(Mathf.Clamp(p.x,radius,pixels-radius),Mathf.Clamp(p.y,radius,pixels-radius));
                colors[y*pixels+x]=new Color(1,1,1,Mathf.Clamp01(radius-Vector2.Distance(p,nearest)+.5f));
            }
            roundedTexture.SetPixels(colors);roundedTexture.Apply(false,true);
            rounded=Sprite.Create(roundedTexture,new Rect(0,0,pixels,pixels),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,Vector4.one*radius);
            return rounded;
        }
        void Measure()
        {
            if(panel==null)return;
            var bounds=safe.rect;
            if(bounds.width<100||bounds.height<100)return;
            if(Mathf.Abs(lastWidth-bounds.width)<.5f&&Mathf.Abs(lastHeight-bounds.height)<.5f)return;
            lastWidth=bounds.width;lastHeight=bounds.height;
            nextControls=0;
            float maximum=Mathf.Min(Mathf.Max(300,owner!=null&&owner.Config!=null?owner.Config.dialogueMaxWidth:800),bounds.width*.36f);
            float width=Mathf.Clamp(line.GetPreferredValues(line.text).x+60,Mathf.Min(270,maximum),maximum);
            float textHeight=line.GetPreferredValues(line.text,width-60,float.PositiveInfinity).y;
            float height=Mathf.Min(textHeight+52,bounds.height*.62f);
            size=new Vector2(width,height);panel.sizeDelta=size;
            line.rectTransform.sizeDelta=new Vector2(0,textHeight);line.rectTransform.anchoredPosition=Vector2.zero;
            bool overflow=textHeight>height-51;
            scroll.enabled=overflow;scrollTarget.raycastTarget=overflow;group.blocksRaycasts=overflow&&IsOpen;group.interactable=overflow&&IsOpen;
            scroll.verticalNormalizedPosition=1;
        }
        void LateUpdate()
        {
            if(panel==null)return;
            if(Speaker==null||!Speaker.isActiveAndEnabled||Player.LocalInstance==null){CloseImmediate();return;}
            if(owner==null||owner.Paused){CloseImmediate();return;}
            if(IsOpen)
            {
                var player=Player.LocalInstance.transform.position;
                if(StudentWalkMap.PlanarSquare(player,Speaker.transform.position)>owner.Config.talkRange*owner.Config.talkRange||Mathf.Abs(player.y-Speaker.transform.position.y)>owner.Config.height)Close();
            }
            progress=Mathf.MoveTowards(progress,IsOpen?1:0,Time.unscaledDeltaTime/(IsOpen?.22f:.16f));
            // The same curve in both directions makes interrupted pops continuous.
            float t=1+2.2f*Mathf.Pow(progress-1,3)+1.2f*Mathf.Pow(progress-1,2);
            panel.localScale=Vector3.one*Mathf.LerpUnclamped(.86f,1,t);group.alpha=Mathf.SmoothStep(0,1,progress);
            Measure();Place();
            if(!IsOpen&&progress<=0)FinishClose();
        }
        void Place()
        {
            var camera=Camera.main;if(camera==null||Speaker==null)return;
            var projected=camera.WorldToScreenPoint(Speaker.DialogueAnchor);
            if(projected.z<=.1f){Close();group.alpha=0;return;}
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,projected,null,out var head);
            var bounds=safe.rect;bounds.xMin+=20;bounds.xMax-=20;bounds.yMin+=24;bounds.yMax-=24;
            if(head.x<bounds.xMin-30||head.x>bounds.xMax+30||head.y<bounds.yMin-30||head.y>bounds.yMax+30){Close();group.alpha=0;return;}
            var faceProjection=camera.WorldToScreenPoint(Speaker.FacePosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,faceProjection,null,out var face);
            var radiusProjection=camera.WorldToScreenPoint(Speaker.FacePosition+camera.transform.right*owner.Config.radius);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,radiusProjection,null,out var radius);
            float half=Mathf.Clamp(Mathf.Abs(radius.x-face.x)+16,28,bounds.width*.12f);
            var faceRect=new Rect(face-Vector2.one*half,Vector2.one*(half*2));
            if(Time.unscaledTime>=nextControls){DiscoverControls();nextControls=Time.unscaledTime+.5f;}
            float best=float.PositiveInfinity;Vector2 position=Vector2.zero;int chosen=placement;
            for(int side=0;side<3;side++)for(int vertical=0;vertical<4;vertical++)
            {
                float x=side==0?head.x:face.x+(side==1?-1:1)*(half+size.x*.5f+18);
                float y=vertical==0?head.y+12:vertical==1?head.y-size.y*.25f:vertical==2?head.y-size.y+40:bounds.yMax-size.y;
                var p=new Vector2(Mathf.Clamp(x,bounds.xMin+size.x*.5f,bounds.xMax-size.x*.5f),Mathf.Clamp(y,bounds.yMin,bounds.yMax-size.y));
                var rect=new Rect(p-new Vector2(size.x*.5f,0),size);
                float score=Overlap(rect,faceRect)*1000+Vector2.SqrMagnitude(p-(head+Vector2.up*12))*.01f;
                int candidate=side*4+vertical;if(placement>=0&&candidate!=placement)score+=1200;
                foreach(var control in controls)score+=Overlap(rect,control)*100;
                if(score<best){best=score;position=p;chosen=candidate;}
            }
            placement=chosen;
            panel.anchoredPosition=position;tail.anchorMin=tail.anchorMax=new Vector2(.5f,0);
            float dx=head.x-position.x,dy=head.y-position.y;
            if(Mathf.Abs(dx)>size.x*.5f)tail.anchoredPosition=new Vector2(Mathf.Sign(dx)*(size.x*.5f-2),Mathf.Clamp(dy,24,size.y-24));
            else tail.anchoredPosition=new Vector2(Mathf.Clamp(dx,-size.x*.5f+24,size.x*.5f-24),0);
        }
        void DiscoverControls()
        {
            controls.Clear();
            foreach(var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                var root=button.GetComponentInParent<Canvas>();if(root==null||root.rootCanvas.renderMode!=RenderMode.ScreenSpaceOverlay)continue;
                Reserve(button.transform as RectTransform);
            }
            var joystick=MobileInputUI.FindJoystick();if(joystick!=null&&joystick.isActiveAndEnabled)Reserve(joystick.transform as RectTransform);
            var hint=TrialContextHints.Instance;if(hint!=null&&hint.Opacity>.01f)Reserve(hint.HintPanel);
            var b=safe.rect;controls.Add(new Rect(b.xMax-b.width*.28f,b.yMax-b.height*.27f,b.width*.28f,b.height*.27f));
        }
        void Reserve(RectTransform control)
        {
            if(control==null)return;control.GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,corners[0],null,out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,corners[2],null,out var max);
            var rect=Rect.MinMaxRect(min.x-8,min.y-8,max.x+8,max.y+8);
            if(rect.width<safe.rect.width*.5f&&rect.height<safe.rect.height*.5f)controls.Add(rect);
        }
        static float Overlap(Rect a,Rect b)=>Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin))*Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));
        public void Close(){IsOpen=false;if(group!=null){group.blocksRaycasts=false;group.interactable=false;}}
        public void CloseImmediate(){IsOpen=false;progress=0;FinishClose();}
        void FinishClose(){var previous=Speaker;Speaker=null;if(previous!=null)previous.Resume();if(panel!=null)panel.gameObject.SetActive(false);}
        void OnDisable()=>CloseImmediate();
        void OnDestroy(){if(rounded!=null)Destroy(rounded);if(roundedTexture!=null)Destroy(roundedTexture);}
    }
}
