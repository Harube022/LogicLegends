#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Full page/diagram geometry and real touch navigation at four landscape sizes.</summary>
public static class MinimalLibraryReadingChecks
{
    const string Report="Library/MinimalLibraryReadingChecks.txt";
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,Fields).GetValue(o);
    static void Check(bool okay,string reason){if(!okay)throw new InvalidOperationException(reason);}
    static IEnumerator Tap(Button button)=>LibraryReadingPlayChecks.Tap(button);
    public static IEnumerator Run()
    {
        var resume=UnityEditor.SessionState.GetString("Library.ReaderQA.From","").Split(':');
        bool resuming=resume.Length==3;int startSize=resuming?int.Parse(resume[1]):2;
        if(!resuming||!File.Exists(Report))File.WriteAllText(Report,"Minimalist Study Library: isolated profile, InputSystem simulated touch, Unity Editor.\n");
        var reader=UnityEngine.Object.FindFirstObjectByType<StudyLibraryController>();
        var root=Get<GameObject>(reader,"readerRoot");
        var next=Get<Button>(reader,"nextButton");var previous=Get<Button>(reader,"previousButton");
        var back=root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Close Book");
        Check(!root.GetComponentsInChildren<Transform>(true).Any(t=>new[]{"Book Title","Reading Hint","Source","Spread Indicator","Enlarged Source Page"}.Contains(t.name)),"Removed UI must not be constructed");
        Check(Get<RectTransform>(reader,"leftPageRect").GetComponent<Button>()==null,"Page enlargement must have no tap handler");
        int totalPages=0;
        foreach(int size in new[]{2,4,6,12}.SkipWhile(s=>s!=startSize))
        {
            bool partial=resuming;
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.2f);
            int checkedPages=0;
            foreach(string period in new[]{"PRELIM","MIDTERMS","PRE-FINALS","FINALS"})
            {
                var shelf=UnityEngine.Object.FindObjectsByType<StudyShelfZone>(FindObjectsSortMode.None).Single(s=>s.Period==period);
                LibraryReadingPlayChecks.Face(shelf.transform.position,shelf.transform.position+shelf.FocusCamera.transform.forward*4);
                yield return new WaitForSecondsRealtime(.4f);
                var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="InteractButton");
                yield return Tap(hand);Check(reader.IsViewing(shelf),"Hand opens "+period);
                yield return new WaitForSecondsRealtime(.9f);
                var cards=shelf.GetComponentsInChildren<Button>().OrderBy(b=>b.name).ToArray();
                foreach(var card in cards)
                {
                    if(resuming)
                    {
                        string id=Get<StudyBookDefinition[]>(shelf,"books")[Array.IndexOf(cards,card)].topicId;
                        if(id!=resume[2])continue;
                        resuming=false;
                    }
                    if(!reader.IsViewing(shelf))reader.EnterShelf(shelf);
                    yield return Tap(card);yield return null;Canvas.ForceUpdateCanvases();
                    var book=Get<StudyBookDefinition>(reader,"currentBook");
                    Check(book.lessonPages[0].cover,"Every book starts with a cover");
                    Check(Strip(book.lessonPages[0].text).StartsWith("Discrete Structures 1\n\n"),"Cover contains the course and topic");
                    Check(!previous.interactable,"Previous disabled on first spread");
                    int firstCount=book.PageCount;
                    while(true)
                    {
                        int page=reader.CurrentSpreadStartPage;
                        CheckPage(reader,book,page,"left");checkedPages++;
                        if(page+1<book.PageCount){CheckPage(reader,book,page+1,"right");checkedPages++;}
                        else
                        {
                            Check(!Get<TMP_Text>(reader,"rightPageText").gameObject.activeInHierarchy,"Unused right page has no end message");
                            Check(Get<TMP_Text>(reader,"rightPageNumberText").text=="","Unused page has no number");
                        }
                        if(!next.interactable)break;
                        if(size==2&&page==0)
                        {ScreenCapture.CaptureScreenshot("Library/MinimalBook-"+book.topicId+".png");yield return null;}
                        yield return Tap(next);
                        // A tap storm must still make exactly one spread transition.
                        for(int spam=0;spam<5;spam++){reader.NextSpread();reader.PreviousSpread();}
                        Check(reader.CurrentSpreadStartPage==page+2,"One turn advances exactly two individual pages");
                        while(reader.IsTurningPage)yield return null;
                        yield return null;
                    }
                    int last=reader.CurrentSpreadStartPage;reader.NextSpread();Check(reader.CurrentSpreadStartPage==last,"End boundary");
                    if(previous.interactable)
                    {
                        yield return Tap(previous);while(reader.IsTurningPage)yield return null;
                        Check(reader.CurrentSpreadStartPage==last-2,"Previous flips backward");
                        yield return Tap(next); // Exercise Back during a turn.
                    }
                    yield return Tap(back);yield return null;
                    Check(!reader.IsInReadingView&&!reader.IsTurningPage&&Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked,"Back clears camera/input and active turn");
                    Check(Player.LocalInstance.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff),"Avatar visibility restored");
                    Check(!root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Page Turn Preview"),"No stale animated sheets");
                    Check(book.PageCount==firstCount,"Navigation does not change page count");
                    File.AppendAllText(Report,"PASS "+Screen.width+"x"+Screen.height+" "+book.topicId+": "+book.PageCount+" pages, geometry, glyphs, touch, forward/back, rapid taps and Back cancellation.\n");
                }
                if(reader.IsInReadingView)reader.CloseBook();
            }
            if(!partial){if(totalPages==0)totalPages=checkedPages;else Check(checkedPages==totalPages,"Pagination remains consistent across aspect ratios");}
        }
        File.AppendAllText(Report,"PASS: "+totalPages+" pages per resolution; all four landscape sizes. No real progress/rewards changed.\n");
    }
    static void CheckPage(StudyLibraryController reader,StudyBookDefinition book,int index,string side)
    {
        var data=book.lessonPages[index];var text=Get<TMP_Text>(reader,side+"PageText");
        var number=Get<TMP_Text>(reader,side+"PageNumberText");
        Check(number.text==(index+1).ToString(),book.topicId+" page "+index+": simple individual page number");
        Check(number.alignment==TextAlignmentOptions.BottomLeft,"Page number is bottom-left");
        Check(number.rectTransform.anchorMax.y<text.rectTransform.anchorMin.y,"Page number has reserved content clearance");
        if(string.IsNullOrEmpty(data.diagram))
        {
            Check(text.text==data.text,"Lesson text stays intact");
            Check(!text.enableAutoSizing&&text.fontSize==StudyLibraryPagination.BodySize,"Fixed body font, never shrink-to-fit");
            CheckText(text,book.topicId+" page "+(index+1));
            Check(!Regex.IsMatch(Strip(text.text),@"\b(quiz|semester|prelim|midterm|pre-final|week|uploaded|PPT)\b",RegexOptions.IgnoreCase),"No quiz or editorial metadata");
        }
        else
        {
            var diagram=Get<GameObject>(reader,side+"Diagram");
            foreach(var label in diagram.GetComponentsInChildren<TMP_Text>())CheckText(label,book.topicId+" / "+data.diagram+" / "+label.text);
        }
    }
    static void CheckText(TMP_Text text,string context)
    {
        text.ForceMeshUpdate();
        var preferred=text.GetPreferredValues(text.text,text.rectTransform.rect.width,Mathf.Infinity);
        Check(preferred.y<=text.rectTransform.rect.height+1,context+": text needs "+preferred.y+" but has "+text.rectTransform.rect.height);
        Check(!text.isTextOverflowing,context+": TMP reports overflowing text");
        var bounds=text.textBounds;var rect=text.rectTransform.rect;
        Check(bounds.min.x>=rect.xMin-1&&bounds.max.x<=rect.xMax+1&&bounds.min.y>=rect.yMin-1&&bounds.max.y<=rect.yMax+1,context+": visible glyphs exceed their content rectangle; bounds="+bounds+" rect="+rect);
        uint[] missing;bool complete=text.font.HasCharacters(Strip(text.text),out missing,true,false);
        Check(complete||missing.All(c=>char.IsControl((char)c)),context+": missing math glyphs "+string.Join(",",missing??new uint[0]));
    }
    static string Strip(string s)=>Regex.Replace(s??"","<[^>]+>","");
}
#endif
