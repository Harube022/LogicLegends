using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem; 

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial UI Elements")]
    [SerializeField] private GameObject tutorialOverlay;
    [SerializeField] private RectTransform pointer; 
    [SerializeField] private Text instructionText; 
    
    [Header("Objectives Panel Elements")]
    [SerializeField] private Canvas objectivesPanelCanvas; 
    [SerializeField] private TextMeshProUGUI objectiveText; 

    [Header("Gameplay Buttons (Target UI)")]
    [SerializeField] private Canvas joystickCanvas;
    [SerializeField] private Canvas jumpButtonCanvas;
    [SerializeField] private Canvas interactButtonCanvas;

    [Header("Visual Targets for Pointer")]
    [SerializeField] private RectTransform joystickVisualTarget; // <-- NEW: To target the exact circle
    [SerializeField] private Button jumpButton;
    [SerializeField] private Button interactButton;

    private int currentStep = 0;
    private Vector3 pointerBasePos;
    private bool isPointerActive = false;
    private bool bobHorizontally = false; 

    void Start()
    {
        StartCoroutine(TutorialFlow());
    }

    void Update()
    {
        if (isPointerActive && pointer != null)
        {
            float bobOffset = Mathf.Sin(Time.time * 6f) * 15f;
            
            if (bobHorizontally)
            {
                pointer.position = pointerBasePos + new Vector3(bobOffset, 0, 0);
            }
            else
            {
                pointer.position = pointerBasePos + new Vector3(0, bobOffset, 0);
            }
        }
    }

    IEnumerator TutorialFlow()
    {
        objectivesPanelCanvas.overrideSorting = true; 

        // --- PHASE 1: MOVEMENT ---
        currentStep = 1;
        tutorialOverlay.SetActive(true);
        
        joystickCanvas.overrideSorting = true;
        jumpButton.interactable = false;
        interactButton.interactable = false;
        
        instructionText.text = "Press and move the joystick to move the character";
        objectiveText.text = "Task: Move around the courtyard"; 

        // <-- UPDATED: Now targeting the exact visual circle
        PositionPointer(joystickVisualTarget);

        yield return new WaitUntil(() => currentStep > 1);

        isPointerActive = false;
        pointer.gameObject.SetActive(false); 
        instructionText.text = ""; 
        yield return new WaitForSeconds(1.5f);
        
        joystickCanvas.overrideSorting = false;

        // --- PHASE 2: JUMP ---
        jumpButtonCanvas.overrideSorting = true;
        jumpButton.interactable = true;
        
        instructionText.text = "Press the jump button to JUMP";
        objectiveText.text = "Task: Try jumping in the air"; 

        // <-- UPDATED: Target the button directly
        PositionPointer(jumpButton.GetComponent<RectTransform>());

        bool jumpClicked = false;
        jumpButton.onClick.AddListener(() => jumpClicked = true);

        yield return new WaitUntil(() => jumpClicked);
        
        jumpButtonCanvas.overrideSorting = false;
        jumpButton.onClick.RemoveAllListeners();
        isPointerActive = false;
        pointer.gameObject.SetActive(false);
        instructionText.text = ""; 
        currentStep++;
        
        yield return new WaitForSeconds(0.5f); 

        // --- PHASE 3: INTERACT ---
        interactButtonCanvas.overrideSorting = true;
        interactButton.interactable = true;

        instructionText.text = "Press the hand button to pick up objects/Interact";
        objectiveText.text = "Task: Learn how to interact"; 

        // <-- UPDATED: Target the button directly
        PositionPointer(interactButton.GetComponent<RectTransform>());

        bool interactClicked = false;
        interactButton.onClick.AddListener(() => interactClicked = true);

        yield return new WaitUntil(() => interactClicked);

        interactButtonCanvas.overrideSorting = false;
        interactButton.onClick.RemoveAllListeners();
        isPointerActive = false;
        pointer.gameObject.SetActive(false);
        instructionText.text = ""; 
        
        // --- PHASE 4: THE PARCHMENT GUIDE ---
        instructionText.text = "This scroll is your guide. Always check it for your current tasks!\n(Tap anywhere to continue)";
        
        pointer.gameObject.SetActive(true);
        pointer.pivot = new Vector2(0.5f, 0.5f);

        Vector3[] objCorners = new Vector3[4];
        objectivesPanelCanvas.GetComponent<RectTransform>().GetWorldCorners(objCorners);
        
        Vector3 rightCenter = new Vector3(
            objCorners[3].x, 
            (objCorners[0].y + objCorners[1].y) / 2f, 
            objCorners[0].z
        );

        float sideHoverOffset = 60f; 
        pointerBasePos = rightCenter + new Vector3(sideHoverOffset, 0, 0);
        pointer.position = pointerBasePos;
        
        // <-- Adjust this 90 to something else if the arrow doesn't point perfectly left
        pointer.localRotation = Quaternion.Euler(0, 0, -90); 
        
        bobHorizontally = true;
        isPointerActive = true;

        yield return new WaitForSeconds(0.5f);

        yield return new WaitUntil(() => 
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        );

        // --- CLEANUP ---
        tutorialOverlay.SetActive(false);
        pointer.gameObject.SetActive(false);
        isPointerActive = false;
        instructionText.text = ""; 
        objectivesPanelCanvas.overrideSorting = false; 
        objectiveText.text = "Go To Logical Expressions Realm Through the Portal"; 
        
        Debug.Log("Tutorial Complete!");
    }

    private void PositionPointer(RectTransform target)
    {
        bobHorizontally = false; 
        
        pointer.gameObject.SetActive(true);
        pointer.pivot = new Vector2(0.5f, 0.5f);
        pointer.anchorMin = new Vector2(0.5f, 0.5f);
        pointer.anchorMax = new Vector2(0.5f, 0.5f);

        Vector3[] targetCorners = new Vector3[4];
        target.GetWorldCorners(targetCorners);

        Vector3 topCenter = new Vector3(
            (targetCorners[0].x + targetCorners[3].x) / 2f, 
            targetCorners[1].y,                             
            targetCorners[0].z                              
        );

        float hoverHeight = 40f; 
        pointerBasePos = topCenter + new Vector3(0, hoverHeight, 0);
        pointer.position = pointerBasePos;
        
        // <-- Adjust this 0 to something else (like -45) if your arrow sprite looks diagonal!
        pointer.localRotation = Quaternion.Euler(0, 0, 0); 
        
        isPointerActive = true; 
    }

    public void CompleteMovementStep()
    {
        if (currentStep == 1)
        {
            currentStep = 2;
        }
    }
}