using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchiveTesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuPlayerControlsVerification
    {
        [Serializable] class Result { public string name, detail; public bool passed; }
        [Serializable] class Report
        {
            public string checkedUtc;
            public bool passed;
            public List<Result> checks = new List<Result>();
            public string limits = "Unity Editor: injected keyboard events and shared UI pointer callbacks drive the real Player and CharacterController. Physical Android touch and multiplayer are not verified.";
        }

        [MenuItem("Tools/Logic Legends/Khezu Phase 1/Verify Player Controls In Play Mode")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var session = Object.FindFirstObjectByType<OpenArchiveExplorationSession>();
            if (session == null) throw new InvalidOperationException("No local exploration controls in this scene.");
            session.StartCoroutine(Verify(session));
        }

        static Button Button(string name) { return Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name); }
        static IEnumerator Verify(OpenArchiveExplorationSession session)
        {
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("o") };
            Action<string, bool, string> check = (name, passed, detail) => report.checks.Add(new Result { name = name, passed = passed, detail = detail });
            var player = session.Explorer;
            var controller = player.GetComponent<CharacterController>();
            var monster = Object.FindFirstObjectByType<KhezuAwakeningTestController>();
            var monsterPosition = monster.transform.position;
            var monsterRotation = monster.transform.rotation;
            var keyboard = Keyboard.current;
            bool complete = false;
            try
            {
                session.ReturnToSpawn();
                yield return new WaitForSecondsRealtime(.3f);
                check("Visible, grounded local character", Player.LocalInstance == player && controller.isGrounded && player.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled), player.transform.position.ToString("F3"));
                check("One gameplay camera, listener and EventSystem", Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled) == 1 && Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled) == 1 && Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "Existing exploration follow camera replaces fixed review view.");
                var start = player.transform.position;
                if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSecondsRealtime(.4f);
                float distance = Vector3.Distance(start, player.transform.position);
                if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(.1f);
                check("W key moves character", keyboard != null && distance > 1, distance.ToString("F2") + " m");
                check("Keyboard release clears movement", GameInput.Instance.GetMovementVectorNormalized() == Vector2.zero, "Original GameInput actions.");

                session.ReturnToSpawn();
                var joystick = MobileInputUI.FindJoystick();
                var rect = (RectTransform)joystick.transform;
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(Vector3.up * rect.rect.height * .4f)), button = PointerEventData.InputButton.Left };
                start = player.transform.position;
                ExecuteEvents.Execute(joystick.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.4f);
                ExecuteEvents.Execute(joystick.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                check("Shared joystick moves character", Vector3.Distance(start, player.transform.position) > 1, Vector3.Distance(start, player.transform.position).ToString("F2") + " m");
                check("Joystick release clears movement", GameInput.Instance.GetMovementVectorNormalized() == Vector2.zero, "Original MobileInputUI callback.");

                session.ReturnToSpawn(); yield return new WaitForSecondsRealtime(.2f);
                float initialY = player.transform.position.y, peak = initialY;
                if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                float end = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < end) { peak = Mathf.Max(peak, player.transform.position.y); yield return null; }
                check("Space jumps", keyboard != null && peak - initialY > 1, "Peak rise " + (peak - initialY).ToString("F2") + " m");
                session.ReturnToSpawn(); yield return new WaitForSecondsRealtime(.2f);
                initialY = peak = player.transform.position.y;
                Button("JumpButton").onClick.Invoke();
                end = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < end) { peak = Mathf.Max(peak, player.transform.position.y); yield return null; }
                check("Shared jump button jumps", peak - initialY > 1, "Peak rise " + (peak - initialY).ToString("F2") + " m");

                var target = player.transform.Find("CameraTarget");
                float yaw = target.eulerAngles.y;
                var look = Object.FindFirstObjectByType<MobileLookInput>();
                pointer.position = new Vector2(Screen.width * .7f, Screen.height * .5f);
                ExecuteEvents.Execute(look.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                pointer.position += new Vector2(160, 0);
                ExecuteEvents.Execute(look.gameObject, pointer, ExecuteEvents.dragHandler);
                yield return new WaitForSecondsRealtime(.15f);
                ExecuteEvents.Execute(look.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                check("Right-side drag turns camera", Mathf.Abs(Mathf.DeltaAngle(yaw, target.eulerAngles.y)) > 5, Mathf.DeltaAngle(yaw, target.eulerAngles.y).ToString("F1") + " degrees");
                Button("SettingsButton").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
                check("Settings pauses and blocks gameplay input", Time.timeScale == 0 && GameInput.Instance.GameplayInputBlocked, "Original LevelMenu.");
                Button("ResumeButton").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
                check("Resume restores gameplay input", Time.timeScale > 0 && !GameInput.Instance.GameplayInputBlocked, "Original LevelMenu.");
                Button("Return_To_Spawn").onClick.Invoke(); yield return new WaitForSecondsRealtime(.2f);
                check("Return-to-spawn keeps character grounded", controller.isGrounded && Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(session.SpawnPoint.position.x, session.SpawnPoint.position.z)) < .2f, player.transform.position.ToString("F3"));
                check("Monster stays asleep and stationary while exploring", monster.CurrentState == "Sleep" && monster.Mistakes == 0 && monster.transform.position == monsterPosition && monster.transform.rotation == monsterRotation, "No connection from player input to monster movement or mistakes.");
                ScreenCapture.CaptureScreenshot(KhezuAwakeningTestSetup.Folder + "/Reports/PlayerControls_PlayMode.png");
                complete = true;
            }
            finally
            {
                if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                check("Control verification completed", complete, "");
                report.passed = report.checks.All(c => c.passed);
                File.WriteAllText(KhezuAwakeningTestSetup.Folder + "/Reports/PlayerControlsValidation.json", JsonUtility.ToJson(report, true));
                Debug.Log("Khezu player controls checks: " + report.checks.Count(c => c.passed) + "/" + report.checks.Count);
            }
        }
    }
}
