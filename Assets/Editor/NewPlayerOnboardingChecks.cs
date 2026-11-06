using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

/// <summary>Local UI/audio checks only; does not sign in, create users or write profiles.</summary>
public static class NewPlayerOnboardingChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
    static void Call(AuthManager auth, string name, params object[] args) => typeof(AuthManager).GetMethod(name, Hidden).Invoke(auth, args);
    static void Check(bool pass, string message, List<string> results)
    {
        if (!pass) throw new InvalidOperationException(message);
        results.Add("PASS " + message);
    }

    public static void Run()
    {
        if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Main Menu")
            throw new InvalidOperationException("Run in Main Menu Play Mode.");
        var auth = UnityEngine.Object.FindFirstObjectByType<AuthManager>();
        auth.StartCoroutine(Verify(auth));
    }

    static IEnumerator Verify(AuthManager auth)
    {
        const string report = "output/loading-screen-preview/unity/onboarding-checks.txt";
        var results = new List<string>();
        var music = Get<AudioSource>(auth, "mainMenuMusic");
        var video = Get<VideoPlayer>(auth, "introVideoPlayer");
        var username = Get<UsernameSetupUI>(auth, "usernameSetup");
        var character = Get<GameObject>(auth, "characterSelectMenu");
        var menu = Get<GameObject>(auth, "modeSelection");
        var cinematic = Get<GameObject>(auth, "cinematicPanel");
        var screen = UnityEngine.Object.FindFirstObjectByType<SharedLoadingScreen>();
        bool signingIn = Get<bool>(auth, "signingIn");
        bool wasPlaying = music.isPlaying;
        float scale = Time.timeScale, volume = music.volume;
        bool mute = music.mute;
        var mixer = music.outputAudioMixerGroup;
        var audioMode = video.audioOutputMode;
        // Ignore pending profile replies during this temporary local presentation test.
        Set(auth, "sessionVersion", Get<int>(auth, "sessionVersion") + 1);
        Set(auth, "signingIn", true);
        try
        {
            character.SetActive(true);
            Call(auth, "ShowUsernamePrompt", new object[] { null });
            Check(!character.activeSelf, "Character selection hides when username entry opens", results);
            Check(Get<GameObject>(username, "overlay").activeInHierarchy, "Username prompt stays visible after character selection hides", results);
            if (!music.isPlaying) music.Play();
            foreach (string completion in new[] { "complete", "skip", "error" })
            {
                Call(auth, "PlayIntroCutscene");
                Check(!music.isPlaying && cinematic.activeSelf, completion + ": menu music pauses during the intro", results);
                Check(!Get<GameObject>(username, "overlay").activeSelf && !character.activeSelf && !menu.activeSelf,
                    completion + ": setup screens and main menu remain hidden during intro", results);
                Check(video.audioOutputMode == audioMode && music.volume == volume && music.mute == mute && music.outputAudioMixerGroup == mixer,
                    completion + ": video audio and saved menu audio settings are unchanged", results);
                Time.timeScale = 0f;
                if (completion == "skip") { auth.OnClickSkipCutscene(); auth.OnClickSkipCutscene(); }
                else if (completion == "error") Call(auth, "OnCutsceneError", video, "Expected local onboarding validation error");
                else Call(auth, "OnCutsceneFinished", video);
                // Resolve readiness locally, without waiting on or modifying a live account.
                Set(auth, "sessionVersion", Get<int>(auth, "sessionVersion") + 1);
                Call(auth, "CompleteAccountLoading", (Action)(() => Call(auth, "ShowModeSelection")));
                bool sawLoading = false;
                float deadline = Time.realtimeSinceStartup + 5f;
                do
                {
                    sawLoading |= screen.IsVisible;
                    yield return null;
                } while (Get<bool>(auth, "finishingIntro") && Time.realtimeSinceStartup < deadline);
                Check(sawLoading, completion + ": loading screen appears after cinematic", results);
                Check(!Get<bool>(auth, "finishingIntro") && !screen.IsVisible && !screen.GetComponent<CanvasGroup>().blocksRaycasts,
                    completion + ": loading fades away and releases input even while time is paused", results);
                Check(menu.activeSelf && !cinematic.activeSelf && music.isPlaying,
                    completion + ": main menu is revealed and music resumes", results);
                Time.timeScale = scale;
            }
            music.Stop();
            Call(auth, "PlayIntroCutscene");
            auth.OnClickSkipCutscene();
            Set(auth, "sessionVersion", Get<int>(auth, "sessionVersion") + 1);
            Call(auth, "CompleteAccountLoading", (Action)(() => Call(auth, "ShowModeSelection")));
            while (Get<bool>(auth, "finishingIntro")) yield return null;
            Check(!music.isPlaying, "An already stopped menu track is not restarted by the intro", results);
            results.Add("COMPLETE: local UI/audio checks; no account or profile writes.");
        }
        finally
        {
            Time.timeScale = scale;
            Set(auth, "signingIn", signingIn);
            if (wasPlaying && !music.isPlaying) music.Play();
            else if (!wasPlaying) music.Stop();
            File.WriteAllLines(report, results);
        }
    }

    public static void RunConnectionChecks()
    {
        var auth = UnityEngine.Object.FindFirstObjectByType<AuthManager>();
        auth.StartCoroutine(VerifyConnection(auth));
    }

    static IEnumerator VerifyConnection(AuthManager auth)
    {
        var results = new List<string>();
        var screen = UnityEngine.Object.FindFirstObjectByType<SharedLoadingScreen>();
        var transition = UnityEngine.Object.FindFirstObjectByType<PrelimTeleportTransition>();
        var visibility = screen.GetComponent<CanvasGroup>();
        var status = Get<TMPro.TMP_Text>(screen, "statusLabel");
        var feedback = Get<TMPro.TMP_Text>(auth, "authenticationMessage");
        var error = Get<GameObject>(screen, "errorPanel");
        var retryButton = Get<UnityEngine.UI.Button>(screen, "retryButton");
        var cancelButton = Get<UnityEngine.UI.Button>(screen, "cancelButton");
        bool wasSigningIn = Get<bool>(auth, "signingIn");
        float scale = Time.timeScale;
        int retries = 0, cancellations = 0, prepared = 0;
        Action retry = null;
        retry = () => { retries++; Call(auth, "BeginAccountLoading", retry); Set(auth, "signingIn", true); };
        try
        {
            Set(auth, "signingIn", true);
            Call(auth, "BeginAccountLoading", retry);
            int failedVersion = Get<int>(auth, "sessionVersion");
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(1.5f);
            Check(screen.IsVisible && !error.activeSelf && Get<bool>(auth, "accountLoading"),
                "Pending readiness keeps loading visible without a presentation timer", results);
            Check(status.text == "Loading..." && string.IsNullOrEmpty(feedback.text),
                "Only Loading... appears; authentication feedback is hidden", results);
            Call(auth, "ShowConnectionError");
            Check(screen.IsVisible && error.activeSelf && visibility.interactable && visibility.blocksRaycasts,
                "Failed connection shows a blocking Retry / Cancel confirmation", results);
            Check(Get<int>(auth, "sessionVersion") > failedVersion,
                "Failure invalidates late replies from the previous request", results);
            auth.CheckLoginState();
            Check(error.activeSelf && Get<bool>(auth, "connectionErrorVisible"),
                "A late initialization notification cannot dismiss the error before Retry", results);
            yield return null;
            ScreenCapture.CaptureScreenshot("output/loading-screen-preview/unity/connection-error.png");
            yield return null;
            retryButton.onClick.Invoke(); retryButton.onClick.Invoke();
            Check(retries == 1 && screen.IsVisible && !error.activeSelf && Get<bool>(auth, "accountLoading"),
                "Retry starts loading again exactly once and clears the error panel", results);
            screen.ShowConnectionError(retry, () => cancellations++);
            cancelButton.onClick.Invoke(); cancelButton.onClick.Invoke();
            Check(cancellations == 1, "Cancel invokes its terminal action exactly once", results);
            Call(auth, "BeginAccountLoading", retry);
            Call(auth, "CompleteAccountLoading", (Action)(() =>
            {
                prepared++;
                Check(transition.FadeAlpha >= .99f, "Menu preparation occurs under TeleportManager's full black cover", results);
                Call(auth, "ShowModeSelection");
            }));
            bool sawBlack = false;
            float deadline = Time.realtimeSinceStartup + 6f;
            do
            {
                sawBlack |= transition.FadeAlpha >= .99f;
                yield return null;
            } while (Get<bool>(auth, "accountReveal") && Time.realtimeSinceStartup < deadline);
            Check(sawBlack && prepared == 1 && !screen.IsVisible && transition.FadeAlpha <= .01f,
                "Successful readiness reveals the menu through TeleportManager's black fade", results);
            Check(!visibility.blocksRaycasts && !transition.IsTransitioning,
                "Loading and teleport input blockers are released after the reveal", results);
            Check(retryButton.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier == 4.5f &&
                cancelButton.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier == 4.5f,
                "Confirmation actions retain the shared button artwork settings", results);
            results.Add("COMPLETE: controlled pending/failure/retry checks; Cancel was intercepted to keep the app open.");
        }
        finally
        {
            Time.timeScale = scale;
            Set(auth, "accountLoading", false);
            Set(auth, "connectionErrorVisible", false);
            Set(auth, "accountRetry", null);
            Set(auth, "signingIn", wasSigningIn);
            File.WriteAllLines("output/loading-screen-preview/unity/connection-checks.txt", results);
        }
    }
}
