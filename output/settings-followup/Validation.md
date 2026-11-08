# Settings follow-up

The shared settings panel serves Main Menu and the ten solo trial contexts. Existing account, economy and stage progression behavior is unchanged.

Changes:

- Left-handed controls mirror the outer Joystick, rather than the centered Background object that holds MobileInputUI. The joystick moves right while jump and interact move left. Each authored layout property is tracked independently, so a stage updating only a position does not accidentally mirror anchors twice.
- Graphics quality changes only graphics quality. Shadow quality and lighting quality retain their own selected values.
- Apply displays exactly `Changes Saved` in a larger green label. Editing another value clears the message. Settings text is excluded from puzzle-feedback styling even while its parent panel is inactive, preventing the incorrect `Check:` prefix.
- Closing with pending changes opens a confirmation. Apply saves and closes Settings; Cancel discards edits and closes Settings; X closes only the confirmation and preserves the draft. Closing an unchanged draft exits directly.
- Reset opens a confirmation. Yes selects defaults in the draft, Cancel leaves values unchanged, and X returns to Settings. Apply still saves the confirmed default values.
- Modal backgrounds now stretch across the full root canvas, outside the fitted settings panel and safe-area margins. This applies to Main Menu navigation, unsaved changes, Reset and the shared informational dialogs. Popups are removed when their settings owner closes.
- Solo decorative fixtures no longer run programmed dimming/outage effects. The Truth Table lantern surfaces retain their emission. Unlit flame, torch, lamp and candle surfaces remain unlit during the selective material conversion.
- Local lighting now uses bounded per-pixel light pools: four additional lights per object on Low, six on Medium and eight on High, without additional-light shadow maps, SSAO or HDR. The earlier Low vertex-light profile could illuminate large low-poly surfaces unevenly; Forward light limits also constrain how many lamps affect one mesh. [Unity lighting troubleshooting](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/ts-lights-flicker-disappear)

All 148 offline checks passed in Unity. The fixture uses copies of the actual PRELIM joystick, jump and interaction controls. It checks mirrored geometry, updates from a stage layout, independent quality controls, save feedback, each confirmation action, full-canvas background coverage, preference persistence and pause restoration across ten trial contexts. Fixture brightness is sampled over three simulated minutes per trial context. The original scene and preference values were restored and the temporary fixture was removed. No real account, wallet, sign-out or scene navigation operation was performed. See `PlayModeChecks.txt` for the results.

`Trial1LightingBefore.png` and `Trial1LightingAfter.png` render the same saved chamber using the earlier vertex-light profile and the corrected pixel-light profile. Editor placeholder door text has not been populated by gameplay in those renders. They are visual comparisons, not Android performance benchmarks.

Physical Android performance has not been measured. The existing Unity Remote requirements check fails in the Editor; the offline checks do not rely on Unity Remote.
