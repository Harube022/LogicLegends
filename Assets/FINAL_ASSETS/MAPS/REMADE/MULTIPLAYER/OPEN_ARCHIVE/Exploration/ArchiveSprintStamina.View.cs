using LogicLegends.OpenArchive.Topics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Exploration
{
    public sealed partial class ArchiveSprintStamina
    {
        public bool HudVisible => staminaRoot != null && staminaRoot.gameObject.activeInHierarchy;
        public RectTransform HudCard => staminaCard;
        RectTransform staminaRoot,staminaCard,staminaFill,joystickRect;
        Canvas staminaCanvas;
        TMP_Text staminaLabel,staminaValue;
        UnityEngine.UI.Image staminaColor;
        float nextPlacement;
        readonly Vector3[] joystickCorners = new Vector3[4];
        void BuildStaminaHud()
        {
            var go = new GameObject("Archive_Stamina_HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
            go.transform.SetParent(transform,false); staminaRoot = go.GetComponent<RectTransform>();
            staminaCanvas = go.GetComponent<Canvas>(); staminaCanvas.renderMode = RenderMode.ScreenSpaceOverlay; staminaCanvas.sortingOrder = 139;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var group = go.GetComponent<CanvasGroup>(); group.interactable = group.blocksRaycasts = false;
            staminaCard = StageUITheme.Rect("Sprint_Stamina",staminaRoot);
            staminaCard.anchorMin = staminaCard.anchorMax = Vector2.zero; staminaCard.pivot = new Vector2(.5f,0);
            staminaCard.sizeDelta = new Vector2(260,46);
            staminaCard.gameObject.AddComponent<ArchiveHudCardGraphic>().raycastTarget = false;
            staminaLabel = StaminaText("Stamina_State",new Vector2(.06f,.47f),new Vector2(.78f,.95f),TextAlignmentOptions.MidlineLeft);
            staminaValue = StaminaText("Stamina_Percent",new Vector2(.79f,.47f),new Vector2(.94f,.95f),TextAlignmentOptions.MidlineRight);
            var track = StaminaRect("Stamina_Track",staminaCard,new Vector2(.06f,.17f),new Vector2(.94f,.36f));
            var background = track.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(.01f,.04f,.045f); background.raycastTarget = false;
            staminaFill = StaminaRect("Stamina_Fill",track,Vector2.zero,Vector2.one);
            staminaColor = staminaFill.gameObject.AddComponent<UnityEngine.UI.Image>(); staminaColor.raycastTarget = false;
        }
        static RectTransform StaminaRect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var rect = StageUITheme.Rect(name,parent); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        TMP_Text StaminaText(string name,Vector2 min,Vector2 max,TextAlignmentOptions alignment)
        {
            var text = StaminaRect(name,staminaCard,min,max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = StageUITheme.ReadingFont; text.fontSize = 15; text.fontStyle = FontStyles.Bold;
            text.color = new Color(1,.95f,.81f); text.raycastTarget = false; text.richText = false;
            text.alignment = alignment; text.textWrappingMode = TextWrappingModes.NoWrap; return text;
        }
        void PresentStamina()
        {
            bool show = !Suspended && (ArchiveTopicUI.Current == null || ArchiveTopicUI.Current.ReadingStation == null) &&
                (ArchiveLearningUI.Current == null || !ArchiveLearningUI.Current.GuideVisible && !ArchiveLearningUI.Current.BarrierVisible);
            if (staminaRoot == null) { if (!show) return; BuildStaminaHud(); }
            staminaRoot.gameObject.SetActive(show); if (!show) return;
            staminaLabel.text = Exhausted ? "RECOVERING" : Sprinting ? "SPRINTING" : "STAMINA";
            staminaValue.text = Mathf.CeilToInt(Fraction*100)+"%";
            staminaFill.anchorMax = new Vector2(Fraction,1);
            staminaColor.color = Exhausted ? new Color(.98f,.27f,.22f) : Fraction <= .25f ? new Color(1,.70f,.22f) : new Color(.27f,.86f,.68f);
            if (Time.unscaledTime < nextPlacement) return; nextPlacement = Time.unscaledTime+.25f;
            if (joystickRect == null) { var joystick = MobileInputUI.FindJoystick(); if (joystick != null) joystickRect = joystick.transform as RectTransform; }
            float scale = Mathf.Max(.01f,staminaCanvas.scaleFactor),width = staminaCard.sizeDelta.x*scale;
            Rect safe = Screen.safeArea; float x = safe.xMin+width*.5f+20*scale,y = safe.yMin+40*scale;
            if (joystickRect != null && joystickRect.gameObject.activeInHierarchy)
            {
                joystickRect.GetWorldCorners(joystickCorners);
                x = (joystickCorners[0].x+joystickCorners[2].x)*.5f; y = joystickCorners[2].y+12*scale;
            }
            x = Mathf.Clamp(x,safe.xMin+width*.5f+8*scale,safe.xMax-width*.5f-8*scale);
            y = Mathf.Clamp(y,safe.yMin+8*scale,safe.yMax-54*scale);
            staminaCard.anchoredPosition = new Vector2(x/scale,y/scale);
        }
    }
}
