# Solo settings and lighting

Scope: Main Menu and the ten solo trials in TrialCatalog. The shared pause owner and stage-selection paths remain in use.

Implemented: saved graphics/FPS/shadow/lighting/fog/motion-blur preferences; horizontal/vertical/zoom sensitivity; touch size/opacity/handedness with mirrored look surface; music/SFX/mute; reading size/contrast/text cues; Apply and Reset. Six requested controls are removed. In-trial Main Menu uses confirmed navigation and keeps the account signed in. Main Menu retains confirmed logout/quit.

Presentation uses runtime copies of three mobile-budget URP lighting presets and selective opaque lit-material fallbacks. Default graphics and lighting are Low at 30 FPS, with real-time shadows and motion blur Off. Forward rendering, 80% world render scale, no SSAO, no requested HDR/depth/opaque texture and no additional-light shadows provide the cheaper baseline. Medium and High remain optional. Authored chamber/sunset ambient lighting and readable/emissive surfaces are preserved. Optional shadow coverage is Off/20/35/55 units with a single cascade. The existing Truth Table player-centered night-visibility effect remains.

Validation: all ten saved trial scenes have valid LevelMenu/pause references. Offline Play Mode checks passed in ten trial contexts, including saved preference reload, real camera yaw/pitch multipliers, mirrored controls/look area, size/opacity, nested pause restoration, native dropdown item selection, mute/FPS, confirmed Main Menu navigation and restoration outside solo. Additional checks cover graphics-only migration, the mobile pipeline budgets, pipeline reuse for audio changes, and the compact confirmation card. Test preferences and scenes were restored afterward. No real account, wallet, sign-out or navigation action was performed.

MainMenu_*.png and Trial_*.png are earlier native Unity renders of the shared settings UI. The latest confirmation, green save status, left-handed controls and dropdown previews are in `output/settings-followup`, alongside the 148 passing follow-up checks. Trial1_Lighting_Before/After.png are earlier comparison renders from before the Android performance revision and are not current mobile profile benchmarks.

Physical Android GPU performance and a complete playthrough of every trial were not measured. The specific reported following-shadow case needs its trial/location to distinguish shadow-distance fade from the Truth Table night mask.

The Editor also reports a Unity Remote ADB forwarding requirement failure. The standalone Editor Play Mode checks passed; physical-device testing was not performed.
