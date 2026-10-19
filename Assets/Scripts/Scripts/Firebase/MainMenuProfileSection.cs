using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Account and equipped-outfit overview. Journey progression is deliberately independent.</summary>
public sealed class MainMenuProfileSection : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName, outfitName;
    [SerializeField] private MenuDataFeedback presentation;
    [SerializeField] private Button accountInformation, close, retry;
    [SerializeField] private GameObject accountPanel;
    [SerializeField] private RawImage characterImage;
    [SerializeField] private CharacterInspectView inspection;
    [SerializeField] private GameObject maleMannequin, femaleMannequin;
    [SerializeField] private CustomizationManager.EquippableModel[] models;
    [SerializeField] private ShopItem[] catalog;
    [Header("Profile Overview")]
    [SerializeField] private TMP_Text playerId, coins, gems;
    [SerializeField] private Button editName, copyId, customize;
    [SerializeField] private MainMenuManager menu;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture portraitTexture;

    private readonly Dictionary<GameObject, bool> previousStates = new Dictionary<GameObject, bool>();
    private FirebaseAuth auth;
    private string userId;
    private int requestVersion;
    private bool accountWasOpen;
    private volatile bool authChanged;
    private RenderTexture previousTexture;
    private float previousAspect;
    private bool capturedTexture;

    private void Awake()
    {
        close.onClick.AddListener(Hide);
        retry.onClick.AddListener(Reload);
        accountInformation.onClick.AddListener(OpenAccount);
        editName.onClick.AddListener(() =>
        {
            OpenAccount();
            if (accountPanel.activeSelf) accountPanel.GetComponent<ProfileAccountInformationUI>().ShowUsernameEditor();
        });
        copyId.onClick.AddListener(CopyPlayerId);
        customize.onClick.AddListener(() => { Hide(); menu.ShowCustomizationMenu(); });
        accountPanel.SetActive(false);
    }
    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }
    public void Hide() => gameObject.SetActive(false);
    private void OnEnable() => Reload();
    private void OnDisable()
    {
        requestVersion++;
        StopAllCoroutines();
        if (auth != null) auth.StateChanged -= OnAuthChanged;
        accountPanel.SetActive(false);
        accountWasOpen = false;
        RestorePreview();
    }
    private void OnAuthChanged(object sender, EventArgs args) => authChanged = true;
    private void Update()
    {
        if (authChanged)
        {
            authChanged = false;
            if (auth != null && auth.CurrentUser?.UserId != userId) { Hide(); return; }
        }
        if (accountWasOpen && !accountPanel.activeSelf)
        {
            accountWasOpen = false;
            if (!string.IsNullOrEmpty(userId) && PlayerUsername.TryGetVerified(userId, out string verified))
                playerName.text = verified;
        }
    }

    private void OpenAccount()
    {
        if (auth == null || auth.CurrentUser?.UserId != userId) return;
        accountWasOpen = true;
        accountPanel.SetActive(true);
        accountPanel.transform.SetAsLastSibling();
    }
    public void Reload()
    {
        if (!isActiveAndEnabled) return;
        StopAllCoroutines();
        RestorePreview();
        presentation.BeginLoad("Loading your profile...");
        playerName.text = outfitName.text = playerId.text = coins.text = gems.text = string.Empty;
        accountInformation.interactable = false;
        editName.interactable = copyId.interactable = customize.interactable = false;
        retry.gameObject.SetActive(false);
        StartCoroutine(LoadProfile(++requestVersion));
    }
    private IEnumerator LoadProfile(int version)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!FirebaseManager.IsReady && string.IsNullOrEmpty(FirebaseManager.InitializationError) &&
               Time.realtimeSinceStartup < deadline) yield return null;
        if (!FirebaseManager.IsReady) { Error("Account services are unavailable. Please retry."); yield break; }
        if (auth != null) auth.StateChanged -= OnAuthChanged;
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += OnAuthChanged;
        var user = auth.CurrentUser;
        if (user == null) { Error("Sign in to view your profile."); yield break; }
        userId = user.UserId;
        string id = userId;
        accountInformation.interactable = true;
        editName.interactable = copyId.interactable = true;
        playerId.text = id;
        if (PlayerUsername.TryGetVerified(id, out string cached)) playerName.text = cached;
        var data = FirebaseDatabase.DefaultInstance.RootReference.Child("users").Child(id);
        // Read only account presentation and equipment; no trial/progression data is requested.
        var request = Task.WhenAll(data.Child("username").GetValueAsync(),
            data.Child("base_character").GetValueAsync(), data.Child("equipped").Child("clothes").GetValueAsync(),
            data.Child("coins").GetValueAsync(), data.Child("gems").GetValueAsync());
        request.ContinueWithOnMainThread(task =>
        {
            if (!Current(version, id)) return;
            if (task.IsCanceled || task.IsFaulted) { Error("Couldn't load your profile. Check your connection and retry."); return; }
            string name = task.Result[0].Value?.ToString();
            playerName.text = string.IsNullOrEmpty(name) ? "Player" : name;
            if (PlayerUsername.TryNormalize(name, out string normalized)) PlayerUsername.SetVerified(id, normalized);
            coins.text = CurrencyLabel(task.Result[3]);
            gems.text = CurrencyLabel(task.Result[4]);
            string bodyId = task.Result[1].Value?.ToString();
            if (bodyId != "Male_Character" && bodyId != "Female_Character")
            { Error("Choose your character before viewing your outfit."); return; }
            var body = bodyId == "Female_Character" ? femaleMannequin : maleMannequin;
            string clothes = task.Result[2].Value?.ToString();
            if (string.IsNullOrEmpty(clothes)) clothes = bodyId == "Female_Character" ? "female_default" : "male_default";
            CustomizationManager.EquippableModel selected = null;
            foreach (var model in models)
                if (model != null && model.model != null && model.itemID == clothes &&
                    body != null && model.model.transform.IsChildOf(body.transform)) { selected = model; break; }
            outfitName.text = OutfitLabel(clothes);
            if (selected == null) { Error("This outfit preview is unavailable. Please retry."); return; }
            Remember(maleMannequin); Remember(femaleMannequin);
            foreach (var model in models) if (model != null) Remember(model.model);
            foreach (var model in models) if (model != null && model.model != null)
                model.model.SetActive(model == selected);
            maleMannequin.SetActive(body == maleMannequin);
            femaleMannequin.SetActive(body == femaleMannequin);
            previousTexture = previewCamera.targetTexture;
            previousAspect = previewCamera.aspect;
            capturedTexture = true;
            previewCamera.targetTexture = portraitTexture;
            previewCamera.aspect = (float)portraitTexture.width / portraitTexture.height;
            characterImage.texture = portraitTexture;
            inspection.ShowCharacter(body.transform, selected.model.transform);
            customize.interactable = true;
            retry.gameObject.SetActive(false);
            StartCoroutine(RevealCharacter(version, id));
        });
        deadline = Time.realtimeSinceStartup + 20f;
        while (!request.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (!request.IsCompleted && Current(version, id)) Error("Your connection is taking longer. You can wait or retry.");
    }
    private bool Current(int version, string id) => this != null && isActiveAndEnabled &&
        version == requestVersion && auth != null && auth.CurrentUser?.UserId == id;
    private IEnumerator RevealCharacter(int version, string id)
    {
        yield return null;
        if (Current(version, id))
        {
            characterImage.gameObject.SetActive(true);
            presentation.RevealWhenReady();
        }
    }
    private static string CurrencyLabel(DataSnapshot snapshot)
    {
        if (!snapshot.Exists || snapshot.Value == null) return "0";
        return long.TryParse(snapshot.Value.ToString(), out long value) ? value.ToString("N0") : "--";
    }
    private void CopyPlayerId()
    {
        if (auth == null || string.IsNullOrEmpty(userId) || auth.CurrentUser?.UserId != userId) return;
        GUIUtility.systemCopyBuffer = userId;
        presentation.ShowTransient("Player ID copied.");
    }
    private string OutfitLabel(string id)
    {
        if (id == "male_default" || id == "female_default") return "Default Outfit";
        foreach (var item in catalog) if (item != null && item.ItemID == id) return item.DisplayName;
        return "Equipped Outfit";
    }
    private void Remember(GameObject value)
    {
        if (value != null && !previousStates.ContainsKey(value)) previousStates.Add(value, value.activeSelf);
    }
    private void RestorePreview()
    {
        inspection.Clear();
        if (capturedTexture && previewCamera != null)
        {
            previewCamera.targetTexture = previousTexture;
            previewCamera.aspect = previousAspect;
        }
        capturedTexture = false;
        characterImage.gameObject.SetActive(false);
        foreach (var state in previousStates) if (state.Key != null) state.Key.SetActive(state.Value);
        previousStates.Clear();
    }
    private void Error(string message)
    {
        presentation.FailLoad(message);
        retry.gameObject.SetActive(true);
    }
}
