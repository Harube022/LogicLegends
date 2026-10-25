#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Four deliberate shots of one real question. All presentation changes exist only in the offline recording session.</summary>
public static class PropositionalHelpClipCapture
{
    const int Fps = 24;
    const string Output = "Library/StageHelpCaptures/";
    const string Evidence = "Library/PropositionalHelpSequence.txt";
    static readonly string[] Keys = { "PRELIM", "PRELIM_02_Question", "PRELIM_03_Answers", "PRELIM_04_Selection" };
    static readonly string[] Captions = {
        "Approach the Book Statue and tap the hand button to begin.",
        "Read the question and use the hint board to guide your answer.",
        "Compare the four door answers with the question.",
        "Walk to your chosen door and stand on its selection pad to submit your answer."
    };
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static Camera camera;
    static Canvas overlay;
    static RecordingVeil veil;
    static Image fade;
    static RecordingTouchRing tapRing;
    static RectTransform hand;
    static TMP_Text question;
    static QuizManager quiz;
    static LogicQuestion sameQuestion;
    static string[] sameAnswers;
    static TopicChallenge room;
    static Vector3 forward, right, doorsCenter;
    static Touchscreen touch;
    static int finger;
    static int segment;
    static float originalHandScale;

    public static IEnumerator Record()
    {
        // Invalidate only generated manifests; a failed recording cannot install a partial playlist.
        foreach (string key in Keys) if (File.Exists(Output + key + ".json")) File.Delete(Output + key + ".json");
        File.WriteAllText(Evidence, "Actual Game view. One question, no validation callbacks or admin clears. Offline backend and reward writes disabled.\n");
        quiz = UnityEngine.Object.FindFirstObjectByType<QuizManager>();
        room = quiz.CurrentChallengeForTutorial;
        var book = room.roomChoiceCanvas.transform.parent.GetComponentInChildren<BookInteract>(true);
        question = (TMP_Text)typeof(QuizManager).GetField("questionTextUI", Private).GetValue(quiz);
        var handButton = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "InteractButton");
        hand = (RectTransform)handButton.transform;
        camera = Camera.main;
        camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled = false;
        camera.fieldOfView = 45;
        doorsCenter = room.choiceDoors.Select(d => d.transform.position).Aggregate(Vector3.zero, (a, b) => a + b) / 4;
        forward = doorsCenter - book.transform.position; forward.y = 0; forward.Normalize();
        right = Vector3.Cross(Vector3.up, forward);
        BuildOverlay();
        touch = InputSystem.AddDevice<Touchscreen>(); finger = 600;
        // Initial setup is outside the recording. All visible movement uses CharacterController.Move.
        var spawn = book.transform.position - forward * 8 + right * 2.8f;
        spawn.y = room.topicSpawnPoint.position.y;
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject, spawn, Quaternion.LookRotation(forward));
        SetCamera(book.transform.position - forward * 14 + Vector3.up * 8, book.transform.position + Vector3.up * 2);
        yield return Frames(12);
        Start(0);
        yield return FadeIn();
        var approach = book.transform.position - forward * 3.2f + right * 2.8f; approach.y = spawn.y;
        yield return Walk(approach, 3.5f);
        yield return Frames(8);
        // Magnify the actual live button, retaining its real listener and raycast target.
        originalHandScale = hand.localScale.x;
        for (int i = 0; i < 30; i++)
        {
            float t = Mathf.SmoothStep(0, 1, (i + 1) / 30f);
            hand.localScale = Vector3.one * Mathf.Lerp(originalHandScale, originalHandScale * 1.55f, t);
            veil.SetHoles(ScreenRect(hand, 15));
            yield return new WaitForEndOfFrame();
        }
        yield return Frames(12);
        Snapshot("01_Hand");
        yield return Tap(handButton);
        if (!quiz.IsQuizActive) throw new InvalidOperationException("Real touch of the hand did not activate the nearby current book.");
        sameQuestion = (LogicQuestion)typeof(QuizManager).GetField("currentQuestion", Private).GetValue(quiz);
        sameAnswers = room.roomChoiceCanvas.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
        File.AppendAllText(Evidence, "Question: " + sameQuestion.questionText + "\nDoors left to right: " + string.Join(" | ", sameAnswers) + "\nHand: real Touchscreen Began/Ended, normal proximity path; challenge active.\n");
        yield return Frames(26);
        yield return Finish();
        hand.localScale = Vector3.one * originalHandScale;
        veil.SetHoles();
        // Trim the ordinary wait before the first progressive hint row; never synthesize hint answers.
        while (room.hintBoard.RevealedRows < 1) yield return null;
        yield return Frames(3 * Fps); // Let the normal Start! animation finish outside the next shot.

        PrepareQuestion();
        // Controls are irrelevant to the reading/answer comparison shots and obscure the board.
        hand.parent.gameObject.AddComponent<CanvasGroup>().alpha=0;
        var avatarRenderers=Player.LocalInstance.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        foreach(var renderer in avatarRenderers) renderer.enabled=false;
        SetCamera(doorsCenter - forward * 19 + Vector3.up * 4, doorsCenter - Vector3.up);
        veil.SetHoles(ScreenRect(question.rectTransform, 12));
        Start(1);
        yield return FadeIn();
        yield return Frames(70);
        Snapshot("02_Question");
        var bounds = room.hintBoard.GetComponent<Renderer>().bounds;
        Vector3 to = bounds.center - forward * 13;
        var from = camera.transform.position; var rotation = camera.transform.rotation;
        var end = Quaternion.LookRotation(bounds.center - to);
        // A single slow transition to a straight-on board close-up; the camera then remains still.
        for (int i = 0; i < 42; i++)
        {
            float t = Mathf.SmoothStep(0, 1, (i + 1) / 42f);
            camera.transform.SetPositionAndRotation(Vector3.Lerp(from, to, t), Quaternion.Slerp(rotation, end, t));
            question.alpha = 1 - t;
            veil.SetHoles(WorldRect(bounds, 15));
            yield return new WaitForEndOfFrame();
        }
        yield return Frames(68);
        Snapshot("02_Hint");
        AssertSame();
        yield return Finish();

        question.alpha = 0;
        // Enlarge the existing four label components only for the recorded explanation.
        // Their strings remain those assigned by the real randomized question.
        foreach (var label in room.roomChoiceCanvas.GetComponentsInChildren<TMP_Text>())
        {
            label.fontSize = 70; label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(560, 180);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.outlineColor = Color.black; label.outlineWidth = .22f;
        }
        camera.fieldOfView = 50;
        SetCamera(doorsCenter - forward * 17 + Vector3.up * 5.5f, doorsCenter - Vector3.up * .7f);
        Canvas.ForceUpdateCanvases(); yield return Frames(2);
        var labels = room.roomChoiceCanvas.GetComponentsInChildren<TMP_Text>();
        veil.SetHoles(labels.Select(t => ScreenRect(t.rectTransform, 12)).ToArray());
        Start(2);
        yield return FadeIn();
        yield return Frames(96);
        Snapshot("03_FourAnswers");
        AssertSame();
        yield return Finish();

        foreach(var renderer in avatarRenderers) renderer.enabled=true;
        var pad = UnityEngine.Object.FindObjectsByType<SelectionPad>(FindObjectsSortMode.None)
            .Where(p => Vector3.Distance(p.transform.position, book.transform.position) < 40 && quiz.IsChoiceCorrect(p.PadIndex)).First();
        // Fixed three-quarter view includes the full walk, pad charge and opening door.
        veil.SetHoles(); camera.fieldOfView = 54;
        Vector3 view = pad.transform.position - forward * 17 + right * 6 + Vector3.up * 8;
        SetCamera(view, (approach + pad.transform.position) * .5f + Vector3.up * 2.5f);
        Start(3);
        yield return FadeIn();
        // Route around the solid book stand without ever teleporting during the recorded action.
        Vector3 beside = book.transform.position + right * 4.7f - forward * .8f; beside.y = spawn.y;
        yield return Walk(beside, 4.5f);
        Vector3 onPad = pad.transform.position; onPad.y = spawn.y;
        yield return Walk(onPad, 4.5f);
        Snapshot("04_OnPad");
        int before = (int)typeof(QuizManager).GetField("currentTopicIndex", Private).GetValue(quiz);
        int chargedFrames = 0;
        float until = Time.realtimeSinceStartup + 8;
        while (quiz.IsQuizActive && Time.realtimeSinceStartup < until)
        {
            AssertSame(); chargedFrames++;
            yield return new WaitForEndOfFrame();
        }
        if(quiz.IsQuizActive)throw new InvalidOperationException("The actual selection-pad dwell did not confirm the answer.");
        // Hold the real pad-confirmation frame for reading, trimming the unrelated room teleport.
        // This affects only this Editor capture; normal Player/SelectionPad code is untouched.
        Time.timeScale=0;
        yield return Frames(24);
        Snapshot("04_Selected");
        yield return Finish();
        Time.timeScale=1;
        until=Time.realtimeSinceStartup+10;
        while((int)typeof(QuizManager).GetField("currentTopicIndex", Private).GetValue(quiz)==before&&Time.realtimeSinceStartup<until)yield return null;
        int after = (int)typeof(QuizManager).GetField("currentTopicIndex", Private).GetValue(quiz);
        if (after != before + 1) throw new InvalidOperationException("The real pad + guided doorway route did not advance exactly one challenge.");
        File.AppendAllText(Evidence, "Selection: physically walked onto pad " + (pad.PadIndex + 1) + "; normal dwell confirmed answer after " + chargedFrames + " observed frames. Post-capture normal guided route advanced exactly one challenge.\n");
        InputSystem.RemoveDevice(touch);
        UnityEngine.Object.Destroy(overlay.gameObject);
        var manifest = new StageHelpVideoCatalog.Recording {
            scene = "PRELIM", clips = Keys,
            source = "One continuous real Unity test attempt. Physical walking, actual hand touch, normal selection-pad dwell. Recording-only stable cameras, magnification and spotlights; no account writes. Every clip ends in a 48-frame / 2-second fade to black at 24 fps.",
            cues = Captions.Select(c => new StageHelpVideoCatalog.Cue { seconds = 0, caption = c }).ToArray()
        };
        File.WriteAllText(Output + "PRELIM.json", JsonUtility.ToJson(manifest, true));
        File.AppendAllText(Evidence, "PASS: all four clips use the same question and door answers. Each ends with exactly 48 fade frames. No normal gameplay source or saved scene modified.\n");
    }

    static void Start(int index)
    {
        segment = index; fade.color = Color.black;
        StageHelpGameplayRecorder.StartRecording(Keys[index]);
    }
    static IEnumerator Finish()
    {
        AssertSameUnlessCompleted();
        for (int i = 0; i < 48; i++)
        {
            fade.color = new Color(0, 0, 0, (i + 1) / 48f);
            yield return new WaitForEndOfFrame();
            if(i==23||i==47)Snapshot("Fade_"+segment+"_"+(i+1));
        }
        // Recorder flushes at end-of-frame too. Hold black so its final encoded frame is fully black.
        yield return Frames(3);
        File.AppendAllText(Evidence, Keys[segment] + " fade: 48 frames = 2.000 seconds + 3 black hold frames. Total frames: " + StageHelpGameplayRecorder.RecordedFrames + "\n");
        StageHelpGameplayRecorder.StopSegment(Keys[segment], Captions[segment]);
    }
    static IEnumerator FadeIn()
    {
        for (int i = 0; i < 8; i++) { fade.color = new Color(0, 0, 0, 1 - (i + 1) / 8f); yield return new WaitForEndOfFrame(); }
    }
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return new WaitForEndOfFrame(); }
    static IEnumerator Walk(Vector3 to, float speed)
    {
        var target = new GameObject("Recording walk destination").transform; target.position = to;
        Player.LocalInstance.BeginGuidedMovement(target, speed, .18f);
        float end = Time.realtimeSinceStartup + 9;
        while (Vector2.Distance(XZ(Player.LocalInstance.transform.position), XZ(to)) > .24f && Time.realtimeSinceStartup < end) yield return new WaitForEndOfFrame();
        if (Vector2.Distance(XZ(Player.LocalInstance.transform.position), XZ(to)) > .35f) throw new InvalidOperationException("Recorded physical walk obstructed: " + to + "; player " + Player.LocalInstance.transform.position);
        Player.LocalInstance.EndGuidedMovement(); UnityEngine.Object.Destroy(target.gameObject);
    }
    static Vector2 XZ(Vector3 point) => new Vector2(point.x, point.z);
    static void SetCamera(Vector3 position, Vector3 look) { camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position)); }
    static void AssertSameUnlessCompleted() { if (sameQuestion != null && quiz.IsQuizActive) AssertSame(); }
    static void AssertSame()
    {
        if (!ReferenceEquals(sameQuestion, typeof(QuizManager).GetField("currentQuestion", Private).GetValue(quiz)) ||
            !sameAnswers.SequenceEqual(room.roomChoiceCanvas.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text)))
            throw new InvalidOperationException("Question or answer labels changed between clips.");
    }
    static void Snapshot(string key) => ScreenCapture.CaptureScreenshot("Library/PropositionalHelp_" + key + ".png");
    static IEnumerator Tap(Button button)
    {
        Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
        var point = ScreenRect((RectTransform)button.transform, 0).center;
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != button) throw new InvalidOperationException("Hand touch raycast obstructed.");
        var ring = (RectTransform)tapRing.transform;
        ring.anchoredPosition = point * (1280f / Screen.width) - new Vector2(640, 360); ring.sizeDelta = new Vector2(58, 58);
        tapRing.color = new Color(1, .8f, .15f, .9f);
        int id = ++finger;
        InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
        yield return Frames(4);
        InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
        for (int i = 0; i < 10; i++) { ring.sizeDelta = Vector2.one * (58 + i * 5); tapRing.color = new Color(1, .8f, .15f, .8f * (1 - i / 9f)); yield return new WaitForEndOfFrame(); }
    }
    static void PrepareQuestion()
    {
        // Magnify the actual question component, not a different tutorial question.
        question.rectTransform.SetParent(overlay.transform, false);
        question.rectTransform.anchorMin = question.rectTransform.anchorMax = new Vector2(.5f, .5f);
        question.rectTransform.pivot = new Vector2(.5f, .5f);
        question.rectTransform.anchoredPosition = new Vector2(0, 155);
        question.rectTransform.sizeDelta = new Vector2(1120, 220);
        question.fontSize = 44; question.enableAutoSizing = false;
        question.textWrappingMode = TextWrappingModes.Normal; question.alignment = TextAlignmentOptions.Center;
        question.outlineColor = Color.black; question.outlineWidth = .18f;
        question.rectTransform.SetSiblingIndex(0); question.alpha = 1;
    }
    static Rect ScreenRect(RectTransform target, float padding)
    {
        var canvas = target.GetComponentInParent<Canvas>(); var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (canvas.renderMode == RenderMode.WorldSpace) cam = camera;
        var corners = new Vector3[4]; target.GetWorldCorners(corners);
        var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(cam, p)).ToArray();
        return Rect.MinMaxRect(points.Min(p => p.x) - padding, points.Min(p => p.y) - padding, points.Max(p => p.x) + padding, points.Max(p => p.y) + padding);
    }
    static Rect WorldRect(Bounds bounds, float padding)
    {
        var points = new List<Vector3>();
        for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            points.Add(camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
        return Rect.MinMaxRect(points.Min(p => p.x) - padding, points.Min(p => p.y) - padding, points.Max(p => p.x) + padding, points.Max(p => p.y) + padding);
    }
    static void BuildOverlay()
    {
        overlay = new GameObject("Recording-only presentation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 20000;
        var scaler = overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
        var vr = NewRect("Recording spotlight", overlay.transform); veil = vr.gameObject.AddComponent<RecordingVeil>(); veil.raycastTarget = false; veil.color = new Color(.01f, .015f, .025f, .67f);
        var ring = NewRect("Actual touch visualization", overlay.transform); ring.anchorMin = ring.anchorMax = new Vector2(.5f, .5f); tapRing = ring.gameObject.AddComponent<RecordingTouchRing>(); tapRing.raycastTarget = false; tapRing.color = Color.clear;
        
        var fr = NewRect("Two second transition", overlay.transform); fade = fr.gameObject.AddComponent<Image>(); fade.raycastTarget = false; fade.color = Color.clear;
    }
    static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
}

/// <summary>Editor-only recording matte. Its holes never receive input or change control positions.</summary>
public sealed class RecordingTouchRing : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); const int sides=48; float outer=rectTransform.rect.width*.5f, inner=outer*.75f;
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;int n=vh.currentVertCount;
            vh.AddVert(new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner,0),color,Vector2.zero);
            vh.AddVert(new Vector3(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer,0),color,Vector2.zero);
            vh.AddVert(new Vector3(Mathf.Cos(b)*outer,Mathf.Sin(b)*outer,0),color,Vector2.zero);
            vh.AddVert(new Vector3(Mathf.Cos(b)*inner,Mathf.Sin(b)*inner,0),color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
public sealed class RecordingVeil : MaskableGraphic
{
    readonly List<Rect> holes = new List<Rect>();
    public void SetHoles(params Rect[] screens)
    {
        holes.Clear();
        foreach (var s in screens)
        {
            Vector2 min, max;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, s.min, null, out min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, s.max, null, out max);
            var r = Rect.MinMaxRect(Mathf.Max(rectTransform.rect.xMin, min.x), Mathf.Max(rectTransform.rect.yMin, min.y), Mathf.Min(rectTransform.rect.xMax, max.x), Mathf.Min(rectTransform.rect.yMax, max.y));
            if (r.width > 0 && r.height > 0) holes.Add(r);
        }
        enabled = holes.Count > 0; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var area = rectTransform.rect;
        var xs = new List<float>{area.xMin, area.xMax}; var ys = new List<float>{area.yMin, area.yMax};
        foreach (var h in holes) { xs.Add(h.xMin); xs.Add(h.xMax); ys.Add(h.yMin); ys.Add(h.yMax); }
        xs.Sort(); ys.Sort();
        for (int x = 0; x < xs.Count - 1; x++) for (int y = 0; y < ys.Count - 1; y++)
        {
            var r = Rect.MinMaxRect(xs[x], ys[y], xs[x + 1], ys[y + 1]);
            if (r.width < .1f || r.height < .1f || holes.Any(h => h.Contains(r.center))) continue;
            int n = vh.currentVertCount; vh.AddVert(new Vector3(r.xMin,r.yMin),color,Vector2.zero); vh.AddVert(new Vector3(r.xMin,r.yMax),color,Vector2.zero); vh.AddVert(new Vector3(r.xMax,r.yMax),color,Vector2.zero); vh.AddVert(new Vector3(r.xMax,r.yMin),color,Vector2.zero); vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
#endif
