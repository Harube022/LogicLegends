# Main Menu gem top-up presentation

Existing gem pack restyled using GraphMapFrame, GraphStatusFrame, the original Gem sprite and WoodButtonGreen. IMPACT SDF throughout; action is 280 x 88 with PPU multiplier 4.5. Card is 480 x 620, centered in the existing scroll panel. Existing pack amount, PHP price, button callback, checkout and wallet logic are preserved.

PayMongoTestTopUp now has an optional hideWalletBalanceStatus flag, enabled only on the Main Menu instance. It hides the duplicate Your gems / Sandbox gems balance line while retaining checking, pending-payment, unavailable and error feedback. The top currency banner remains unchanged.

Validation: no C# compilation errors; balance-only filter and preserved payment feedback verified; no text overflow in Play Mode; existing BuyFiveGems persistent callback and price verified. Preview used temporary runtime-disabled commerce controllers and did not invoke checkout, buy gems or change balances. Runtime overrides are discarded on leaving Play Mode.
