using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A fixed-size cell: pending controls never change grid geometry.</summary>
public sealed class GemPackCard : MonoBehaviour
{
    [SerializeField] private TMP_Text packName, packPrice, actionLabel, paymentStatus;
    [SerializeField] private Button buyButton, cancelPaymentButton;
    [SerializeField] private RectTransform heading, gemIcon, priceFrame;
    [SerializeField] private CommerceClient.Pack pack;
    public CommerceClient.Pack Pack => pack;
    public void Bind(CommerceClient.Pack pack, Action<GemPackCard> buy, Action cancel)
    {
        this.pack = pack;
        packName.text = pack.gems.ToString("N0", CultureInfo.InvariantCulture) + " Gems";
        packPrice.text = "PHP " + (pack.amount / 100m).ToString("N2", CultureInfo.InvariantCulture);
        buyButton.onClick = new Button.ButtonClickedEvent();
        cancelPaymentButton.onClick = new Button.ButtonClickedEvent();
        buyButton.onClick.AddListener(() => buy(this));
        cancelPaymentButton.onClick.AddListener(() => cancel());
    }
    public void Present(bool pending, bool processing, bool review, bool canBuy, bool canCancel, bool checking = false)
    {
        ApplyLayout(pending);
        actionLabel.text = checking ? "CHECK PAYMENT" : processing ? "VERIFYING..." : pending ? "CONTINUE PAYMENT" : "BUY GEMS";
        actionLabel.fontSize = pending || checking ? 18 : 20;
        buyButton.interactable = canBuy;
        cancelPaymentButton.gameObject.SetActive(pending && !processing && !review);
        cancelPaymentButton.interactable = canCancel;
        paymentStatus.gameObject.SetActive(pending);
        paymentStatus.text = review ? "NEEDS REVIEW\nPlease contact support." : processing ?
            "VERIFYING PAYMENT\nPlease wait for confirmation." : "PAYMENT PENDING";
    }
    private void ApplyLayout(bool pending)
    {
        // Move content within a fixed grid cell, never the card or the row below.
        Place(heading, pending ? 120 : 104, 208, 32);
        Place(gemIcon, pending ? 76 : 55, pending ? 44 : 48, pending ? 44 : 48);
        Place(packName.rectTransform, pending ? 36 : 4, 208, 38);
        Place(packPrice.rectTransform, pending ? 2 : -33, 200, 32);
        Place(priceFrame, pending ? 2 : -33, 200, 28);
        Place(buyButton.transform as RectTransform, pending ? -42 : -90, 196, 52);
        Place(cancelPaymentButton.transform as RectTransform, -96, 196, 44);
        Place(paymentStatus.rectTransform, -136, 208, 40);
    }
    private static void Place(RectTransform rect, float y, float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(width, height);
    }
}
