using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A read-only map modal. Reuses the live map and restores its exact dock on close.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1500)]
public sealed class ExpandableMinimap : MonoBehaviour, IPointerClickHandler
{
    public static ExpandableMinimap OpenMap { get; private set; }
    public bool IsOpen { get; private set; }
    public Action<bool> ViewChanged;
    public RectTransform Panel => (RectTransform)transform;
    Transform originalParent;
    Vector2 anchorMin,anchorMax,pivot,position,size;
    Vector3 scale;
    int sibling;
    GameObject overlay;
    RectTransform safe, viewport;
    CanvasGroup mapRaycasts;
    bool raycastBefore, blockedBefore, playerBefore, cursorBefore;
    CursorLockMode cursorLockBefore;
    float timeScaleBefore;
    Player player;
    readonly List<Behaviour> cameraInputs = new List<Behaviour>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { OpenMap=null; }

    public static ExpandableMinimap Attach(RectTransform panel)
    {
        var map = panel.GetComponent<ExpandableMinimap>();
        if (map == null) map = panel.gameObject.AddComponent<ExpandableMinimap>();
        var hit = panel.GetComponent<Image>();
        if (hit == null) { hit=panel.gameObject.AddComponent<Image>();hit.color=Color.clear; }
        hit.raycastTarget = true;
        // Taps on the small diagram inspect the map; they must not edit node answers.
        foreach (var button in panel.GetComponentsInChildren<Button>(true)) button.interactable = false;
        return map;
    }
    public void OnPointerClick(PointerEventData data)
    {
        data.Use(); if (data.button == PointerEventData.InputButton.Left) Open();
    }
    public void Open()
    {
        if (IsOpen || OpenMap != null || !gameObject.activeInHierarchy || GameInput.Instance != null && GameInput.Instance.GameplayInputBlocked) return;
        originalParent=transform.parent;sibling=transform.GetSiblingIndex();
        anchorMin=Panel.anchorMin;anchorMax=Panel.anchorMax;pivot=Panel.pivot;position=Panel.anchoredPosition;size=Panel.sizeDelta;scale=Panel.localScale;
        overlay = new GameObject("Expanded Map",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=overlay.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=4000;
        var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var shade=overlay.AddComponent<Image>();shade.color=new Color(.01f,.025f,.04f,.94f);
        safe=StageUITheme.Rect("Map Safe Area",overlay.transform);StageUITheme.Stretch(safe,Vector2.zero,Vector2.one);safe.gameObject.AddComponent<Crystal.SafeArea>();
        var title=StageUITheme.Text("Map heading",safe,"STAGE MAP",36);StageUITheme.Stretch(title.rectTransform,new Vector2(.08f,.89f),new Vector2(.92f,.97f));
        viewport=StageUITheme.Rect("Map viewport",safe);StageUITheme.Stretch(viewport,new Vector2(.06f,.16f),new Vector2(.94f,.84f));
        var close=StageUITheme.Button("Close Expanded Map",safe,"Close",Close);
        var closeRect=(RectTransform)close.transform;closeRect.anchorMin=closeRect.anchorMax=new Vector2(.5f,0);closeRect.pivot=new Vector2(.5f,0);closeRect.anchoredPosition=new Vector2(0,24);closeRect.sizeDelta=new Vector2(280,88);
        blockedBefore=GameInput.Instance!=null&&GameInput.Instance.GameplayInputBlocked;GameInput.Instance?.SetGameplayInputBlocked(true);
        player=Player.LocalInstance;playerBefore=player!=null&&player.enabled;if(player!=null)player.ToggleControl(false);
        MobileInputUI.FindJoystick()?.ResetJoystick();
        foreach(var behaviour in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
            if(behaviour.enabled&&(behaviour is Unity.Cinemachine.CinemachineInputAxisController||behaviour is CinemachinePinchZoom||behaviour is ThirdPersonCameraController||behaviour is CameraTargetController))
            {cameraInputs.Add(behaviour);behaviour.enabled=false;}
        cursorLockBefore=Cursor.lockState;cursorBefore=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        timeScaleBefore=Time.timeScale;Time.timeScale=0;
        IsOpen=true;OpenMap=this;
        Panel.SetParent(viewport,false);Panel.anchorMin=Panel.anchorMax=Panel.pivot=new Vector2(.5f,.5f);Panel.anchoredPosition=Vector2.zero;
        mapRaycasts=GetComponent<CanvasGroup>();
        if(mapRaycasts==null)mapRaycasts=gameObject.AddComponent<CanvasGroup>();
        raycastBefore=mapRaycasts.blocksRaycasts;mapRaycasts.blocksRaycasts=false;
        Canvas.ForceUpdateCanvases();Layout();ViewChanged?.Invoke(true);
    }
    void LateUpdate()
    {
        // Rebuilt diagrams can add fresh node graphics after Attach.
        foreach(var graphic in GetComponentsInChildren<Graphic>(true))
            if(graphic.transform!=transform)graphic.raycastTarget=false;
        if(IsOpen) Layout();
    }
    void Layout()
    {
        if(viewport==null||size.x<=0||size.y<=0)return;
        Panel.sizeDelta=size;
        Panel.localScale=Vector3.one*Mathf.Min(viewport.rect.width/size.x,viewport.rect.height/size.y);
        Panel.anchoredPosition=Vector2.zero;
    }
    public void Close()
    {
        if(!IsOpen)return;IsOpen=false;if(OpenMap==this)OpenMap=null;
        ViewChanged?.Invoke(false);
        if(originalParent!=null)
        {
            Panel.SetParent(originalParent,false);Panel.SetSiblingIndex(sibling);Panel.anchorMin=anchorMin;Panel.anchorMax=anchorMax;Panel.pivot=pivot;
            Panel.anchoredPosition=position;Panel.sizeDelta=size;Panel.localScale=scale;
        }
        if(mapRaycasts!=null)mapRaycasts.blocksRaycasts=raycastBefore;
        GameInput.Instance?.SetGameplayInputBlocked(blockedBefore);if(player!=null)player.ToggleControl(playerBefore);
        MobileLookInput.ResetDelta();
        foreach(var input in cameraInputs)if(input!=null)input.enabled=true;cameraInputs.Clear();
        Cursor.lockState=cursorLockBefore;Cursor.visible=cursorBefore;
        Time.timeScale=timeScaleBefore;
        if(overlay!=null){overlay.SetActive(false);Destroy(overlay);}overlay=null;
    }
    void OnDisable(){if(IsOpen)Close();}
    void OnDestroy(){if(IsOpen)Close();}
}
