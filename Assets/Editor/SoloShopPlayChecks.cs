#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Tests the existing Main Menu shop with fake wallets and real touch controls. Never signs in or spends real funds.</summary>
public static class SoloShopPlayChecks
{
    const string Flag="SoloEconomy.ShopQA",Report="Library/SoloShopPlayChecks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int coins,gems,purchases,finger;
    static readonly HashSet<string> owned=new HashSet<string>();
    static string character;
    static SoloEconomy.CosmeticPrice[] quotes;
    static SoloEconomy.Config policy;
    static Touchscreen touch;
    [Serializable] sealed class Request { public string itemId,currency;public int expectedPrice,economyVersion; }
    [Serializable] sealed class Reply { public int status;public bool success;public string body; }
    [Serializable] sealed class Catalog { public Entry[] items; }
    [Serializable] sealed class Entry { public string id,rarity,character; }
    [MenuItem("Logic Legends/Economy/Check Coins and Gems Shop in Play Mode")]
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the scene and exit Play Mode.");
        SessionState.SetString(Flag+".Scene",SceneManager.GetActiveScene().path);SessionState.SetInt(Flag+".Size",BookMapMobileChecks.GetSize());SessionState.SetBool(Flag,true);
        EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity");EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Bootstrap()
    {
        if(!SessionState.GetBool(Flag,false))return;
        SetStatic(typeof(CommerceClient),"ValidationUid","solo-shop-fixture");
        SetStatic(typeof(CommerceClient),"ValidationRequest",(Func<string,string,string,int,Task<string>>)Transport);
        SetStatic(typeof(TrialBackendClient),"ValidationTransport",(Func<string,string,Task<string>>)((a,b)=>Task.FromResult("{\"admin\":false}")));
        TeleportManager.ValidationNavigation=scene=>{};
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Loaded()
    {
        if(SessionState.GetBool(Flag,false))new GameObject("Isolated Shop QA Runner").AddComponent<TrialAccountQaRunner>().StartCoroutine(Guard(Run()));
    }
    [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;}
    static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);TeleportManager.ValidationNavigation=null;
        SetStatic(typeof(CommerceClient),"ValidationRequest",null);SetStatic(typeof(CommerceClient),"ValidationUid",null);SetStatic(typeof(TrialBackendClient),"ValidationTransport",null);
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag+".Size",6));
        EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(SessionState.GetString(Flag+".Scene","Assets/Scenes/PRELIM.unity"));
    }
    static void SetStatic(Type type,string field,object value)=>type.GetField(field,BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,value);
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Hidden).SetValue(target,value);
    static T Field<T>(object target,string field)=>(T)target.GetType().GetField(field,Hidden).GetValue(target);
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Hidden).Invoke(target,args);
    static CommerceClient.Account Account()=>new CommerceClient.Account {coins=coins,gems=gems,inventory=owned.ToArray(),character=character,economy=policy,catalog=quotes,sandbox=CommerceClient.Sandbox,live=!CommerceClient.Sandbox};
    static Task<string> Transport(string url,string json,string token,int timeout)
    {
        if(url.EndsWith("/health"))return Task.FromResult(JsonUtility.ToJson(new Reply{status=200,success=true,body="{\"mode\":\"fixture\"}"}));
        if(url.EndsWith("/purchase")) {
            var r=JsonUtility.FromJson<Request>(json);var q=quotes.Single(p=>p.id==r.itemId);int price=r.currency=="coins"?q.coins:q.gems;
            if(r.economyVersion!=2||r.expectedPrice!=price)throw new InvalidOperationException("Invalid quote");
            if(owned.Add(r.itemId)){if(r.currency=="coins")coins-=price;else gems-=price;purchases++;}
        }
        return Task.FromResult(JsonUtility.ToJson(new Reply{status=200,success=true,body=JsonUtility.ToJson(Account())}));
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack=new Stack<IEnumerator>();stack.Push(work);
        while(stack.Count>0) {
            object next;
            try {var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}next=top.Current;}
            catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}
            if(next is IEnumerator child)stack.Push(child);else yield return next;
        }
        File.AppendAllText(Report,"ALL COIN/GEM SHOP CHECKS PASSED\n");EditorApplication.isPlaying=false;
    }
    static IEnumerator Run()
    {
        File.WriteAllText(Report,"Actual saved Main Menu UI. Test transport, no real funds or inventory writes.\n");
        Application.runInBackground=true;BookMapMobileChecks.SetSize(6);yield return null;yield return null;
        float readyBy=Time.realtimeSinceStartup+10;
        while(TeleportManager.Instance!=null&&TeleportManager.Instance.IsTransitioning&&Time.realtimeSinceStartup<readyBy)yield return null;
        yield return new WaitForSecondsRealtime(.8f);
        UnityEngine.Object.FindFirstObjectByType<MainMenuManager>()?.ShowShopMenu();
        var shop=UnityEngine.Object.FindFirstObjectByType<ShopManager>(FindObjectsInactive.Include);
        for(Transform t=shop.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);
        yield return null;shop.StopAllCoroutines();
        var preview=Field<ShopPreviewUI>(shop,"previewUI");
        var overlay=Field<GameObject>(preview,"overlay");
        var canvas=overlay.GetComponent<Canvas>();if(canvas==null)canvas=overlay.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=4000;
        if(overlay.GetComponent<GraphicRaycaster>()==null)overlay.AddComponent<GraphicRaycaster>();
        policy=JsonUtility.FromJson<SoloEconomy.Config>(Resources.Load<TextAsset>("SoloEconomy").text);
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText("Backend/commerce/catalog.json"));
        quotes=catalog.items.Select(i=>{var r=policy.rarities.Single(t=>t.name==i.rarity);return new SoloEconomy.CosmeticPrice{id=i.id,rarity=i.rarity,coins=r.coins,gems=r.gems};}).ToArray();
        touch=InputSystem.AddDevice<Touchscreen>();finger=500;
        foreach(var item in Field<ShopItem[]>(shop,"allShopItems"))foreach(var currency in new[]{ShopItem.Currency.Coins,ShopItem.Currency.Gems})
        {
            shop.StopAllCoroutines();preview.Close();
            character=catalog.items.Single(i=>i.id==item.ItemID).character;coins=200000;gems=2000;purchases=0;owned.Clear();owned.Add("legacy_outfit");
            Set(shop,"userId","solo-shop-fixture");Set(shop,"requestVersion",99);Set(shop,"playerBaseCharacter",character);
            Set(shop,"dataLoaded",true);Set(shop,"commerceReady",true);Set(shop,"revealing",false);Set(shop,"purchasing",false);Set(shop,"nextWalletCheck",float.MaxValue);
            Call(shop,"ApplyAccount",Account());Call(shop,"UpdateShopUI");Call(shop,"SetReveal",1f,true);
            item.SelectCurrency(ShopItem.Currency.Coins);shop.AttemptPurchase(item);yield return null;yield return null;
            if(item.ItemID=="Common_Skin"&&currency==ShopItem.Currency.Coins){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/SoloShop_FirstPreview.png");yield return null;}
            Check(preview.IsOpen,item.ItemID+": centered preview opens");
            if(currency==ShopItem.Currency.Gems)yield return Tap(Field<Button>(preview,"payGems"));
            Check(item.CurrencyType==currency&&Field<TMP_Text>(preview,"currencyUnit").text==currency.ToString().ToUpperInvariant(),item.ItemID+": explicit "+currency+" quote");
            if(item.ItemID=="Common_Skin"&&currency==ShopItem.Currency.Gems)
                foreach(int size in new[]{6,12,3}) {BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();CheckLayout(preview);ScreenCapture.CaptureScreenshot("Library/SoloShop_"+Screen.width+"x"+Screen.height+".png");}
            BookMapMobileChecks.SetSize(6);yield return null;yield return null;
            int price=item.Price;yield return Tap(Field<Button>(preview,"primary"));Check(preview.IsConfirming,"explicit confirmation before spending");
            Check(!Field<Button>(preview,"payCoins").IsInteractable()&&!Field<Button>(preview,"payGems").IsInteractable(),"currency cannot change during confirmation");
            yield return Tap(Field<Button>(preview,"primary"));shop.ConfirmPurchase();
            Check(Field<TMP_Text>(preview,"feedbackHeading").text=="PURCHASE SUCCESSFUL"&&Field<HashSet<string>>(shop,"ownedItems").Contains(item.ItemID),item.ItemID+": client accepts the server receipt and shows success");
            Check(purchases==1&&owned.Contains(item.ItemID)&&owned.Contains("legacy_outfit"),item.ItemID+": one ownership grant; existing inventory preserved");
            Check(coins==200000-(currency==ShopItem.Currency.Coins?price:0)&&gems==2000-(currency==ShopItem.Currency.Gems?price:0),item.ItemID+": only the selected currency changes");
        }
        preview.Close();InputSystem.RemoveDevice(touch);touch=null;
    }
    static void CheckLayout(ShopPreviewUI preview)
    {
        foreach(string name in new[]{"price","balance","currencyUnit","message"}) {var t=Field<TMP_Text>(preview,name);t.ForceMeshUpdate();Check(!t.isTextOverflowing,name+" fits at "+Screen.width+"x"+Screen.height);}
        Check(!Bounds(Field<Button>(preview,"payCoins").transform as RectTransform).Overlaps(Bounds(Field<Button>(preview,"payGems").transform as RectTransform)),"currency buttons have separate mobile targets");
    }
    static IEnumerator Tap(Button button)
    {
        Canvas.ForceUpdateCanvases();Check(button.IsInteractable(),"shop touch action available: "+button.name);var point=Bounds(button.transform as RectTransform).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"shop raycast reaches "+button.name+" at "+point+" ("+Screen.width+"x"+Screen.height+"): "+string.Join(",",hits.Take(5).Select(h=>h.gameObject.name+"/"+h.gameObject.GetComponentInParent<Canvas>().sortingOrder)));
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
    static Rect Bounds(RectTransform t){var c=new Vector3[4];t.GetWorldCorners(c);var p=c.Select(v=>RectTransformUtility.WorldToScreenPoint(null,v));return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));}
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
}
#endif
