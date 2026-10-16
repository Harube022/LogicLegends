#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercises the saved rigs with real GameInput and CharacterController movement, offline.</summary>
public static class PlayerLocomotionPlayChecks
{
    const string Flag = "Characters.LocomotionQA";
    static string Report => SessionState.GetString(Flag + ".Report", "Library/PlayerLocomotionPlayChecks.txt");

    [MenuItem("Logic Legends/Characters/Run All Costume Play Checks")]
    public static void Begin()
    {
        SessionState.SetString(Flag + ".Filter", "");
        SessionState.SetString(Flag + ".Report", "Library/PlayerLocomotionPlayChecks.txt");
        BeginFixture();
    }

    [MenuItem("Logic Legends/Characters/Recheck Corrected Legacy Imports")]
    public static void RecheckImports()
    {
        SessionState.SetString(Flag + ".Filter", "EKATUX|EATUX|WOMANMODEL1|WOMANMODEL1 1");
        SessionState.SetString(Flag + ".Report", "Library/PlayerLocomotionLegacyPlayChecks.txt");
        BeginFixture();
    }

    static void BeginFixture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (SessionState.GetBool(Flag, false)) RestoreScenes();
        if (!EditorSceneManager.SaveOpenScenes()) return;
        var paths = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).ToArray();
        SessionState.SetString(Flag + ".Paths", string.Join("|", paths));
        SessionState.SetString(Flag + ".Active", SceneManager.GetActiveScene().path);
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
        new GameObject("Fixture Input", typeof(GameInput));
        foreach (string path in PlayerLocomotionSetup.Prefabs)
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            root.SetActive(false);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is Player) && !(behaviour is PlayerAnimator)) UnityEngine.Object.DestroyImmediate(behaviour);
            // No cloud, account, rewards, or scene progression scripts run in this fixture.
            foreach (var driver in root.GetComponentsInChildren<PlayerAnimator>(true)) driver.gameObject.SetActive(false);
        }
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Locomotion Test Floor"; floor.transform.position = new Vector3(0, -.5f, 0);
        floor.transform.localScale = new Vector3(2000, 1, 2000);
        var camera = new GameObject("Locomotion Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.fieldOfView = 45;
        new GameObject("Fixture Sun", typeof(Light)).GetComponent<Light>().type = LightType.Directional;
        foreach (string path in paths) EditorSceneManager.CloseScene(SceneManager.GetSceneByPath(path), true);
        File.WriteAllText(Report, "Real movement, all saved player variants.\n");
        SessionState.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged -= Restore;
        EditorApplication.playModeStateChanged += Restore;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(Flag, false)) QueueRestore();
    }
    static void Restore(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Flag, false)) QueueRestore();
    }
    static void QueueRestore()
    {
        EditorApplication.update -= RestoreWhenReady;
        EditorApplication.update += RestoreWhenReady;
    }
    static void RestoreWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= RestoreWhenReady;
        if (SessionState.GetBool(Flag, false)) RestoreScenes();
    }
    static void RestoreScenes()
    {
        var paths = SessionState.GetString(Flag + ".Paths", "Assets/Scenes/PRELIM.unity").Split('|');
        for (int i = 0; i < paths.Length; i++)
            if (!string.IsNullOrEmpty(paths[i])) EditorSceneManager.OpenScene(paths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
        var active = SceneManager.GetSceneByPath(SessionState.GetString(Flag + ".Active", paths[0]));
        if (active.IsValid()) SceneManager.SetActiveScene(active);
        SessionState.SetBool(Flag, false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RunIfRequested()
    {
        if (SessionState.GetBool(Flag, false)) new GameObject("Costume Play Checks").AddComponent<LocomotionCheckRunner>().StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator check)
    {
        while (true)
        {
            bool more; object next;
            try { more = check.MoveNext(); next = more ? check.Current : null; }
            catch (Exception e)
            {
                File.AppendAllText(Report, "FAIL " + e + "\n"); Debug.LogException(e);
                EditorApplication.isPlaying = false; yield break;
            }
            if (!more) break;
            yield return next;
        }
        EditorApplication.isPlaying = false;
    }

    static IEnumerator Run()
    {
        Time.timeScale = 1;
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        var roots = SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.GetComponent<Player>() != null).ToArray();
        Directory.CreateDirectory("Temp/LocomotionQA");
        yield return null;
        int count = 0;
        foreach (var root in roots)
        {
            var player = root.GetComponent<Player>();
            var controller = root.GetComponent<CharacterController>();
            var skins = root.GetComponentsInChildren<PlayerAnimator>(true);
            string filter = SessionState.GetString(Flag + ".Filter", "");
            if (!string.IsNullOrEmpty(filter)) skins = skins.Where(s => filter.Split('|').Contains(s.name)).ToArray();
            root.SetActive(true);
            yield return null;
            player.SetRunningEnabled(true);
            foreach (var driver in skins)
            {
                driver.gameObject.SetActive(true);
                var animator = driver.GetComponent<Animator>();
                string label = driver.name;
                var initialPosition = driver.transform.localPosition;
                var initialRotation = driver.transform.localRotation;
                controller.enabled = false;
                root.transform.SetPositionAndRotation(new Vector3(0, controller.height * .5f - controller.center.y + .02f, 0), Quaternion.identity);
                controller.enabled = true;
                player.ResetMotionAfterTeleport();
                GameInput.Instance.SetMobileMovement(Vector2.zero);
                yield return new WaitForSeconds(.2f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsTag("Idle"), label + ": initial idle");
                var feet = PlayerLocomotionSetup.FindFeet(driver.gameObject);
                Check(!animator.applyRootMotion, label + ": script owns movement");
                GameInput.Instance.SetMobileMovement(Vector2.up);
                yield return new WaitForSeconds(.3f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsTag("Locomotion"), label + ": walk starts promptly; flags=" + player.IsWalking() + ", velocity=" + controller.velocity + ", input=" + GameInput.Instance.GetMovementVectorNormalized() + ", enabled=" + player.enabled + ", bool=" + animator.GetBool("IsWalking") + ", time=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime + ", transition=" + animator.IsInTransition(0));
                Check(!player.IsRunning(), label + ": walk before run delay");
                CheckRate(driver, animator, controller, false);
                float speed = FlatSpeed(controller);
                Check(Mathf.Abs(speed - player.moveSpeed) < .1f, label + ": original walk speed");
                var firstFoot = driver.transform.InverseTransformPoint(feet[0].position);
                float motionDeadline = Time.time + 1;
                while (Vector3.Distance(firstFoot, driver.transform.InverseTransformPoint(feet[0].position)) <= .005f && Time.time < motionDeadline) yield return null;
                Check(Vector3.Distance(firstFoot, driver.transform.InverseTransformPoint(feet[0].position)) > .005f, label + ": feet actually animate");
                Capture(label + "_Walk", player);
                yield return new WaitForSeconds(2.9f);
                Check(player.IsRunning(), label + ": original three-second run activation");
                CheckRate(driver, animator, controller, animator.parameters.Any(p => p.name == "IsRunning"));
                Check(animator.GetCurrentAnimatorClipInfo(0).All(c => c.clip.isLooping), label + ": locomotion keeps looping");
                var runFoot = driver.transform.InverseTransformPoint(feet[0].position);
                float runMotionDeadline = Time.time + 1;
                while (Vector3.Distance(runFoot, driver.transform.InverseTransformPoint(feet[0].position)) <= .005f && Time.time < runMotionDeadline) yield return null;
                Check(Vector3.Distance(runFoot, driver.transform.InverseTransformPoint(feet[0].position)) > .005f, label + ": feet keep animating after repeated cycles");
                Check(Mathf.Abs(FlatSpeed(controller) - player.moveSpeed * 1.5f) < .1f, label + ": original run speed");
                Capture(label + "_Run", player);
                float rootY = root.transform.position.y;
                GameInput.Instance.MobileJump();
                yield return new WaitForSeconds(.12f);
                Check(player.IsJumping() && root.transform.position.y > rootY + .2f, label + ": real jump succeeds");
                Check(animator.GetCurrentAnimatorStateInfo(0).IsTag("Jump"), label + ": jump interrupts locomotion");
                Check(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).speedMultiplier - 1) < .001f, label + ": jump timing independent of travel speed");
                Capture(label + "_Jump", player);
                float deadline = Time.time + 3;
                while (player.IsJumping() && Time.time < deadline) yield return null;
                yield return new WaitForSeconds(.15f);
                Check(controller.isGrounded && !player.IsJumping(), label + ": real landing");
                Check(animator.GetCurrentAnimatorStateInfo(0).IsTag("Locomotion"), label + ": immediate landing to locomotion");
                GameInput.Instance.SetMobileMovement(Vector2.zero);
                yield return new WaitForSeconds(.2f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsTag("Idle") && !player.IsRunning(), label + ": stopping returns to idle");
                Capture(label + "_Idle", player);
                // A wall must stop visual locomotion even while input remains held.
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = root.transform.position + Vector3.forward * (controller.radius + .55f) + Vector3.up * 3;
                wall.transform.localScale = new Vector3(30, 12, 1);
                Physics.SyncTransforms();
                GameInput.Instance.SetMobileMovement(Vector2.up);
                yield return new WaitForSeconds(.3f);
                Check(FlatSpeed(controller) < .05f && animator.GetCurrentAnimatorStateInfo(0).IsTag("Idle"), label + ": no running in place against a wall");
                UnityEngine.Object.Destroy(wall);
                GameInput.Instance.SetMobileMovement(Vector2.zero);
                // Guided movement uses its actual speed, not the normal player speed.
                var target = new GameObject("Guided walk target"); target.transform.position = root.transform.position + Vector3.forward * 20;
                player.BeginGuidedMovement(target.transform, 4);
                yield return new WaitForSeconds(.3f);
                CheckRate(driver, animator, controller, false);
                Check(Mathf.Abs(FlatSpeed(controller) - 4) < .1f, label + ": guided walk cadence");
                player.EndGuidedMovement(); UnityEngine.Object.Destroy(target);
                Time.timeScale = 0;
                yield return null; yield return null;
                Check(animator.GetFloat(PlayerAnimator.WALK_PLAYBACK) == 0, label + ": pause clears stale velocity");
                Time.timeScale = 1;
                Check(Vector3.Distance(driver.transform.localPosition, initialPosition) < .001f && Quaternion.Angle(driver.transform.localRotation, initialRotation) < .01f, label + ": no animated model root drift");
                driver.gameObject.SetActive(false);
                count++;
                File.AppendAllText(Report, "PASS " + label + ": walk/run/stop/jump/landing/wall/guided/pause; walk " + speed.ToString("F2") + " units/s\n");
            }
            root.SetActive(false);
            // Player's singleton is cleared on destroy, not disable.
            UnityEngine.Object.Destroy(root);
            yield return null;
        }
        File.AppendAllText(Report, "ALL " + count + " CHARACTER PLAY CHECKS PASSED\n");
    }

    static float FlatSpeed(CharacterController controller)
    {
        var v = controller.velocity; v.y = 0; return v.magnitude;
    }
    static void CheckRate(PlayerAnimator driver, Animator animator, CharacterController controller, bool run)
    {
        string field = run ? "sourceRunSpeed" : "sourceWalkSpeed";
        float source = (float)typeof(PlayerAnimator).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(driver);
        float expected = FlatSpeed(controller) / (source * Mathf.Abs(driver.transform.lossyScale.z));
        Check(Mathf.Abs(animator.GetFloat(run ? PlayerAnimator.RUN_PLAYBACK : PlayerAnimator.WALK_PLAYBACK) - expected) < .05f,
            driver.name + ": cadence matches actual velocity");
    }
    static void Capture(string name, Player player)
    {
        var camera = Camera.main;
        camera.transform.position = player.transform.position + new Vector3(0, 6, -17);
        camera.transform.LookAt(player.transform.position + Vector3.up * 2.5f);
        var old = RenderTexture.active;
        var target = RenderTexture.GetTemporary(480, 480, 24);
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(480, 480, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 480, 480), 0, 0); texture.Apply();
        File.WriteAllBytes("Temp/LocomotionQA/" + name + ".png", texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture); camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(target);
    }
    static void Check(bool result, string message)
    {
        if (!result) throw new InvalidOperationException(message);
    }
}
public sealed class LocomotionCheckRunner : MonoBehaviour
{
    void LateUpdate()
    {
        var player = Player.LocalInstance;
        var camera = Camera.main;
        if (player == null || camera == null) return;
        // Keep each newly enabled skin in view; normal Animator culling must not
        // freeze an offscreen rig in an otherwise valid movement test.
        camera.transform.position = player.transform.position + new Vector3(0, 6, -17);
        camera.transform.LookAt(player.transform.position + Vector3.up * 2.5f);
    }
}
#endif
