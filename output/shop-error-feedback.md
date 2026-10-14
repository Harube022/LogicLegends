# Shop feedback and errors

Updated 2026-10-14. Clothes purchase feedback appears in the clothes preview. Loading errors appear in the shop because no item preview is ready yet. Top-up messages belong to its separate test checkout.

## Clothes purchase preview

| Message | Meaning and action |
| --- | --- |
| Purchase successful. Equip it in Customization. | The confirmed purchase returned saved ownership. The preview remains open with OWNED disabled. The message lasts for Shop Manager's Purchase Feedback Duration, then returns to the normal ownership note. |
| Saving your purchase... | The confirmed operation is still pending. Appears after Purchase Progress Delay; this is progress, not an error. |
| Purchase cancelled. No currency was spent. | Cancel was pressed before submitting the purchase. |
| Not enough coins / gems. | The available balance is below the item's price. Buying/confirmation is disabled. |
| Not enough coins / gems. Retry to refresh your balance. | The saved balance no longer covers the item when the server transaction checks it. |
| You already own these clothes. Retry to refresh your shop. | Ownership was already saved, possibly from another device or an earlier purchase. It is not charged again. |
| Your saved character changed. Retry to refresh your shop before buying. | The populated saved account has a different base character from the current shop preview. Retry reloads the correct catalog. An initially empty transaction cache no longer triggers this message. |
| Couldn't load your saved character. Retry to refresh your shop. | A populated account record has no base character value. |
| Couldn't read your balance. Retry to refresh your shop. | The selected saved currency is invalid or negative. |
| Couldn't complete the purchase. Retry to refresh your shop. | The database operation failed or was canceled; refresh before attempting another purchase. |
| Couldn't confirm the purchase. Retry to refresh your shop. | The returned account does not confirm ownership of this item. The UI does not report success from an empty result. |
| This outfit preview is unavailable. Please choose another item. | The item's matching mannequin model is missing. Buying is disabled. |

Purchase errors stay visible with RETRY and CLOSE. Both actions refresh the shop; retry does not automatically submit another purchase.

## Shop loading and configuration

| Message | Meaning |
| --- | --- |
| Couldn't connect. Please try again. | Firebase did not become ready within the loading wait, or initialization failed. |
| Please log in to load your shop. | There is no signed-in account. |
| Couldn't load your shop. Check your connection and retry. | Fetching the saved profile failed or was canceled. |
| Choose your character before opening the shop. | The loaded profile has no recognized male/female character selection. |
| Couldn't read your balances. Please retry. | The coins or gems value is invalid or negative. |
| Your connection is taking longer. You can wait or retry. | The account fetch is taking more than 20 seconds. This does not log the player out. |
| The clothes preview is unavailable. Please reopen the shop. | The Main Menu ShopPreviewUI reference is missing. |

## Top-up test checkout

The separate top-up component can report sign-in required, login verification failure, a missing public HTTPS backend URL, unavailable backend/emulators, a backend-provided error, or top-ups disabled in release builds. Pending payment is a status message, not a completed payment; local test gems are separate from game gems.

## Verification

14 pure purchase-rule checks passed, including the initial empty-cache regression, actual changed/missing character, existing ownership, negative/malformed balances, insufficient funds, exact balance, and free items. The checks do not submit Firebase writes. Purchase UI states are verified separately in Play Mode without invoking the final purchase operation.

Firebase documents that a transaction callback can initially receive null local data even when the remote database contains the account: https://firebase.google.com/docs/database/unity/save-data
