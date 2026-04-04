using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem; 
using UnityEngine.Events; // ---> NEW: Required for Unity Events! <---

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial UI Elements")]
    [SerializeField] private GameObject tutorialOverlay;
    [SerializeField] private RectTransform pointer; 
    [SerializeField] private Text instructionText; 

    // ---> NEW: Slot for your Pinch Graphic <---
    [Header("Camera Zoom Tutorial")]
    [Tooltip("Drag the UI Image containing your Pinch icon here")]
    [SerializeField] private GameObject pinchTutorialUI;
    
    [Header("Objectives Panel Elements")]
    [SerializeField] private Canvas objectivesPanelCanvas; 
    [SerializeField] private TextMeshProUGUI objectiveText; 
    
    [Header("Toggle Button Elements")]
    [SerializeField] private Canvas toggleButtonCanvas; 
    [SerializeField] private Button objectivesToggleButton; 

    [Header("Gameplay Buttons (Target UI)")]
    [SerializeField] private Canvas joystickCanvas;
    [SerializeField] private Canvas jumpButtonCanvas;
    [SerializeField] private Canvas interactButtonCanvas;

    [Header("Visual Targets for Pointer")]
    [SerializeField] private RectTransform joystickVisualTarget; 
    [SerializeField] private Button jumpButton;
    [SerializeField] private Button interactButton;

    // ---> NEW: A switch that flips when the tutorial is totally done! <---
    [Header("Tutorial Events")]
    public UnityEvent onTutorialComplete;

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

        PositionPointer(interactButton.GetComponent<RectTransform>());

        bool interactClicked = false;
        interactButton.onClick.AddListener(() => interactClicked = true);

        yield return new WaitUntil(() => interactClicked);

        interactButtonCanvas.overrideSorting = false;
        interactButton.onClick.RemoveAllListeners();
        isPointerActive = false;
        pointer.gameObject.SetActive(false);
        instructionText.text = ""; 
        
        yield return new WaitForSeconds(0.5f);

        // ---> NEW PHASE 4: CAMERA ZOOM <---
        instructionText.text = "Pinch two fingers on the right side of the screen to ZOOM the camera.";
        objectiveText.text = "Task: Try zooming the camera"; 

        // Turn on your pinch graphic!
        if (pinchTutorialUI != null) pinchTutorialUI.SetActive(true);

        // Give the player 4.5 seconds to practice zooming in and out
        yield return new WaitForSeconds(4.5f);

        // Hide it and move on
        if (pinchTutorialUI != null) pinchTutorialUI.SetActive(false);
        instructionText.text = ""; 
        
        yield return new WaitForSeconds(0.5f);

        // --- PHASE 5: THE SCROLL EXPLANATION ---
        instructionText.text = "This scroll is your guide always check it for your current task.";
        objectiveText.text = "Task: Read the scroll"; 

        PositionPointer(objectivesPanelCanvas.GetComponent<RectTransform>());

        yield return new WaitForSeconds(4f);

        isPointerActive = false;
        pointer.gameObject.SetActive(false);
        instructionText.text = ""; 
        
        yield return new WaitForSeconds(0.5f);

        // --- PHASE 6: THE TOGGLE BUTTON ---
        toggleButtonCanvas.overrideSorting = true; 
        objectivesToggleButton.interactable = true;

        instructionText.text = "Tap this button to hide or show your tasks anytime!";
        objectiveText.text = "Task: Try closing the scroll"; 

        PositionPointer(objectivesToggleButton.GetComponent<RectTransform>());

        yield return new WaitUntil(() => !objectivesPanelCanvas.gameObject.activeSelf);

        // --- CLEANUP ---
        tutorialOverlay.SetActive(false);
        pointer.gameObject.SetActive(false);
        isPointerActive = false;
        instructionText.text = ""; 
        objectivesPanelCanvas.overrideSorting = false; 
        
        if (!objectivesPanelCanvas.gameObject.activeSelf) 
        {
            objectivesToggleButton.GetComponent<ObjectivesToggle>().ToggleVisibility();
        }

        objectiveText.text = "Go To Logical Expressions Realm Through the Portal"; 
        
        // ---> NEW: Fire the completion event! <---
        onTutorialComplete?.Invoke();
        
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