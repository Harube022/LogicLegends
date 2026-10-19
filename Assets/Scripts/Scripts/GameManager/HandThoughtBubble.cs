using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>The same non-clickable thought-bubble artwork for books, doors and pickups.</summary>
public sealed class HandThoughtBubble : MonoBehaviour
{
    public RectTransform Rect { get; private set; }
    public TMP_Text Marker { get; private set; }
    public CanvasGroup Instruction { get; private set; }
    public UnityEngine.UI.Image HandIcon { get; private set; }
    public float Expansion { get; private set; }
    readonly List<Material> materials = new List<Material>();
    static Sprite circle;
    RectTransform safeArea;
    readonly Vector3[] corners=new Vector3[4];
    readonly List<RectTransform> overlayControls=new List<RectTransform>();
    readonly List<Rect> obstructions=new List<Rect>();
    readonly List<float> candidateX=new List<float>(),candidateY=new List<float>();

    public static HandThoughtBubble Create(Transform parent, string name, string markerName = "Interaction Indicator", bool yellowMarker = false)
    {
        var rect = StageUITheme.Rect(name, parent);
        var view = rect.gameObject.AddComponent<HandThoughtBubble>();
        view.Rect = rect;
        var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 100;
        rect.pivot = new Vector2(.5f,0);
        var surface = Resources.Load<Material>("StageUI/BookBubbleSurface");
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.sprite = Circle(); image.raycastTarget = false; image.material = surface;
        foreach(float size in new[]{13f,7f})
        {
            var dot = StageUITheme.Rect("Thought dot",rect); dot.anchorMin = dot.anchorMax = new Vector2(.28f,0);
            dot.anchoredPosition = new Vector2(size==13?-4:-16,size==13?-5:-17); dot.sizeDelta = Vector2.one*size;
            var art = dot.gameObject.AddComponent<UnityEngine.UI.Image>(); art.sprite = Circle(); art.material = surface; art.raycastTarget = false;
        }
        view.Marker = StageUITheme.Text(markerName,rect,"!",70);
        // The reading face has a distinct dot and stem, unlike the condensed heading face.
        if(yellowMarker)view.Marker.font=StageUITheme.ReadingFont;
        view.Marker.fontStyle = yellowMarker ? FontStyles.Normal : FontStyles.Bold;
        view.Marker.color = yellowMarker ? new Color(1f,.8f,0f) : Color.black;
        view.OverlayText(view.Marker); StageUITheme.Stretch(view.Marker.rectTransform,Vector2.zero,Vector2.one);
        var row = StageUITheme.Rect("Hand instruction",rect); StageUITheme.Stretch(row,Vector2.zero,Vector2.one);
        view.Instruction = row.gameObject.AddComponent<CanvasGroup>(); view.Instruction.blocksRaycasts = false; view.Instruction.interactable = false;
        view.AddText(row,"Use the",-147,104);
        var icon = StageUITheme.Rect("Actual hand button icon",row); icon.anchorMin = icon.anchorMax = new Vector2(.5f,.5f);
        icon.anchoredPosition = new Vector2(-64,0); icon.sizeDelta = new Vector2(44,44);
        view.HandIcon = icon.gameObject.AddComponent<UnityEngine.UI.Image>(); view.HandIcon.material = surface;
        view.HandIcon.preserveAspect = true; view.HandIcon.raycastTarget = false; view.HandIcon.color = new Color(.12f,.18f,.2f);
        view.AddText(row,"button to interact.",78,225); view.SetExpansion(0);
        return view;
    }
    void AddText(Transform parent,string value,float x,float width)
    {
        var text = StageUITheme.Text(value,parent,value,26,true); text.color = Color.black; text.textWrappingMode = TextWrappingModes.NoWrap;
        OverlayText(text); text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f,.5f);
        text.rectTransform.anchoredPosition = new Vector2(x,0); text.rectTransform.sizeDelta = new Vector2(width,70);
    }
    void OverlayText(TMP_Text text)
    {
        var template = Resources.Load<Material>("StageUI/BookBubbleTextOverlay"); if(template==null)return;
        var material = new Material(text.fontSharedMaterial); material.shader = template.shader; material.renderQueue = template.renderQueue;
        text.fontSharedMaterial = material; materials.Add(material);
    }
    public void SetExpansion(float value)
    {
        Expansion = value; Rect.sizeDelta = new Vector2(Mathf.Lerp(105,470,Mathf.SmoothStep(0,1,value)),110);
        Marker.alpha = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,.9f,value));
        Instruction.alpha = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1,value));
        if(HandIcon.sprite==null)
            foreach(var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(button.name=="InteractButton"&&button.image!=null)
                {
                    HandIcon.sprite=button.image.sprite;
                    var safe=button.GetComponentInParent<Crystal.SafeArea>(true);if(safe!=null)safeArea=(RectTransform)safe.transform;
                    break;
                }
    }
    public void Show(bool available,bool near,Vector3 position)
    {
        if(gameObject.activeSelf!=available)gameObject.SetActive(available);
        if(!available){SetExpansion(0);return;}
        Rect.position = position + Vector3.up * (.1f*(1+Mathf.Sin(Time.unscaledTime*2)));
        var camera = Camera.main;
        if(camera!=null)
        {
            Rect.rotation = camera.transform.rotation;
            float scale = Mathf.Clamp(Vector3.Distance(camera.transform.position,Rect.position)*.0009f,.006f,.06f);
            var parentScale = transform.parent.lossyScale;
            Rect.localScale = new Vector3(scale/Mathf.Abs(parentScale.x),scale/Mathf.Abs(parentScale.y),scale/Mathf.Abs(parentScale.z));
        }
        SetExpansion(Mathf.MoveTowards(Expansion,near?1:0,Time.unscaledDeltaTime*3.5f));
        if(camera!=null&&Expansion>.8f)KeepInstructionInView(camera);
    }
    // Crystal towers can be much taller than the gameplay camera. Keep their
    // expanded instructions inside the safe area and clear of mobile controls.
    public void KeepInView(TMP_Text worldLabel=null)
    {
        var camera=Camera.main;if(camera==null)return;
        var screen=camera.WorldToScreenPoint(Rect.position);if(screen.z<=0)return;
        var safe=SafeScreenRect();
        var bounds=ScreenBounds(Rect,camera);
        if(Expansion>.8f&&bounds.width>safe.width*.7f)
            Rect.localScale*=safe.width*.7f/bounds.width;
        KeepInstructionInView(camera,true);
        if(Expansion<=.8f)return;
        if(overlayControls.Count==0)
            foreach(var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var canvas=button.GetComponentInParent<Canvas>();
                if(canvas!=null&&canvas.rootCanvas.renderMode==RenderMode.ScreenSpaceOverlay)
                    overlayControls.Add((RectTransform)button.transform);
            }
        obstructions.Clear();
        foreach(var control in overlayControls)
            if(control!=null&&control.gameObject.activeInHierarchy)obstructions.Add(ScreenBounds(control,null));
        if(worldLabel!=null&&worldLabel.gameObject.activeInHierarchy)
        {
            // Reserve the visible glyphs, rather than a tower label's generously
            // padded layout rectangle, so portrait screens retain usable space.
            worldLabel.ForceMeshUpdate();
            var textBounds=worldLabel.textBounds;
            var a=camera.WorldToScreenPoint(worldLabel.transform.TransformPoint(textBounds.min));
            var b=camera.WorldToScreenPoint(worldLabel.transform.TransformPoint(textBounds.max));
            obstructions.Add(UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y)));
        }
        bounds=ScreenBounds(Rect,camera);
        var half=bounds.size*.5f;var desired=bounds.center;
        candidateX.Clear();candidateY.Clear();candidateX.Add(desired.x);candidateY.Add(desired.y);
        foreach(var obstacle in obstructions)
        {
            candidateX.Add(obstacle.xMin-half.x-8);candidateX.Add(obstacle.xMax+half.x+8);
            candidateY.Add(obstacle.yMin-half.y-8);candidateY.Add(obstacle.yMax+half.y+8);
        }
        var best=desired;float bestCost=float.PositiveInfinity;
        foreach(float x in candidateX)foreach(float y in candidateY)
        {
            var center=new Vector2(Mathf.Clamp(x,safe.xMin+half.x+16,safe.xMax-half.x-16),Mathf.Clamp(y,safe.yMin+half.y+16,safe.yMax-half.y-16));
            var candidate=new Rect(center-half,bounds.size);bool clear=true;
            foreach(var obstacle in obstructions)if(candidate.Overlaps(obstacle)){clear=false;break;}
            float cost=(center-desired).sqrMagnitude;
            if(clear&&cost<bestCost){best=center;bestCost=cost;}
        }
        screen=camera.WorldToScreenPoint(Rect.position);
        Rect.position=camera.ScreenToWorldPoint(screen+new Vector3(best.x-desired.x,best.y-desired.y,0));
    }
    Rect SafeScreenRect()
    {
        if(safeArea==null)return Screen.safeArea;
        safeArea.GetWorldCorners(corners);
        return UnityEngine.Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    Rect ScreenBounds(RectTransform rect,Camera camera)
    {
        rect.GetWorldCorners(corners);
        var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
    }
    void KeepInstructionInView(Camera camera,bool force=false)
    {
        var screen=camera.WorldToScreenPoint(Rect.position);if(screen.z<=0)return;
        Rect.GetWorldCorners(corners);var min=camera.WorldToScreenPoint(corners[0]);var max=camera.WorldToScreenPoint(corners[2]);
        if(!force&&(max.x<0||min.x>Screen.width||max.y<0||min.y>Screen.height))return;
        var safe=SafeScreenRect();
        float dx=Mathf.Max(0,safe.xMin+16-min.x)+Mathf.Min(0,safe.xMax-16-max.x);
        float dy=Mathf.Max(0,safe.yMin+16-min.y)+Mathf.Min(0,safe.yMax-16-max.y);
        Rect.position=camera.ScreenToWorldPoint(screen+new Vector3(dx,dy,0));
    }
    static Sprite Circle()
    {
        if(circle!=null)return circle;
        var texture = new Texture2D(96,96,TextureFormat.RGBA32,false); texture.name = "Interaction Bubble Circle";
        var pixels = new Color[96*96];
        for(int y=0;y<96;y++)for(int x=0;x<96;x++)pixels[y*96+x]=new Color(1,1,1,Mathf.Clamp01(47.5f-Vector2.Distance(new Vector2(x,y),new Vector2(47.5f,47.5f))));
        texture.SetPixels(pixels); texture.Apply(); circle = Sprite.Create(texture,new Rect(0,0,96,96),new Vector2(.5f,.5f),96);return circle;
    }
    void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);}
}
