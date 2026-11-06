#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Isolated Play Mode visibility checks; never submits answers or awards coins.</summary>
public static class PropositionalChamberLightingChecks
{
    const string Report = "Library/PropositionalChamberLightingChecks.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        File.AppendAllText(Report, "PASS " + message + "\n");
    }
    public static IEnumerator Run()
    {
        File.WriteAllText(Report, "Saved PRELIM lighting, isolated offline Play Mode. No submissions or rewards.\n");
        var profile = UnityEngine.Object.FindFirstObjectByType<PropositionalChamberLighting>();
        var rooms = PropositionalChamberLightingSetup.Rooms();
        var quiz = UnityEngine.Object.FindFirstObjectByType<QuizManager>();
        var clock = UnityEngine.Object.FindFirstObjectByType<LevelTimerManager>();
        var order = (List<int>)typeof(QuizManager).GetField("challengeOrder", Private).GetValue(quiz);
        var camera = Camera.main;
        var brain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        bool brainBefore = brain != null && brain.enabled;
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        float fov = camera.fieldOfView;
        var ambient = RenderSettings.ambientSkyColor; float sun = profile.sunlight.intensity;
        Check(profile.chamberBounds.Length == 5 && profile.torches.Length == 25, "five chamber bounds and all 25 existing torches");
        Check(profile.torches.All(l => l.shadows == LightShadows.None && l.range == 28 && l.intensity == 23), "shadowless existing torches; no new realtime lights");
        Check(!clock.IsTimerAdvancing && !quiz.IsQuizActive, "onboarding cleanup leaves the challenge waiting for the book");
        if (brain != null) brain.enabled = false;
        try
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i]; var book = room.roomChoiceCanvas.transform.parent.GetComponentInChildren<BookInteract>(true);
                PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject, room.topicSpawnPoint.position, room.topicSpawnPoint.rotation);
                quiz.ClearQuizUI();
                typeof(QuizManager).GetField("currentTopicIndex", Private).SetValue(quiz, order.IndexOf(i));
                typeof(QuizManager).GetField("currentChallengeStarted", Private).SetValue(quiz, false);
                quiz.OpenQuiz(book); clock.StopTimer();
                Check(quiz.IsQuizActive, room.topicName + ": actual book activation displays the question");
                Check(room.roomChoiceCanvas.GetComponentsInChildren<TMPro.TMP_Text>(true).All(t => !string.IsNullOrEmpty(t.text)), room.topicName + ": answer labels populated");
                var materials = room.hintBoard.GetComponent<Renderer>().sharedMaterials.Take(2);
                Check(materials.All(m => m.IsKeywordEnabled("_EMISSION") && m.shader.isSupported && !ShaderUtil.ShaderHasError(m.shader)), room.topicName + ": reading materials survive import and Play Mode");
                Vector3 doors = room.choiceDoors.Select(d => d.transform.position).Aggregate(Vector3.zero, (a,b) => a+b) / 4;
                Vector3 forward = doors - book.transform.position; forward.y = 0; forward.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                var board = room.hintBoard.GetComponent<Renderer>().bounds;
                var views = new[] {
                    new { name = "Gameplay", at = book.transform.position-forward*14+right*3+Vector3.up*8, look = book.transform.position+forward*7+Vector3.up*3, fov = 60f },
                    new { name = "TutorialBook", at = book.transform.position-forward*8+Vector3.up*5, look = book.transform.position+forward*4+Vector3.up*3.8f, fov = 60f },
                    new { name = "HintBoard", at = board.center-forward*10, look = board.center, fov = 50f }
                };
                foreach (var view in views)
                {
                    camera.transform.SetPositionAndRotation(view.at, Quaternion.LookRotation(view.look-view.at)); camera.fieldOfView = view.fov;
                    Check(profile.AppliesTo(camera), room.topicName + ": scoped lighting covers " + view.name);
                    int before = profile.ScopedRenderCount;
                    var tex = TreesChamberSetup.Capture("Library/Prelim_Play_"+i+"_"+view.name+".png", view.at, view.look, view.fov);
                    var pixels = tex.GetPixels32();
                    int black = pixels.Count(p => (p.r*.2126f+p.g*.7152f+p.b*.0722f)/255 < .025f);
                    int white = pixels.Count(p => (p.r*.2126f+p.g*.7152f+p.b*.0722f)/255 > .97f);
                    UnityEngine.Object.Destroy(tex);
                    Check(black < pixels.Length*.02 && white < pixels.Length*.03, room.topicName + ": " + view.name + " avoids crushed blacks / blown highlights");
                    Check(profile.ScopedRenderCount > before && RenderSettings.ambientSkyColor == ambient && profile.sunlight.intensity == sun, room.topicName + ": camera render restores original global lighting");
                    yield return null;
                }
                foreach (int size in new[] { 6, 12, 3 })
                {
                    BookMapMobileChecks.SetSize(size); yield return new WaitForSecondsRealtime(.3f); yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot("Library/Prelim_Mobile_"+i+"_"+Screen.width+"x"+Screen.height+".png");
                    Check(profile.AppliesTo(camera), room.topicName + ": gameplay lighting at " + Screen.width + "x" + Screen.height);
                }
            }
            var outside = new GameObject("Outside chamber QA camera").AddComponent<Camera>();
            outside.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.back));
            Check(!profile.AppliesTo(outside), "outside cameras retain the scene's original environment");
            UnityEngine.Object.Destroy(outside.gameObject);
            int selected = StageSelectionState.SelectedStage;
            StageSelectionState.SelectForEditorSpawn(2);
            Check(!profile.AppliesTo(camera), "other trial selections do not receive chamber lighting");
            StageSelectionState.SelectForEditorSpawn(selected);
        }
        finally
        {
            clock.StopTimer(); quiz.ClearQuizUI();
            camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            if (brain != null) brain.enabled = brainBefore;
            BookMapMobileChecks.SetSize(6);
        }
        Check(RenderSettings.ambientSkyColor == ambient && profile.sunlight.intensity == sun, "ambient and directional light restored after all camera checks");
        File.AppendAllText(Report, "ALL LIGHTING CHECKS PASSED. Physical-device GPU profiling not performed.\n");
    }
}
#endif
