using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public partial class TruthTableDoorInteraction
{
    Transform[] houseRoots;
    bool[] doorOpened;
    Vector3[] doorBubblePositions;
    HandThoughtBubble[] doorBubbles;
    CanvasGroup[] doorBubbleVisibility;
    Bounds[] houseLocalBounds;
    float[] houseCycleElapsed, houseVisibility;
    const float HouseIndicatorHoldSeconds = 10f, HouseIndicatorHiddenSeconds = 60f, HouseIndicatorFadeSeconds = 1f;
    RectTransform quizCard;
    TMP_Text correctAnswerText;
    Coroutine feedbackCloseRoutine;
    readonly Dictionary<GameObject,bool> hiddenQuizControls=new Dictionary<GameObject,bool>();

    public bool UsesHandBubbles => isActiveAndEnabled && IsTruthTableActive;
    bool CanUseWorld => UsesHandBubbles && IsStageRunning && !panelOpen && !stageClock.IsTutorialOpen &&
        Time.timeScale > 0 && player != null && player.enabled && gameInput != null && !gameInput.GameplayInputBlocked &&
        (TruthTableControlsOnboarding.Active == null || !TruthTableControlsOnboarding.Active.IsActive);

    void InitializePresentation()
    {
        // Center the quiz on the full Canvas, independently of safe-area insets.
        // The quiz needs its own raycaster above gameplay canvases. A transparent
        // SafeArea or mobile-look surface must never intercept its finger events.
        var canvas = StageFullScreenPanels.RootCanvas(blankPanel.transform);
        blankPanel.transform.SetParent(canvas.transform, false);
        blankPanel.transform.SetAsLastSibling();
        var modalCanvas = blankPanel.GetComponent<Canvas>();
        if (modalCanvas == null) modalCanvas = blankPanel.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 300;
        if (blankPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            blankPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var modalGroup = blankPanel.GetComponent<CanvasGroup>();
        if (modalGroup == null) modalGroup = blankPanel.AddComponent<CanvasGroup>();
        modalGroup.ignoreParentGroups = true;
        modalGroup.interactable = true;
        modalGroup.blocksRaycasts = true;
        var panelRect = (RectTransform)blankPanel.transform;
        StageUITheme.Stretch(panelRect, Vector2.zero, Vector2.one);
        panelRect.localScale = Vector3.one;
        quizCard = (RectTransform)questionText.transform.parent;
        correctAnswerText = StageUITheme.Text("Correct Answer",quizCard,"",26,true);
        correctAnswerText.color = new Color(1,.9f,.6f); correctAnswerText.gameObject.SetActive(false);
        var shade = blankPanel.GetComponent<UnityEngine.UI.Image>();
        if(shade==null)shade=blankPanel.AddComponent<UnityEngine.UI.Image>();
        // Keep Settings/Admin reachable outside the card. World interactions
        // remain blocked by panelOpen and GameInput, independently of UI rays.
        shade.color=new Color(.01f,.025f,.04f,.9f);shade.raycastTarget=false;
        var cardImage = quizCard.GetComponent<UnityEngine.UI.Image>();
        if (cardImage != null) cardImage.raycastTarget = true;
        foreach (var label in blankPanel.GetComponentsInChildren<TMP_Text>(true)) label.raycastTarget = false;
        foreach (var button in answerButtons)
            if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        if (closeButton != null && closeButton.targetGraphic != null) closeButton.targetGraphic.raycastTarget = true;
        if (closeButton != null)
        {
            var backLabel = closeButton.GetComponentInChildren<TMP_Text>(true);
            if (backLabel != null) backLabel.text = "Back";
        }
        doorBubbles = new HandThoughtBubble[doors.Length];
        doorBubbleVisibility = new CanvasGroup[doors.Length];
        houseLocalBounds = new Bounds[doors.Length];
        houseCycleElapsed = new float[doors.Length];
        houseVisibility = new float[doors.Length];
        for(int i=0;i<doors.Length;i++)
        {
            doorBubbles[i]=HandThoughtBubble.Create(houseRoots[i],"Door Interaction Bubble",yellowMarker:true);
            doorBubbleVisibility[i]=doorBubbles[i].gameObject.AddComponent<CanvasGroup>();
            doorBubbleVisibility[i].blocksRaycasts=false;
            doorBubbleVisibility[i].interactable=false;
            houseVisibility[i]=1;
            houseLocalBounds[i]=HouseBounds(i);
            doorBubbles[i].gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if(panelOpen)LayoutQuiz();
        if(doorBubbles==null)return;
        for(int i=0;i<doors.Length;i++)
        {
            bool available=CanUseWorld&&!answered[i]&&doors[i]!=null&&doors[i].enabled;
            bool near=available&&DoorDistanceSquared(i)<=interactionRange*interactionRange;
            doorBubbles[i].Show(available,near,doorBubblePositions[i]);
            // Proximity overrides only the visual cycle, never interaction eligibility.
            bool inside=available&&houseLocalBounds[i].Contains(houseRoots[i].InverseTransformPoint(player.transform.position+Vector3.up*1.5f));
            UpdateHouseIndicator(i,available,near||inside,Time.unscaledDeltaTime);
        }
    }
    Bounds HouseBounds(int index)
    {
        var house=houseRoots[index];
        Bounds bounds=new Bounds();bool found=false;
        foreach(var mesh in house.GetComponentsInChildren<MeshFilter>(true))
        {
            if(mesh.sharedMesh==null||mesh.transform==doors[index].transform)continue;
            var local=mesh.sharedMesh.bounds;
            for(int corner=0;corner<8;corner++)
            {
                var point=house.InverseTransformPoint(mesh.transform.TransformPoint(local.center+Vector3.Scale(local.extents,
                    new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));
                if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
    void UpdateHouseIndicator(int index,bool eligible,bool nearby,float delta)
    {
        if(!eligible)
        {
            // A newly available door always starts with a full visible interval.
            houseCycleElapsed[index]=0;houseVisibility[index]=1;
            doorBubbleVisibility[index].alpha=1;return;
        }
        float target=1;
        if(!nearby)
        {
            float period=HouseIndicatorHoldSeconds+HouseIndicatorHiddenSeconds+2*HouseIndicatorFadeSeconds;
            float elapsed=houseCycleElapsed[index]=(houseCycleElapsed[index]+delta)%period;
            if(elapsed>HouseIndicatorHoldSeconds&&elapsed<=HouseIndicatorHoldSeconds+HouseIndicatorFadeSeconds)
                target=1-(elapsed-HouseIndicatorHoldSeconds)/HouseIndicatorFadeSeconds;
            else if(elapsed>HouseIndicatorHoldSeconds+HouseIndicatorFadeSeconds&&elapsed<=period-HouseIndicatorFadeSeconds)
                target=0;
            else if(elapsed>period-HouseIndicatorFadeSeconds)
                target=(elapsed-(period-HouseIndicatorFadeSeconds))/HouseIndicatorFadeSeconds;
        }
        // Freeze the cycle near/inside a house; fade back in if approached while hidden.
        houseVisibility[index]=Mathf.MoveTowards(houseVisibility[index],target,delta/HouseIndicatorFadeSeconds);
        doorBubbleVisibility[index].alpha=Mathf.SmoothStep(0,1,houseVisibility[index]);
    }
    float DoorDistanceSquared(int index)
    {
        var center=player.transform.position+Vector3.up*1.5f;
        return (doors[index].ClosestPoint(center)-center).sqrMagnitude;
    }
    void HideDoorBubbles()
    {
        if(doorBubbles!=null)foreach(var bubble in doorBubbles)if(bubble!=null)bubble.gameObject.SetActive(false);
    }
    public int HouseForBlock(TruthBlock block)
    {
        var at=block.CurrentSpawnPoint!=null?block.CurrentSpawnPoint.position:block.transform.position;
        int result=-1;float distance=float.MaxValue;
        for(int i=0;i<houseRoots.Length;i++)
        {
            float d=(houseRoots[i].position-at).sqrMagnitude;
            if(d<distance){distance=d;result=i;}
        }
        return result;
    }
    public bool IsBlockAvailable(TruthBlock block)
    {
        if(!CanUseWorld||block==null||!block.UsesHandBubble||!block.gameObject.activeInHierarchy||block.IsPlacedInColumn)return false;
        var item=block.GetComponent<GrabbableObject>();
        if(item!=null&&(item.IsHeld||item.isStoredInInventory))return false;
        int house=HouseForBlock(block);
        return house>=0&&doorOpened[house];
    }
    // Consume the hand action before generic inventory drop/grab handles the same tap.
    public TruthBlock NearestAvailableBlock()
    {
        TruthBlock closest=null;float distance=4;
        foreach(var block in TruthBlock.ActiveBlocks)
        {
            if(!IsBlockAvailable(block))continue;
            float d=block.InteractionDistanceSquared(player);
            if(d<=distance){distance=d;closest=block;}
        }
        return closest;
    }
    public static bool TryHandInteraction()
    {
        var owner=Active;
        if(owner==null||!owner.UsesHandBubbles)return false;
        if(!owner.CanUseWorld)return true;
        int door=-1;float distance=owner.interactionRange*owner.interactionRange;
        for(int i=0;i<owner.doors.Length;i++)
            if(!owner.answered[i]&&owner.doors[i].enabled)
            {float d=owner.DoorDistanceSquared(i);if(d<distance){distance=d;door=i;}}
        if(door>=0){owner.focusedDoor=owner.doors[door];owner.OpenFocusedDoor();return true;}
        TruthBlock closest=owner.NearestAvailableBlock();
        if(closest==null)return false;
        closest.CollectBlock();return true;
    }

    void LayoutQuiz()
    {
        if(quizCard==null)return;
        float width=Mathf.Min(1080,((RectTransform)blankPanel.transform).rect.width-48);
        if(width<=0)return;
        float content=width-96,top=40;
        questionText.font=StageUITheme.ReadingFont;questionText.fontSize=32;questionText.enableAutoSizing=false;
        questionText.color=StageUITheme.Ink;questionText.textWrappingMode=TextWrappingModes.Normal;
        float height=Mathf.Max(76,questionText.GetPreferredValues(questionText.text,content,0).y+12);
        PlaceQuizItem(questionText.rectTransform,top,content,height);top+=height+18;
        for(int i=0;i<4;i++)
        {
            var label=answerTexts[i];label.font=StageUITheme.ReadingFont;label.enableAutoSizing=false;label.fontSize=26;label.textWrappingMode=TextWrappingModes.Normal;
            height=Mathf.Max(68,label.GetPreferredValues(label.text,content-56,0).y+26);
            PlaceQuizItem((RectTransform)answerButtons[i].transform,top,content,height);
            StageUITheme.Stretch(label.rectTransform,Vector2.zero,Vector2.one);label.rectTransform.offsetMin=new Vector2(28,10);label.rectTransform.offsetMax=new Vector2(-28,-10);
            top+=height+12;
        }
        feedbackText.fontSize=28;feedbackText.enableAutoSizing=false;
        PlaceQuizItem(feedbackText.rectTransform,top,content,38);top+=42;
        if(correctAnswerText.gameObject.activeSelf)
        {
            height=Mathf.Max(38,correctAnswerText.GetPreferredValues(correctAnswerText.text,content,0).y+12);
            PlaceQuizItem(correctAnswerText.rectTransform,top,content,height);top+=height+8;
        }
        if(closeButton!=null){PlaceQuizItem((RectTransform)closeButton.transform,top,Mathf.Min(240,content),64);top+=76;}
        quizCard.anchorMin=quizCard.anchorMax=quizCard.pivot=new Vector2(.5f,.5f);
        quizCard.sizeDelta=new Vector2(width,top+44);
        quizCard.anchoredPosition=Vector2.zero;
        quizCard.localScale=Vector3.one*Mathf.Min(1,(((RectTransform)blankPanel.transform).rect.height-96)/(top+44));
    }
    void HideQuizControls()
    {
        hiddenQuizControls.Clear();
        foreach(var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(rect.name=="Joystick"||rect.name=="JumpButton"||rect.name=="InteractButton"||rect.name=="InventoryBar"||rect.name=="TruthTableHelpButton")
            {hiddenQuizControls[rect.gameObject]=rect.gameObject.activeSelf;rect.gameObject.SetActive(false);}
        }
    }
    void RestoreQuizControls()
    {
        foreach(var entry in hiddenQuizControls)if(entry.Key!=null&&(stageClock==null||!stageClock.IsGameOver))entry.Key.SetActive(entry.Value);
        hiddenQuizControls.Clear();
    }
    static void PlaceQuizItem(RectTransform rect,float top,float width,float height)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);
        rect.anchoredPosition=new Vector2(0,-top);rect.sizeDelta=new Vector2(width,height);rect.localScale=Vector3.one;
    }
}
