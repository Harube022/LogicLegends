using System;
using System.Collections;
using System.Linq;
using LogicLegends.Induction;
using LogicLegends.Graph;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Exercises the real offline scene without awarding account rewards.</summary>
public static class InductionPlayChecks
{
    public static IEnumerator Run()
    {
        GraphCompletion.SuppressPersistenceForValidation = true;
        var m = InductionStageManager.Instance;
        while (!m.Ready || !m.help.IsOpen) yield return null;
        Check(Time.timeScale==0&&GameInput.Instance.GameplayInputBlocked&&m.State==InductionState.WaitingForBook&&!m.timer.Running,"automatic Help pauses before book");
        float helpTime=m.timer.Remaining;
        m.InteractBook();m.Submit();Check(m.State==InductionState.WaitingForBook&&m.Correct==0,"Help blocks real interactions");
        for(int p=1;p<m.help.PageCount;p++){m.help.NextButton.onClick.Invoke();Check(m.help.PageIndex==p,"Help next page "+p);}
        Check(!m.help.NextButton.interactable,"Help final page boundary");m.help.PreviousButton.onClick.Invoke();Check(m.help.PageIndex==m.help.PageCount-2,"Help Previous");
        yield return new WaitForSecondsRealtime(.15f);Check(m.timer.Remaining==helpTime,"Help consumes no countdown");
        m.help.BeginButton.onClick.Invoke();Check(!m.help.IsOpen&&Time.timeScale==1&&!GameInput.Instance.GameplayInputBlocked&&!m.timer.Running,"Let’s Begin restores without starting");
        yield return InductionOnboardingPlayChecks.Run();
        m.altarIndicators.Refresh();Check(m.altarIndicators.Arrows.All(a=>!a.gameObject.activeSelf),"waiting indicators hidden");
        Check(m.slots.All(s => s.GetComponentsInChildren<Renderer>(true).Length == 0 && s.rim == null && s.label == null), "anchors have no visible slots");
        Check(m.State == InductionState.WaitingForBook && !m.timer.Running && Mathf.Approximately(m.timer.Remaining, m.startingSeconds), "initial waiting timer");
        m.ui.SubmitButton.onClick.Invoke();Check(!m.CanSubmit&&!m.ui.SubmitButton.gameObject.activeSelf&&m.Mistakes==0,"Submit unavailable before book");
        Check(!m.PickUp(m.Tablets[0]), "no pickup before activation");
        Check(m.Tablets.All(t=>t.AtStand&&!t.gameObject.activeSelf),"no scattered scroll pickups");
        Face(m.stations[1].position);yield return null;
        Check(m.ui.ViewBoardButton.gameObject.activeSelf&&!m.boardView.Viewing,"altar proximity only shows button");
        float originalFov=Camera.main.fieldOfView;
        m.ui.ViewBoardButton.onClick.Invoke();
        float paused = m.timer.Remaining;
        yield return new WaitForSecondsRealtime(1);
        Check(m.State == InductionState.WaitingForBook && m.timer.Remaining == paused && Time.timeScale == 0, "read-only board before book");
        Check(m.boardView.Viewing&&!m.boardView.Transitioning&&m.ui.BoardBackButton.gameObject.activeSelf,"board camera and Back");
        var frame=m.informationBoard.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Info_GoldFrame");
        var center=Camera.main.WorldToViewportPoint(frame.bounds.center);
        Check(Mathf.Abs(center.x-.5f)<.01f&&Mathf.Abs(center.y-.5f)<.01f&&center.z>0,"camera frames current board");
        m.ui.BoardBackButton.onClick.Invoke();yield return new WaitForSecondsRealtime(1);
        Check(!m.Reading&&!m.boardView.Viewing&&Camera.main.fieldOfView==originalFov&&Time.timeScale==1&&!GameInput.Instance.GameplayInputBlocked,"board restores controls and lens");
        m.ui.ViewBoardButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.1f);m.CloseReading();
        Check(!m.boardView.Viewing&&!m.boardView.Transitioning&&Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled,"interrupted blend restores camera");
        Face(m.stations[0].position+Vector3.left*50);yield return null;m.ui.ViewBoardButton.onClick.Invoke();
        Check(!m.Reading&&!m.ui.ViewBoardButton.gameObject.activeSelf,"board unavailable away from altars");
        var hand = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "InteractButton");
        Face(m.scrollStand.position);yield return null;hand.onClick.Invoke();
        Check(m.BrowsingScrolls&&m.ui.Collection.activeSelf,"hand opens stand before start");
        m.ui.SelectScroll(m.Tablets[0]);Check(!m.ui.GetScrollButton.interactable&&!m.CollectScroll(m.Tablets[0]),"before book cannot collect");
        Check(!m.ui.Collection.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Read Scroll"||b.name=="Reference Notes"),"removed reading and reference options");
        m.CloseReading();Check(m.State==InductionState.WaitingForBook&&!m.timer.Running&&m.timer.Remaining==paused,"catalogue never starts challenge");
        Face(m.book.position); yield return null; hand.onClick.Invoke();
        Check(m.State == InductionState.Active && m.timer.Running, "hand starts challenge");
        CheckIndicators(m,0);
        int mistakes = m.Mistakes;
        m.InteractBook(); Check(m.Mistakes == mistakes, "duplicate same-frame activation suppressed");
        yield return null;
        m.InteractBook();hand.onClick.Invoke();
        Check(m.State==InductionState.Active&&m.Mistakes==mistakes&&m.Correct==0&&m.timer.Running&&!m.bookPrompt.gameObject.activeSelf,"active book neither submits nor restarts");
        m.ui.SubmitButton.onClick.Invoke();Check(!m.CanSubmit&&m.Mistakes==mistakes,"empty arrangement cannot submit");
        paused=m.timer.Remaining;m.help.HelpButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.15f);
        Check(m.help.IsOpen&&m.timer.Running&&m.timer.Remaining==paused&&!m.CanAct,"reopened Help preserves active timer and state");
        m.help.BeginButton.onClick.Invoke();Check(m.State==InductionState.Active&&m.timer.Running&&!m.Reading,"Help restores active play");
        var menu = UnityEngine.Object.FindFirstObjectByType<LevelMenu>();
        paused = m.timer.Remaining; menu.OpenPause(); yield return new WaitForSecondsRealtime(.15f);
        Check(m.timer.Remaining == paused && Time.timeScale == 0 && GameInput.Instance.GameplayInputBlocked, "Settings pause");
        menu.ClosePause(); Check(Time.timeScale == 1 && !GameInput.Instance.GameplayInputBlocked, "Settings resume");
        Face(m.stations[1].position);yield return null;paused=m.timer.Remaining;m.ui.ViewBoardButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);Check(m.timer.Running&&m.timer.Remaining==paused&&m.State==InductionState.Active,"active board preserves countdown state");
        m.ui.BoardBackButton.onClick.Invoke();yield return new WaitForSecondsRealtime(1);
        var wrong = m.Tablets.First(t => t.Data.id == "base-wrong-zero");
        yield return Collect(m,hand,wrong);
        Check(m.Held == wrong, "hand pickup");
        Face(m.scrollStand.position);yield return null;hand.onClick.Invoke();m.ui.SelectScroll(m.Tablets.First(t=>t.AtStand));
        Check(!m.ui.GetScrollButton.interactable&&!m.CollectScroll(m.Tablets.First(t=>t.AtStand)),"cannot collect a second scroll while carrying");m.CloseReading();
        m.help.HelpButton.onClick.Invoke(); Check(m.Reading && Time.timeScale == 0, "Help while carrying");
        paused = m.timer.Remaining; yield return new WaitForSecondsRealtime(.15f);
        Check(m.timer.Remaining == paused&&m.Held==wrong, "Help preserves held scroll and time"); m.help.Close();
        var baseSlot = m.slots.Single(s => s.key == "Base:1");
        Face(baseSlot.transform.position); yield return null; m.ui.InteractButton.onClick.Invoke();
        Check(baseSlot.Tablet == wrong && m.Held == null && !baseSlot.HasError, "placement not pregraded");
        Check(m.ui.SubmitButton.gameObject.activeSelf&&m.CanSubmit&&!m.IsTargetable(m.book.position),"Submit offered at altar away from book");
        float before = m.timer.Remaining; m.ui.SubmitButton.onClick.Invoke();
        Check(m.Mistakes == 1 && before - m.timer.Remaining >= 9.99f && before - m.timer.Remaining < 10.1f, "one penalty per submission");
        Check(baseSlot.Tablet == wrong && baseSlot.HasError && wrong.HasError && m.ChallengeIndex == 0, "wrong arrangement preserved and scroll highlighted");
        m.ui.SubmitButton.onClick.Invoke(); Check(m.Mistakes == 1, "duplicate submission suppressed");
        yield return null;before=m.timer.Remaining;m.ui.SubmitButton.onClick.Invoke();
        Check(m.Mistakes==2&&before-m.timer.Remaining>=9.99f&&before-m.timer.Remaining<10.1f&&baseSlot.Tablet==wrong,"retry costs one penalty and preserves placement");
        var correctBase = m.Tablets.First(t => t.Data.id == "base");
        yield return Collect(m,hand,correctBase);
        Face(baseSlot.transform.position); yield return null; Check(m.Place(baseSlot), "replace occupied slot");
        Check(baseSlot.Tablet == correctBase && m.Held == wrong && !baseSlot.HasError && !wrong.HasError, "swap clears only moved scroll error");
        m.ReturnHeld(); Check(m.Held == null && wrong.AtStand&&!wrong.gameObject.activeSelf, "return held tablet to catalogue");
        Face(m.scrollStand.position);yield return null;m.ui.InteractButton.onClick.Invoke();
        m.ui.SelectScroll(correctBase);Check(!m.ui.GetScrollButton.interactable&&!m.CollectScroll(correctBase),"placed scroll cannot be duplicated");m.CloseReading();
        Face(baseSlot.transform.position); yield return null; m.ui.SubmitButton.onClick.Invoke();
        Check(m.Correct == 1 && m.ChallengeIndex == 1 && m.State == InductionState.WaitingForBook && !m.timer.Running, "first completion waits for book");
        m.altarIndicators.Refresh();Check(m.altarIndicators.Arrows.All(a=>!a.gameObject.activeSelf),"completed challenge indicators hidden");
        m.ui.SubmitButton.onClick.Invoke();Check(m.Correct==1,"repeated correct tap cannot skip next challenge");
        paused = m.timer.Remaining; yield return new WaitForSecondsRealtime(.15f);
        Check(m.timer.Remaining == paused && paused < m.startingSeconds, "countdown retained between challenges");
        for (int index = 1; index < 5; index++)
        {
            Check(m.ChallengeIndex == index && m.Tablets.Count == m.Challenge.choices.Length, "question and choice count " + index);
            Check(m.Tablets.Select(t => t.Data.id).Distinct().Count() == m.Tablets.Count && m.Challenge.answers.Values.All(id => m.Tablets.Any(t => t.Data.id == id)), "all required data available " + index);
            Face(m.book.position); yield return null; m.ui.InteractButton.onClick.Invoke();
            Check(m.State == InductionState.Active && m.timer.Running, "context book activation " + index);
            CheckIndicators(m,index);
            foreach (var answer in m.Challenge.answers)
            {
                var tablet = m.Tablets.Single(t => t.Data.id == answer.Value);
                var slot = m.slots.Single(s => s.key == answer.Key);
                yield return Collect(m,hand,tablet);
                Face(slot.transform.position); yield return null; Check(m.Target == slot, "context selects " + answer.Key);
                m.ui.InteractButton.onClick.Invoke(); Check(slot.Tablet == tablet && m.Held == null, "place " + answer.Key);
                Check(!tablet.HasError, "placement is not graded " + answer.Key);
                if (index >= 3 && slot.key.StartsWith("Step:")) Check(tablet.label.text.StartsWith(slot.key.Substring(5) + ". "), "placed proof line number");
                if(index>=3 && answer.Equals(m.Challenge.answers.First()))
                {
                    yield return null;float beforePartial=m.timer.Remaining;m.ui.SubmitButton.onClick.Invoke();
                    Check(m.Correct==index&&m.State==InductionState.Active&&slot.Tablet==tablet&&!slot.HasError&&m.Feedback.Contains("missing")&&beforePartial-m.timer.Remaining>=9.99f,"partial multi-scroll proof rejected and correct placement retained");
                    m.OpenReading("board");float readTime=m.timer.Remaining;m.ui.SubmitButton.onClick.Invoke();
                    Check(!m.CanSubmit&&m.timer.Remaining==readTime,"reading blocks Submit logic");m.CloseReading();
                }
            }
            if (index == 3)
            {
                var first = m.slots.Single(s => s.key == "Step:1"); var second = m.slots.Single(s => s.key == "Step:2");
                Face(first.Tablet.transform.position); yield return null; Check(m.PickUp(first.Tablet), "remove placed tablet");
                Face(second.transform.position); yield return null; Check(m.Place(second), "ordered proof swap");
                Face(first.transform.position); yield return null; Check(m.Place(first), "finish swapped proof");
                yield return null; m.ui.SubmitButton.onClick.Invoke();
                Check(m.State == InductionState.Active && m.Correct == 3 && first.HasError && second.HasError, "wrong order rejected");
                Check(!m.slots.Single(s => s.key == "Step:3").HasError && m.slots.Single(s => s.key == "Step:3").Tablet.Data.id == "step-3", "correct lines preserved");
                Face(first.Tablet.transform.position); yield return null; Check(m.PickUp(first.Tablet), "remove swapped line");
                Face(second.transform.position); yield return null; Check(m.Place(second), "restore second line");
                Face(first.transform.position); yield return null; Check(m.Place(first), "restore first line");
            }
            if (index == 4)
            {
                var slot = m.slots.Single(s => s.key == "Hypothesis:1");
                Face(slot.Tablet.transform.position); yield return null; Check(m.PickUp(slot.Tablet), "remove hypothesis"); m.ReturnHeld();
                yield return null; m.ui.SubmitButton.onClick.Invoke();
                Check(m.Correct == 4 && slot.HasError && m.Feedback.Contains("missing"), "full proof missing section rejected");
                var tablet = m.Tablets.Single(t => t.Data.id == "odd-hyp");
                yield return Collect(m,hand,tablet);
                Face(slot.transform.position); yield return null; Check(m.Place(slot), "repair missing section");
            }
            yield return null;Check(!m.IsTargetable(m.book.position),"proof submission stays at altar");m.ui.SubmitButton.onClick.Invoke();
            Check(m.Correct == index + 1 && !m.timer.Running && m.completionSeals[index].enabled, "completion seal " + index);
            if (index < 4) { paused = m.timer.Remaining; yield return new WaitForSecondsRealtime(.1f); Check(m.timer.Remaining == paused, "waiting pause " + index); }
        }
        Check(m.State == InductionState.StageComplete && (m.staircaseLight == null || m.staircaseLight.enabled) && m.ui.Result.Root.gameObject.activeSelf, "stage completion and optional staircase");
        Check(m.Correct == 5 && m.Mistakes == 6 && m.Tablets.Count == 0 && m.Held == null, "final statistics and cleanup");
        Debug.Log("INDUCTION_PLAY_CHECKS_PASS: automatic/paged/reopened Help pauses and restores state, required altar indicators for all five challenges, scroll selection without obsolete options, duplicates, altar submission, retries, shared timer and completion.");
    }
    static IEnumerator Collect(InductionStageManager m,Button hand,InductionTablet tablet)
    {
        Face(m.scrollStand.position);yield return null;Check(m.Target==m.scrollStand,"stand selected");hand.onClick.Invoke();
        Check(m.BrowsingScrolls&&m.ui.Collection.activeSelf,"catalogue opens");
        m.ui.SelectScroll(tablet);Check(m.ui.GetScrollButton.interactable,"available exact scroll");
        Check(m.ui.Collection.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="Selected Formula").text==tablet.Data.expression,"full exact expression readable");
        m.ui.GetScrollButton.onClick.Invoke();
        Check(m.Held==tablet&&!tablet.AtStand&&!m.Reading&&tablet.gameObject.activeSelf,"collection enters existing carry system");
        m.ui.GetScrollButton.onClick.Invoke();Check(m.Held==tablet&&m.Tablets.Select(t=>t.Data.id).Distinct().Count()==m.Tablets.Count,"repeated get suppressed");
    }
    public static IEnumerator CheckTimeout(bool penalty)
    {
        var m = InductionStageManager.Instance; while (!m.Ready||!m.help.IsOpen) yield return null;m.help.BeginButton.onClick.Invoke();yield return InductionOnboardingPlayChecks.Run(false);
        m.timer.Initialize(2,m.HandleGameOver); Face(m.book.position); yield return null; m.InteractBook();yield return null;
        if (penalty)
        {
            var hand=GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b=>b.name=="InteractButton");
            m.timer.Initialize(1080,m.HandleGameOver);m.timer.Resume();yield return Collect(m,hand,m.Tablets.Single(t=>t.Data.id=="base-wrong-zero"));
            var slot=m.slots.Single(s=>s.key=="Base:1");Face(slot.transform.position);yield return null;Check(m.Place(slot),"timeout scroll placed");
            m.timer.Initialize(2,m.HandleGameOver);m.timer.Resume();yield return null;m.ui.SubmitButton.onClick.Invoke();
        }
        else m.timer.Tick(3);
        Check(m.State==InductionState.GameOver && m.timer.Remaining==0 && !m.timer.Running && GameInput.Instance.GameplayInputBlocked && m.ui.Result.Root.gameObject.activeSelf, "timeout " + penalty);
        Debug.Log("INDUCTION_TIMEOUT_CHECK_PASS: " + (penalty ? "submission penalty" : "countdown expiry"));
    }
    static void Face(Vector3 target)
    {
        var position = target - Vector3.forward * 2.4f; position.y = 29.12f;
        var manager = InductionStageManager.Instance;
        // Approach raised scrolls from outside the solid altar, as a player would.
        // Answer targets at the rear are reachable from the side; choices at the
        // front can each be selected without a neighbouring scroll taking priority.
        foreach (var station in manager.stations)
        {
            var renderer = station.GetComponent<MeshRenderer>(); if (renderer == null) continue;
            var bounds = renderer.bounds;
            if (target.x < bounds.min.x || target.x > bounds.max.x || target.z < bounds.min.z || target.z > bounds.max.z) continue;
            bool slot = manager.slots.Any(s => Mathf.Abs(s.transform.position.x - target.x) < .05f && Mathf.Abs(s.transform.position.z - target.z) < .05f);
            position = slot ? new Vector3(target.x <= bounds.center.x ? bounds.min.x - 1 : bounds.max.x + 1,29.12f,target.z)
                : new Vector3(target.x,29.12f,bounds.min.z-1);
            break;
        }
        Vector3 direction=target-position;direction.y=0;
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject, position, Quaternion.LookRotation(direction));
    }
    static void CheckIndicators(InductionStageManager m,int index)
    {
        m.altarIndicators.Refresh();var expected=index==0?new[]{0}:index==1?new[]{1}:index<4?new[]{2}:new[]{0,1,2};
        Check(Enumerable.Range(0,3).All(i=>m.altarIndicators.Arrows[i].gameObject.activeSelf==expected.Contains(i)),"required altar indicators "+index);
        foreach(int i in expected)
        {
            float top=m.slots.Where(s=>s.key.StartsWith(new[]{"Base:","Hypothesis:","Step:"}[i])).Max(s=>s.transform.position.y+s.formulaLabelHeight+.55f);
            Check(m.altarIndicators.Arrows[i].position.y-.8f*m.altarIndicators.arrowScale>top,"indicator clears scroll labels "+index);
        }
    }
    static void Check(bool valid, string name)
    { if (!valid) throw new InvalidOperationException("Induction Play check failed: " + name); }
}
