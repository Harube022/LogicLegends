# Main Menu shop preview and purchase confirmation

Implemented 2026-10-14. Scene changes are limited to `Assets/Scenes/Main Menu.unity`. Existing character prefab instances and shared art assets remain connected and unchanged.

## Player flow

1. Open Shop and tap PREVIEW on any compatible clothes, including owned clothes.
2. Inspect the selected outfit on the account's male or female mannequin.
3. For an affordable, unowned item, tap BUY to see the item name, price, currency, current balance, and remaining balance.
4. CANCEL returns to the preview without a purchase. CLOSE restores the previous mannequin state.
5. CONFIRM is the only action that submits a purchase. It does not equip the item. The player can equip purchased clothes through Customization.

Owned clothes have an OWNED action with buying disabled. Missing preview models and insufficient funds also disable buying. Shop-menu closure or an invalid account session hides the preview.

## Authoring

- `ShopManager` owns account data, preview eligibility, and the confirmed purchase operation.
- `ShopPreviewUI` owns the modal and local outfit preview. `Shop_Menu > Shop Preview UI > Preview Zoom` defaults to 1.5 and changes only the shop image, not the customization camera.
- `ShopItem.DisplayName` supplies editable player-facing item names.
- `Logic Legends > Shop > Build Clothes Preview And Confirmation` authors this UI only in Main Menu, outside Play Mode.
- The modal reuses the connected dressing-room mannequins and render texture. It remembers and restores body/outfit active states; previewing does not write equipped data.
- The purchase transaction rechecks saved character, inventory ownership, and the selected currency balance before committing ownership and deduction together. Repeated requests are guarded while a purchase is pending.
- The modal uses the shared GraphMapFrame and existing brown-rimmed action art. Main actions are 280 x 88; catalog PREVIEW actions are 220 x 88. Button artwork multiplier is 4.5.

## Verification

Unity compilation passed. Play Mode checks passed for:

- Owned item preview with purchasing disabled.
- Wrong-character item rejection.
- Coin and gem confirmation details, including remaining balance.
- Cancellation without balance changes.
- Insufficient funds disabling BUY and CONFIRM.
- Male and female mannequin selection.
- Restoration of all mannequin/outfit active states and local balances.
- Actual UI raycasts reaching CANCEL and CONFIRM.
- Enlarged character preview, visually inspected in Game View.

Screenshots: `Temp/ShopPreviewQA/preview-zoom.png` and `Temp/ShopPreviewQA/confirmation-final.png`.

The final purchase operation was not invoked during verification; no Firebase account inventory or currency was changed. Live database commit behavior and an Android build were not tested.

## Feedback follow-up

The 2026-10-14 follow-up fixes the transaction's initial empty-local-cache handling: an unchanged empty value allows Firebase to supply the server record and retry. A populated account with a different saved character still aborts. An empty/unconfirmed final result cannot report purchase success.

Purchase progress, success, and errors now appear only inside the open preview. Success uses the existing Purchase Feedback Duration and returns to the ownership note. Errors retain the modal with Retry/Close while purchase eligibility remains disabled; either action refreshes shop data. An old success timer cannot clear another item's preview.

Fourteen pure validation checks passed. Play Mode simulation also passed for success placement, result timing without modal closure, errors remaining visible after data is invalidated, and Retry reloading without purchasing. No final purchase action or Firebase write was invoked in these tests. See `output/shop-error-feedback.md` for the complete feedback catalog.

## Preview UI polish

The preview now has a framed heading, a larger teal outfit name, price and balance plaques, currency icons, grouped numeric values, and a separate status card. All fourteen text elements use IMPACT SDF at the user's request, including body feedback. Confirmation shows its remaining balance in a separate line. Status headings and accents distinguish preview, confirmation, processing, success, ownership, cancellation, and errors.

Play Mode checks passed for the single font, text fitting in owned/unowned previews, confirmation, success and a long error, correctly formatted remaining balance, and button raycasts. The actual Game View was visually inspected. No purchase action was invoked. Current screenshot: `output/shop-preview-impact.png`.

To manually test confirmation with the existing account, remove only one unneeded inventory entry such as `Elite_Skin`, leaving equipped/default clothes and the rest of the account intact. Open that outfit, tap BUY, then CANCEL to check the confirmation without spending currency. Owned items can be previewed without changing Firebase.
