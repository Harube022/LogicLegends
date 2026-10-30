using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>TruthTable-only world composition. Overlay UI is rendered afterward, at native resolution.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(2000)]
public sealed class TruthTableNightVisibility : MonoBehaviour
{
    public Material visibilityMaterial;
    [Range(30,180)] public float coneWidth=100;
    [Range(2,45)] public float softEdge=18;
    [Range(0,.95f)] public float peripheralDarkness=.78f;
    [Range(0,1)] public float blurStrength=.5f;
    [Range(0,4)] public float blurPixels=1.3f;
    [Range(1,10)] public float nearClearRadius=4;
    [Range(5,60)] public float nearbyLampDistance=30;
    [Range(.15f,.8f)] public float lampPoolFraction=.47f;
    [Range(0,15)] public float facingLightBrightness=5;
    [Range(8,40)] public float facingLightRange=24;
    public bool effectEnabled=true;
    public RenderTexture WorldTexture=>world;
    public Material RuntimeMaterial=>material;
    public float AppliedStrength {get;private set;}
    public int ProtectedLampCount {get;private set;}
    Camera view,presenter;
    Light facingLight;
    RenderTexture previousTarget,world;
    Material material;
    UnityEngine.UI.RawImage image;
    GameObject display;
    float nextLampRefresh;
    readonly Vector4[] pools=new Vector4[6];
    TruthTableDuskLighting lighting;
    TruthTableStageClock clock;
    void OnEnable(){RenderPipelineManager.endCameraRendering+=CameraFinished;}
    void LateUpdate()
    {
        if(SceneManager.GetActiveScene().name!="TruthTable")return;
        if(view==null)
        {
            var camera=Camera.main;if(camera==null||visibilityMaterial==null)return;
            Initialize(camera);
        }
        if(world==null||world.width!=Screen.width||world.height!=Screen.height)Resize();
        if(world==null)return;
        var player=Player.LocalInstance;
        if(player==null)return;
        if(clock==null){var doors=GetComponentInChildren<TruthTableDoorInteraction>(true);if(doors!=null)clock=doors.GetComponent<TruthTableStageClock>();}
        // Reading and answering never compete with a directional mask or peripheral blur.
        bool reading=clock!=null&&(clock.IsPlayerAtBoard||clock.IsQuizOpen||clock.IsTutorialOpen||clock.IsGameOver||clock.IsCompleted);
        AppliedStrength=effectEnabled&&!reading&&Time.timeScale>0?1:0;
        var forward=player.transform.forward;forward.y=0;forward.Normalize();
        // A faint, unshadowed forward fill makes the cone readable even on unlit paths.
        facingLight.enabled=effectEnabled;facingLight.intensity=facingLightBrightness;facingLight.range=facingLightRange;
        facingLight.spotAngle=Mathf.Min(179,coneWidth);facingLight.innerSpotAngle=Mathf.Max(1,coneWidth-softEdge*2);
        facingLight.transform.SetPositionAndRotation(player.transform.position+Vector3.up*1.9f+forward*.35f,Quaternion.LookRotation((forward-Vector3.up*.35f).normalized));
        material.SetVector("_Player",player.transform.position);
        material.SetVector("_Forward",forward);
        float half=coneWidth*.5f;
        material.SetVector("_Cone",new Vector4(Mathf.Cos(Mathf.Max(1,half-softEdge)*Mathf.Deg2Rad),Mathf.Cos(Mathf.Min(179,half+softEdge)*Mathf.Deg2Rad),peripheralDarkness,blurStrength));
        material.SetFloat("_Strength",AppliedStrength);material.SetFloat("_BlurPixels",blurPixels);material.SetFloat("_NearRadius",nearClearRadius);
        if(Time.unscaledTime>=nextLampRefresh)
        {
            nextLampRefresh=Time.unscaledTime+.15f;
            if(lighting==null)lighting=GetComponent<TruthTableDuskLighting>();
            for(int k=0;k<6;k++)pools[k]=Vector4.zero;
            ProtectedLampCount=0;
            // Six closest actual light pools, no allocations and no changes to the lights themselves.
            if(lighting!=null)foreach(var lamp in lighting.LampLights)
            {
                if(lamp==null||!lamp.enabled||lamp.intensity<=0)continue;
                float distance=(lamp.transform.position-player.transform.position).sqrMagnitude;
                if(distance>nearbyLampDistance*nearbyLampDistance)continue;
                int at=ProtectedLampCount;
                if(at>=6){at=5;if(distance>Distance(pools[at],player.transform.position))continue;}
                while(at>0&&distance<Distance(pools[at-1],player.transform.position)){if(at<6)pools[at]=pools[at-1];at--;}
                var p=lamp.transform.position;pools[at]=new Vector4(p.x,p.y,p.z,Mathf.Max(2,lamp.range*lampPoolFraction));
                ProtectedLampCount=Mathf.Min(6,ProtectedLampCount+1);
            }
            material.SetVectorArray("_Lamps",pools);material.SetInt("_LampCount",ProtectedLampCount);
        }
        presenter.depth=view.depth+1;
    }
    static float Distance(Vector4 a,Vector3 b){return (new Vector3(a.x,a.y,a.z)-b).sqrMagnitude;}
    void Initialize(Camera camera)
    {
        view=camera;previousTarget=view.targetTexture;
        material=new Material(visibilityMaterial){name="Truth Table Night Visibility (Runtime)"};
        display=new GameObject("TruthTable World Presentation",typeof(RectTransform),typeof(Canvas));display.transform.SetParent(transform,false);
        var canvas=display.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=-30000;
        var go=new GameObject("World image (no raycasts)",typeof(RectTransform),typeof(UnityEngine.UI.RawImage));go.transform.SetParent(display.transform,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        image=go.GetComponent<UnityEngine.UI.RawImage>();image.raycastTarget=false;image.material=material;
        var cameraObject=new GameObject("TruthTable UI Presentation Camera");cameraObject.transform.SetParent(transform,false);presenter=cameraObject.AddComponent<Camera>();
        presenter.cullingMask=0;presenter.clearFlags=CameraClearFlags.SolidColor;presenter.backgroundColor=Color.black;presenter.depth=view.depth+1;presenter.allowHDR=false;presenter.allowMSAA=false;
        var settings=presenter.GetUniversalAdditionalCameraData();settings.renderPostProcessing=false;settings.requiresColorTexture=false;settings.requiresDepthTexture=false;settings.renderShadows=false;
        var fill=new GameObject("TruthTable Facing Visibility Light");fill.transform.SetParent(transform,false);facingLight=fill.AddComponent<Light>();
        facingLight.type=LightType.Spot;facingLight.color=new Color(.64f,.73f,1);facingLight.shadows=LightShadows.None;
        Resize();
    }
    void Resize()
    {
        if(Screen.width<16||Screen.height<16)return;
        if(world!=null){view.targetTexture=previousTarget;if(RenderTexture.active==world)RenderTexture.active=previousTarget;world.Release();Destroy(world);}
        world=new RenderTexture(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32){name="TruthTable World",filterMode=FilterMode.Bilinear};world.Create();
        view.targetTexture=world;image.texture=world;
    }
    void CameraFinished(ScriptableRenderContext context,Camera camera)
    {
        if(camera!=view||material==null)return;
        material.SetVector("_ViewPosition",view.transform.position);
        // RawImage uses upright UI UVs; do not apply the render-target Y flip twice.
        material.SetMatrix("_WorldFromClip",(GL.GetGPUProjectionMatrix(view.projectionMatrix,false)*view.worldToCameraMatrix).inverse);
    }
    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering-=CameraFinished;
        if(view!=null)view.targetTexture=previousTarget;
        if(facingLight!=null)Destroy(facingLight.gameObject);facingLight=null;
        if(presenter!=null)Destroy(presenter.gameObject);if(display!=null)Destroy(display);
        if(world!=null){if(RenderTexture.active==world)RenderTexture.active=previousTarget;world.Release();Destroy(world);}if(material!=null)Destroy(material);
        view=null;presenter=null;image=null;world=null;material=null;display=null;AppliedStrength=0;
    }
}
