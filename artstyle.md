# Logic Legends — UI art style and implementation guide

Original audit: 2026-10-06. Strict visual-matching revision: 2026-10-07. This file is the reusable design reference for new gameplay, UI, button variants, and UI reskins in Logic Legends. Read it before designing or implementing a screen. All asset paths are relative to the LogicLegends project root, even when this document is supplied from another folder.

## 1. Design identity

Logic Legends is a friendly fantasy learning adventure. Its interface combines illustrated wooden signs, parchment scrolls, books, gold trim, bold condensed lettering, and simple recognizable icons. The world uses stylized low-poly gardens, ruins, stone chambers, magical books, crystals, and portals. UI should belong to that world while keeping logic questions and controls easy to read.

The **original illustrated assets are the visual authority**. Gameplay colors and layouts may vary by activity, but their variation does not authorize a different material, rendering style, or ornamental treatment.

1. **Illustrated menus and exploration UI:** warm wood and parchment, thick beveled frames, colorful inset buttons, cream/white lettering, and pictorial icons. Use for menus, settings, inventory/shop, achievements, objectives, and book-like information.
2. **Gameplay puzzle content:** deep teal/blue-green surfaces, subdued teal controls, warm gold headings and dividers, pale text, and clear rectangular work areas. These define a content palette and functional layout. When adding artwork to them, use the original illustrated frame, bevel, shading, and texture treatment described below.

These treatments share the original project's illustrated visual language. Choose wood, parchment, a colored button face, or a calm puzzle interior according to function. Match appearance to a named original reference asset; a generic fantasy description or a matching palette alone is insufficient.

**For a new gameplay mode:** retain its appropriate readable content palette, derive decorated panels and actions from the original reference assets, and reuse established exploration controls. Use the book-reader treatment for extended lessons. A teal button may have a teal face; its brown frame must still match the source button.

**Strict matching rule:** for color/text variants, change only the requested face color, label, or icon. Preserve source shape, frame material and hue, edge thickness, bevel profile, highlight width/direction, shadow, texture density, and visual simplicity. A new panel may require different dimensions and content organization, but its frame and surface rendering must remain consistent with the source. Preserve original assets and create separate derivatives as needed.

### Reference priority and style boundaries

| Component | Primary original reference | Features to retain |
| --- | --- | --- |
| Capsule action | `Assets/Interface_Sprites/main_menu_interface/btn_play.png` | Brown illustrated rim, restrained layered bevel, smooth colored inset, broad upper highlight, darker lower edge. |
| Rectangular action | `Assets/Interface_Sprites/play_interface/btn_solo.png` | Existing rectangular silhouette and layered brown border; use the source's proportions and lighting. |
| Framed panel | `Assets/Interface_Sprites/settings_interface/settings_background.png` | Warm brown wood, broad painted grain, carved edge shapes and modest corner details. |
| Reading/objectives panel | `Assets/Interface_Sprites/customizationsh_shop_interface/customization_scroll.png` or `shop_scroll.png` | Cream paper, brown edges, rolled ends and soft painted shading. |
| Small navigation action | `Assets/Interface_Sprites/back_btn/btn_back.png` or `btn_settings.png` | Chunky square wooden surround, illustrated icon and existing depth treatment. |
| Gameplay touch control | `Assets/Assets/Interface_Sprites/gameplay_interface/` | Existing white symbols and circular silhouettes; retain their intentionally simple treatment. |

Use the exact original sprite wherever it already satisfies the task. When a blank or recolored derivative is needed, supply the actual original as its reference. Match unchanged regions directly; adapting the surface color must not transform wood into stone or brown trim into reflective gold.

Do not introduce cracked marble/slate, hammered metal, polished gold rims, jewel-like surfaces, extra ornamental corners, bloom, or stronger specular shine unless the selected original already contains that treatment or the user explicitly requests a redesign. Existing gold artwork, such as branding and achievement icons, remains valid in its original role; it is not a reason to add gold frames everywhere.

The newer `Assets/Resources/StageUI/TealPanel.png` and `TealButton.png` fit the gameplay palette but introduce stronger stone texture and metallic gold than the original panel/button references. Their presence in the project, and claims in `StageUI/ARTWORK.md`, do not make that rendering treatment the matching target. For strict consistency work, compare against the original sprites above. Revising this document alone does not reskin those assets or their screens.

## 2. Audit scope and confidence

This guide is based on visual inspection of all **73 PNG sprites** under `Assets/Interface_Sprites/` and `Assets/Assets/Interface_Sprites/`, inspection of serialized UI in all **12 scenes** directly under `Assets/Scenes/`, relevant UI prefabs, TMP font assets/settings, and runtime/editor UI-building code. It also uses the documented Sets environment art direction.

This was a source-and-asset audit, not a Play Mode visual test of every screen. Serialized inactive objects, legacy panels, prefab overrides, and runtime-created UI can differ from what a player sees. No gameplay was changed. Values described as recommendations below are design guidance, not claims that a centralized theme system already implements them.

The 2026-10-07 review additionally inspected the newer StageUI sprites, their theme code, and saved screenshots under `Temp/StageUIQA/`. Those screenshots are supporting evidence, not a fresh runtime test. Palette/font compatibility was observed, while stone and shiny gold material choices differed from the original illustrations. Original audit measurements and scene descriptions below are historical references; verify current assignments before implementation.

Use the root Unity project (`Assets/`, `Packages/`, `ProjectSettings/`). The nested `My project (1)/`, vendor demo scenes, generated caches, and `Library/` are not the design authority. The current build settings enable `Main Menu`, PRELIM, RulesOfInference, older stages, and LogicGarden; `Sets.unity` exists but is not currently listed there. Do not infer production status from a scene filename alone.

## 3. Screen families and reference locations

| Surface | Existing design and reuse target |
| --- | --- |
| Main menu and stage selection | `Assets/Scenes/Main Menu.unity`; branded blue-and-gold banner, illustrated colored buttons; stage locks use gold text. `Assets/Scripts/MainMenuManager.cs` switches login, signup, play, shop, customization, achievements, settings, and stage-selection panels. |
| Login/signup/password reset | Same scene; framed parchment form, muted inset input backgrounds, green login, blue signup, red logout; white eye icons for password visibility. |
| Play/mode selection | Blue Solo, green Multiplayer, red PvP, burgundy Logic Garden. There are both icon-bearing and text-only sprite variants; follow the variant actually referenced by the neighboring screen. |
| Shop/customization | Cream parchment scrolls or wood scroll panel, light item cards, coin/gem counters, green Buy/Select controls and gray purchased/selected variants; purple Clothes and green Pets tabs. |
| Achievements | Parchment body inside a wooden frame and header; pale horizontal entries and green Claim buttons. |
| Settings/back navigation | Wooden panel and banner, brown slider trough, amber fill, wooden handle, gold speaker; square wooden back/settings icons. Exit/return actions use red buttons. |
| Exploration HUD/tutorial/objectives | `TUTORIAL.unity`, `MAP1_LEVEL1.unity`, `Stage 1.unity`, `LogicGarden.unity`, and gameplay canvases; red hearts, white circular touch controls, parchment objectives, gold exclamation marker and eye toggles. |
| Dialogue and stage results | Existing scene panels and `DialogueManager`, `GameOverManager`, `StageCompleteManager`. Reuse the host screen's treatment and manager flow. Dialogue can hide gameplay controls; result screens restore/hide controls through existing managers. |
| Propositional logic and truth tables | `Assets/Scenes/PRELIM.unity`; dark teal tutorial card/overlays, gold divider/primary tutorial action, muted teal choices, white/cream question text, outlined HUD labels and teal-framed minimap. |
| Rules of Inference | `Assets/Scenes/RulesOfInference.unity`, `InferenceBoardUI.cs`, `InferenceChallenge.cs`, `RulesOfInferenceBoard.cs`; deep teal board, gold heading, pale text, teal word tiles, distinct blanks and a staged conclusion reveal. |
| Logic Garden study reader | `Assets/Scripts/Scripts/LogicGarden/StudyLibraryController.cs`; brown book cover, two cream pages, dark ink, central spine, gold title, bottom navigation, dark backdrop. Lesson PNGs are content, not examples of the game's UI chrome. |
| Sets | `Assets/Scenes/Sets.unity`, `Assets/Scripts/Scripts/Sets/SetsUIController.cs`; runtime overlay with challenge/set information, top-right timer, bottom prompt, Place action and result panels. Its simpler navy/teal/red/green panels are an existing variation, not a new global theme. |
| Multiplayer | `Stage 1 Multiplayer.unity`, `Stage 2 Multiplayer.unity`; retain the same visual family as solo and preserve network-aware manager behavior. |
| Cinematics | `CINEMATICS.unity` uses a separate background asset; it is not the reference for interactive puzzle layouts. |

## 4. Shapes, materials, and artwork

### Illustrated family

- Wood has visible grain, warm brown edges, small highlights, a dark outer rim, and slight irregularity. Scrolls have rolled ends, cream centers, browned edges, and subtle shading.
- Primary menu buttons are long rounded capsules with a dark brown/bronze frame, a bright inset face, a highlight along the top, and a darker lower edge. Mode buttons are more rectangular but use the same layered border treatment.
- Match the actual brown rim in the original button. "Bronze" describes its warm brown appearance, not permission to add metallic flakes, bright gold reflections, or a new metal material. Preserve the source's smooth face rather than adding stone cracks.
- Titles and button labels are usually bold, condensed, uppercase, cream or white, with dark edging/shadow. Many are **already painted into the PNG**. Do not place duplicate text over them.
- Icons are chunky and readable: gear, money bag, shirt, paw, crossed swords, bulb, laurel medal, coin, gem. Preserve each existing asset's rendering rather than mixing unrelated icon libraries.
- The illustrated moonlit forest background adds blue/teal atmosphere. Keep foreground panels legible against it; do not put long text directly over detailed scenery.

### Gameplay board family

- Use large calm dark surfaces and a small number of functional subdivisions. Gold marks hierarchy; teal marks workspaces and controls.
- Teal is a fill/palette choice. Decorated puzzle panels must reuse or faithfully adapt original wood frames; a teal fill does not require a stone slab or metal frame. Keep gold primarily in the existing text/divider/icon roles unless the reference component itself has gold decoration.
- Keep questions, premises, truth-table cells, answer choices, and feedback visually separate. Match the board next to the new screen before choosing dimensions.
- World labels and puzzle symbols must remain readable against the 3D scene. Existing HUD labels use outlines; the truth-table clock uses black alpha 230/255 and outline width `0.16`.
- Keep the central gameplay view open. Ornamental artwork should not compete with draggable words, diagrams, carried objects, or placement targets.

## 5. Color reference

The following values were captured from code or serialized scene fields during the original audit. Hex values are rounded RGB equivalents for communication; Unity RGBA float values are the implementation reference for that recorded treatment. These descriptive names are **not a mapping to existing theme constants**. Choose the appropriate content colors without changing the source frame material or shading.

| Role | Unity RGBA | Approx. RGB hex | Source |
| --- | --- | --- | --- |
| Inference board | `(0.035, 0.085, 0.11, 1)` | `#09161C` | PRELIM / RulesOfInference, `InferenceSetup.cs` |
| Argument workspace | `(0.08, 0.14, 0.16, 1)` | `#142429` | RulesOfInference, `InferenceDragSetup.cs` |
| Puzzle button / word tile | `(0.16, 0.31, 0.33, 1)` | `#294F54` | Inference setup / `RulesOfInferenceBoard.cs` |
| Inference field | `(0.12, 0.20, 0.23, 1)` | `#1F333B` | `InferenceBoardUI.cs` |
| Inference heading gold | `(0.89, 0.76, 0.46, 1)` | `#E3C275` | `InferenceSetup.cs` |
| Inference body text | `(0.91, 0.95, 0.93, 1)` | `#E8F2ED` | `InferenceBoardUI.cs` |
| Input placeholder | `(0.60, 0.72, 0.74, 1)` | `#99B8BD` | `InferenceBoardUI.cs` |
| Tutorial card | `(0.065, 0.14, 0.16, 0.98)` | `#112429` | PRELIM `TutorialCard` |
| Tutorial dimmer | `(0.015, 0.035, 0.04, 0.91)` | `#04090A` | PRELIM `PropositionalTutorialOverlay` |
| Tutorial primary action | `(0.56, 0.39, 0.15, 1)` | `#8F6326` | PRELIM `BeginButton` |
| Question choices | `(0.18, 0.30, 0.32, 1)` | `#2E4D52` | PRELIM answer buttons |
| Minimap frame | `(0.22, 0.66, 0.68, 0.95)` | `#38A8AD` | PRELIM / RulesOfInference |
| Reader cover | `(0.23, 0.105, 0.045, 1)` | `#3B1B0B` | `StudyLibraryController.cs` |
| Reader paper | `(0.94, 0.87, 0.70, 1)` | `#F0DEB3` | Same |
| Reader ink | `(0.16, 0.10, 0.06, 1)` | `#291A0F` | Same |
| Reader title | `(1, 0.90, 0.62, 1)` | `#FFE69E` | Same |
| Reader diagram accent | `(0.18, 0.36, 0.34, 1)` | `#2E5C57` | Same |

Menu color meanings are encoded in painted sprites, including gradients and borders. Reuse the sprites instead of replacing them with guessed flat hex fills:

| Color | Existing uses |
| --- | --- |
| Blue | Play, Solo, Signup |
| Green | Multiplayer, Pets, Login, Buy, Select, Claim |
| Red | PvP, Logout, Quit, Return to Main Menu |
| Burgundy | Logic Garden |
| Purple | Customization, Clothes |
| Gold/yellow | Achievements, currency, headings and emphasis |
| Orange | Settings, Top-up |
| Gray | Shop category button **and**, separately, purchased/selected action variants; gray alone does not imply disabled everywhere |

Feedback should include text or an icon, not rely only on color. Truth-table answer feedback uses green `(0.25, 0.95, 0.35)` and red `(1, 0.27, 0.27)`. Sets feedback backgrounds use neutral `(0.04, 0.09, 0.15, 0.90)`, correct `(0.08, 0.42, 0.18, 0.95)`, and wrong `(0.55, 0.08, 0.08, 0.95)`. Preserve the chosen screen family's semantics; these are separate implementations, not a universal shared palette.

## 6. Typography

Font assets live under `Assets/TextMesh Pro/Resources/Fonts & Materials/`.

- **`IMPACT SDF.asset`:** the actual project-wide TMP default in `TMP Settings.asset`; explicitly used throughout Main Menu, PRELIM, and several stage screens. Its bold condensed appearance is suitable for short action labels and HUD headings.
- **`LiberationSans SDF.asset`:** used in menu text and inference UI; the study reader explicitly assigns it for readable lesson content. Prefer it for long explanations, premises, form text, and dense reading.
- **`Fredoka-VariableFont_wdth,wght SDF.asset`:** present, with one direct font assignment in PRELIM. It is not the global font. Do not assume all new UI should use Fredoka.
- **`JazzCreateBubble SDF.asset` / `BubbleTitleTMP.asset`:** available decorative assets, not a mandate to introduce bubble lettering to every screen.
- **`LogicFallback_SDF.asset` and `NotoSansMath-Regular.ttf`:** available resources for symbols. Their presence does not mean every text component has a valid fallback chain. TMP's global fallback list is empty in the inspected settings.

**Implementation rule:** assign a font explicitly, or copy the adjacent screen's font and material deliberately. Calling `TMP_Settings.defaultFontAsset` currently selects IMPACT, including in Sets' generated HUD. Do not silently use that default for a long lesson or a new mathematical notation panel.

Check the actual glyphs needed by the activity: `¬ ∧ ∨ → ↔ ∀ ∃ ∈ ∉ ∅ ∪ ∩ ⊆` and any subscripts. Add a suitable per-font fallback only when needed; test the rendered output. Do not alter global fonts just to fix one label.

Observed local size examples (Unity font-size units, **not universal pixel sizes**):

| Context | Existing sizes |
| --- | --- |
| Inference | Heading 40; instructions 27–30; premises 30; actions 25; word tiles 24–30 |
| PRELIM quiz | Question 40; choices 45; feedback 46; submit 34 |
| Sets HUD | Challenge 31; set definitions 27; instruction 29; prompt/button 28; timer 34; results 48–52 |
| Study reader | Title 44; body initially 31 with autosizing range 22–34; hint 20; navigation labels 31 |

World-space labels use different scales. Existing scenes also contain very small serialized font sizes and scaled transforms. Copying a number without its RectTransform, canvas, and world scale will not reproduce its visual size. For new screens, use a clear heading/body/action hierarchy and inspect at the final display size.

## 7. Layout and interaction

The project uses **Unity uGUI**: Canvas, RectTransform, Image/RawImage, Button, Slider, ScrollRect/layout components, TMP text, and EventSystem input. Follow that architecture for additions.

- `Main Menu.unity` uses a 1920 × 1080 reference resolution with width/height match `0.5`. Sets uses the same values in code. Several older exploration canvases use 1920 × 1080 with match `1`; other/world-space canvases serialize 800 × 600 defaults. There is no single scaler setting applied everywhere.
- **Recommended for a new standalone screen-space HUD:** 1920 × 1080, Scale With Screen Size, match `0.5`, unless matching an existing host canvas. Extend an existing canvas when practical instead of creating overlapping independent HUDs.
- Anchor persistent controls to the edges and keep the center available for the world/puzzle. Preserve movement/look/interact/jump areas when adding a new action. Match the actual host scene's placement, not just a generic prefab name.
- Sets demonstrates a top-right timer, challenge/set information toward the upper-left, top-center instruction, lower-center prompt, and right-side Place action. These are a layout reference, not coordinates to copy blindly.
- Truth-table clock labels are placed below the existing minimap with a 12-unit gap and adapt to its width. Do not overlap the minimap or reuse fixed offsets without checking its bounds.
- The study reader's generated book is 1580 × 880 on its reference canvas, with two pages, a narrow spine, title at the top, and navigation at the bottom. Keep that book metaphor for lesson reading.
- Use layout groups/content sizing for variable text and word banks. Allow wrapping or an intentional scroll area; do not shrink long questions until unreadable.
- Recommended new-layout spacing: start with 24–32 reference units of panel padding and 12–16 units between related controls; adjust to the reference screen. These are proposed defaults, not audited project-wide constants.
- Check phone safe areas and wide/narrow landscape sizes. `Assets/CrystalFramework/Utility/SafeArea.cs` is available; verify its actual integration rather than assuming every canvas uses it.

Existing touch controls intentionally use simple white symbols with dark/translucent circular backgrounds, unlike the illustrated menu buttons. Reuse them. Noninteractive ornament/text should not intercept pointer events; interactive drag/drop surfaces must receive the expected raycasts. Ensure there is a compatible EventSystem/input module; do not create duplicates.

Show relevant states: idle, hover/focus where applicable, pressed, selected, locked/disabled, correct, incorrect, and completed. Prefer existing sprite variants or the host Button's transition setup. The study reader has its own highlight/pressed/disabled ColorBlock logic; this is not a global transition standard.

Use concise action labels and friendly instruction sentences. Never reveal the answer through decoration: Rules of Inference deliberately keeps physical rule choices visually equivalent and conceals the conclusion until validation. Apply the same principle to new puzzles.

## 8. Asset reuse map

Paths and even misspellings below are the actual repository names. Preserve them when referencing existing assets.

| Need | Asset folder / example |
| --- | --- |
| Menu capsules | `Assets/Interface_Sprites/main_menu_interface/` — `btn_play.png`, `btn_settings.png`, `btn_customization.png`, `btn_achievements.png`, `btn_logic_garden.png`, `btn_shop.png` |
| Mode buttons | `Assets/Interface_Sprites/play_interface/`; alternate versions under `Assets/Assets/Interface_Sprites/play_interface/` including `btn_logic_graden.png` |
| Parchment and item cards | `Assets/Interface_Sprites/customizationsh_shop_interface/` — `shop_scroll.png`, `customization_scroll.png`, `item_container.png` |
| Wood shop panel, currency, action states | `Assets/Assets/Interface_Sprites/customizationsh_shop_interface/` — `shop_scroll2.png`, `coins_gems/`, `buy_btn/`, `select_btn/` |
| Settings | `Assets/Interface_Sprites/settings_interface/` — `settings_background.png`, `settings_banner.png`, `music_slider_background.png`, `fill_background.png`, `handle.png`, `music_icon.png` |
| Navigation icons | `Assets/Interface_Sprites/back_btn/` — `btn_back.png`, `btn_settings.png` |
| Authentication | `Assets/Assets/Interface_Sprites/login_interface/` and `password_icon/` |
| Achievements | `Assets/Assets/Interface_Sprites/achievements_interface/` |
| Branding/background | `Assets/Assets/Interface_Sprites/background/` — `LL_banner(2).png`, `banner_v2.png`, `background(2).png`, `LL_logo_transparent.png` |
| HUD controls and health | `Assets/Assets/Interface_Sprites/gameplay_interface/` — `interact_btn.png`, `jump_btn.png`, joystick variants, `heart.png`, `hearts.png` |
| Objective/tutorial cues | `Assets/Assets/Interface_Sprites/Tutorial/` — `objectives_ongoing.png`, `task_toggle_on_btn.png`, `task_toggle_off_btn.png`, `Pointer.png`, `hand_pinch.png` |

### Import and composition rules

1. Reuse the exact sprite/sub-sprite referenced by the closest matching scene. Some textures use multiple-sprite import and non-default sprite file IDs; a PNG filename alone does not identify the selected sub-sprite.
2. Preserve transparency, aspect ratio, border thickness, icon proportions, and the painted light/shadow. Use white Image tint for full-color art unless the reference intentionally tints it.
3. Check sprite border metadata before using Image Type Sliced. For example, `settings_background.png.meta` has zero borders; simply choosing Sliced will not create working nine-slice margins. Use the native proportions or author suitable borders deliberately.
4. Keep text inside the usable center of a frame, away from scroll rolls and wood trim. Use a blank background plus TMP for new wording. For a pre-labeled sprite, create a clean text-free derivative before adding a new label; do not cover the old lettering with a flat patch or stretch it to fit.
5. If a new illustrated asset is necessary, match the existing frame, shading, typography, and color role. Keep it transparent outside its silhouette and retain an editable source where practical.
6. Preserve Unity `.meta` files and references. Avoid replacing a shared sprite/font/material when only one new screen needs a variation.

### Creating matching button variants

The goal is a faithful variant of the original component. Preserve the reference's silhouette, corner shape, border thickness, brown wood/frame material, bevel, highlight direction and intensity, shadow, texture density, and icon proportions. Reuse original pixels/artwork wherever possible. A generated image edit is a candidate that must pass comparison; it does not guarantee preservation. Reject or repair candidates that change the frame material, add texture, or increase shine even if the palette looks suitable.

| Starting point | Required approach |
| --- | --- |
| Existing button with a separate TMP label | Reuse its background and change the label, retaining font/material, padding, alignment, and hierarchy. |
| Existing sprite with baked-in text | Inspect the actual sprite, create a separate text-free version that restores the underlying face, and use a TMP child for the replacement label. Preserve the original asset. |
| Existing colored face separate from frame | Change the face color only; preserve frame, highlights, shadow, icon, and text colors. |
| Single full-color sprite containing face and frame | Prefer an existing suitable variant or derive a new face-color variant from that exact reference. Whole-image tint multiplies all colors and can muddy wood, gold, and lettering; it is not a reliable way to change only the face. |
| New action without a suitable asset | Derive a blank button from the closest existing family, then add editable text and an optional separate icon. Do not invent a different border or illustration style. |

**Preferred reusable button structure for new work:** one Button root with a stable hit area, a background face Image, a separate frame/highlight layer where practical, an optional icon Image, and a TMP label. This is a proposed component structure, not an existing prefab. Decorative children should not block raycasts. The state transition must target the intended face/graphic rather than unintentionally recoloring the entire composition.

- Example: green `CONTINUE` and teal `CHECK ANSWER` variants of the Play button retain the original brown rim, broad highlight, smooth face, and shadow while changing the inset color and using separate cream IMPACT labels. Neither variant acquires a gold rim or stone texture.
- Use color roles in section 5 unless the user explicitly specifies another color. Retain readable label contrast in normal, pressed, focused, selected, and disabled states.
- Keep normal/pressed/disabled variants aligned to identical bounds and pivots so they do not jump during interaction. Allow long labels by resizing a properly sliced blank background or adapting layout, not distorting the frame or shrinking text excessively.
- Use the available image-generation/editing tool for new illustrated raster artwork or edits, supplying the actual project sprite as a reference. Inspect local references first. Request transparent surroundings and no baked text for reusable backgrounds. If image editing is unavailable, reuse suitable existing artwork and identify any unfinished asset work rather than claiming a generated replacement exists.
- Record each new reusable asset's source reference, dimensions, intended size range, border/slicing settings, supported states, and prefab usage after creating it. Add its actual path to this guide only once it exists.

For raster edits, describe the limited change precisely: "Use this original project button. Preserve the brown frame, silhouette, bevel, highlights, shadows, texture detail and lighting. Remove the baked label cleanly and change only the inset face to [color]. Keep the face smooth. Transparent surroundings; no text, extra trim, stone texture or metallic gold." Provide the original image as the reference, rather than relying on this wording alone. Keep exact labels in TMP. If the result fails the strict comparison, revise it or reuse an appropriate existing asset and report the remaining limitation.

### Replacing plain UI with finished themed components

When asked to style a screen, complete its visual treatment within the requested scope. Changing only the background color is insufficient for a placeholder panel or button that needs a design. Keep information surfaces calm and preserve intentional simplicity in touch controls, dimmers, diagram lines, and table cells.

| Plain element | Matching treatment |
| --- | --- |
| Menu/objectives/settings panel | Existing wood frame or parchment treatment, appropriate header, inset content area, and consistent padding. |
| Puzzle/question panel | Existing illustrated wood frame or a faithful derivative around a calm deep teal content inset, gold heading/divider, and readable pale text. Preserve the original frame material and painted detail; keep the content inset smooth and quiet. |
| Action rectangle | Blank themed button with a beveled face, consistent frame, editable label, and interaction states. |
| Timer/challenge counter | Compact framed plaque that respects the minimap and gameplay view. Match the host HUD's typography. |
| Correct/incorrect message | Small themed inset or plaque with readable feedback text and a status cue; preserve the existing green/red meanings. |
| Result modal | Host-family frame, clear result heading, readable summary, and matching Retry/Continue/Return buttons. |

Treat this as a visual reskin unless behavior changes are requested. Preserve Button callbacks, object/script references, input fields, drag/drop targets, scroll/mask behavior, navigation, timer/progression rules, and modal visibility. Update runtime builders as well as scene components where needed so the styling survives entering Play Mode. Adding decorative borders may require more content padding; verify that the usable area and touch targets remain adequate.

Compare before/after at the same resolution and compare new components beside their original source reference at the same visible size. Compare texture density, frame hue/material, corner silhouette, bevel, shine, shadow and label treatment independently of the changed color or wording. Check actual Unity rendering, including transparency edges, frame stretching, label fit, selected/disabled states, and touch/drag interaction. Do not restyle unrelated scenes solely because this guide allows themed replacements.

## 9. How UI is created in this project

Appearance is distributed across the following layers. Newer code adds `StageUITheme`, but it is not a universal theme for all project UI, and its current generated artwork is not the original visual authority:

- **Scene-authored UI:** serialized canvases, image sprites, font references, anchors, colors, and button callbacks in `Assets/Scenes/*.unity`.
- **Prefab-authored UI:** reusable objects under `Assets/Prefabs/`, including `PREFABS/WORLD_UI/`. Inspect before reuse: `Canvas.prefab` contains older legacy controls, and `MobileInputUI.prefab` is a controller object with unassigned joystick references, not a complete ready-styled HUD.
- **Runtime UI builders:** `SetsUIController.BuildHudIfNeeded()`, `StudyLibraryController`, and `TruthTableStageClock` create or extend UI in C#. Changes must be made where these elements are created or they can be overwritten on startup.
- **New shared gameplay presentation:** `Assets/Scripts/Scripts/GameManager/StageUITheme.cs` assigns sprites, fonts and states; `StageJourneyUI.cs` creates HUD, tutorial and result components. These currently use the generated assets under `Assets/Resources/StageUI/`. Inspect their assignments when reskinning those screens; do not assume updating a scene Image alone will survive runtime setup. Preserve useful font/layout/behavior choices while evaluating artwork against the original references.
- **Editor authoring helpers:** `Assets/Editor/InferenceSetup.cs`, `InferenceDragSetup.cs`, and `ConfigureSetsStage.cs` build/configure scene content. Read their scope before running them. They are not interchangeable theme installers.
- **Behavior managers:** dialogue, objectives, game-over, stage-completion, menu navigation, input, and puzzle managers handle visibility and progression. Connect the new UI to those flows rather than reproducing them as unrelated buttons.

For new gameplay:

1. Read this guide, inspect the original reference asset for each component, and inspect the host scene plus its prefab/runtime builder.
2. Choose the content palette and list the original art/font assets to reuse. Record which features may change and which source features must be preserved.
3. Identify which layer owns the UI: scene, prefab, runtime builder, or editor setup. Implement in that layer.
4. Build the challenge layout, instruction, progress/timer if required, interaction affordance, feedback, and completion/retry/return states appropriate to the requested gameplay.
5. Assign fonts and colors explicitly; preserve the host canvas/input conventions. Prefer small reusable components or local style constants when useful, without silently redesigning existing screens.
6. Verify the result in Play Mode and at target resolutions. Update this guide only for intentional reusable design decisions, labeling new conventions clearly.

## 10. Known inconsistencies to avoid propagating

- Older exploration scenes reference font GUID `b86979d84307d064da1c84f6fcc3eb3e`, which was not resolved to an asset in this audit. Do not copy that reference blindly; inspect the rendered scene and assign an existing font deliberately.
- Some prefabs and scene objects contain legacy/default gray controls, placeholder text, or generic object names. Their existence does not make them the desired look for a new screen.
- Sets creates its own flat-color HUD and assigns the TMP default. Although the fields are headed "Optional Scene UI Overrides", `BuildHudIfNeeded()` currently creates and assigns new elements; do not assume assigning those fields preserves a custom scene HUD.
- There are duplicate sprite variants in two interface folders, mixed canvas scale settings, and different fonts for display versus reading. Check actual references rather than selecting the first matching filename.
- Gameplay palettes and original illustrated artwork serve different purposes: the palette can vary, while frame materials and rendering must stay consistent with the selected original reference. A newer generated screen is not automatically an approved style reference.
- The current ornate StageUI sprites should not be copied as the baseline for strict matching. Check their brown-frame alternatives or create faithful derivatives when a styling task includes them. This document does not authorize an unrelated full-project reskin.

## 11. Acceptance checklist for new gameplay UI

- [ ] Names the original source asset for every new or reskinned component; style is judged against that original, not a generated derivative.
- [ ] Preserves source silhouette, frame material/hue, border thickness, bevel profile, highlight intensity/direction, shadow and texture density.
- [ ] A requested color/text variant changes only its permitted face, label or icon; no added stone, reflective gold or ornament absent from the source.
- [ ] Compared beside the original at the same visible size; differences beyond the intended edits have been corrected or explicitly reported.
- [ ] Reuses existing artwork and appropriate fonts; no duplicate baked labels.
- [ ] Correct sprite/sub-sprite references; artwork and frames are not distorted.
- [ ] New button variants preserve the reference frame/shading and use independently editable labels; recoloring does not muddy the frame or icons.
- [ ] Requested placeholder replacements have a complete themed treatment, with restrained decoration around readable content.
- [ ] Reskinned components preserve callbacks, references, hit areas, and runtime-generated styling; before/after appearance and interaction have been compared where available.
- [ ] Logical/mathematical glyphs render correctly and long text remains readable.
- [ ] Normal, interaction, disabled/locked, feedback, and result states are understandable.
- [ ] Timer, minimap, objectives, movement, look, jump, and interaction controls do not overlap.
- [ ] Touch targets work at the target device size; layout respects safe areas.
- [ ] Decorative graphics do not block clicks, taps, or dragging.
- [ ] Modal UI, dialogue, retry, return, and completion work with existing managers.
- [ ] No visual cue reveals an answer before the gameplay permits it.
- [ ] Compared in Play Mode with the reference at 1920 × 1080 and relevant phone/tablet aspect ratios; report any checks that could not be run.

## 12. Reusable Codex prompt

Copy the following prompt and replace the bracketed gameplay description. Attach this file when the repository is not already available.

```text
Implement [describe the new gameplay, scene, rules, and required UI] in Logic Legends.

Before making changes, read the supplied artstyle.md or the copy in the project root. Treat the original project sprites listed in its reference-priority table as the visual authority. Inspect those images and the host scene's UI code/prefabs. Use the original wood/parchment illustration style, the appropriate gameplay content palette, and the existing book treatment for lessons.

Reuse the project's sprites, fonts, controls, layout conventions, and managers. Follow artstyle.md for exact asset paths and colors. Assign fonts explicitly, verify mathematical glyphs, preserve sprite proportions, avoid duplicate baked labels, and keep movement controls, minimap, timer, and gameplay visible. Include the interaction, feedback, and result states this gameplay needs. Distinguish intentional design guidance from legacy/default UI and existing inconsistencies documented in the guide.

For new buttons, preserve the original frame material and hue, silhouette, border thickness, bevel, highlight width/direction/intensity, shadow and texture density. Change only the requested face color, label or icon. Prefer the exact existing artwork or a clean blank derivative with separate editable TMP text. A teal face must retain the original brown rim and smooth painted surface. Do not add stone cracks, hammered metal, reflective gold, ornament or stronger shine absent from the original. Existing ornate StageUI derivatives are not the matching baseline.

Within the requested scope, replace unfinished panels/buttons with artwork reused or faithfully derived from the original assets. Keep puzzle content uncluttered and preserve intentional simple controls. When new raster artwork is needed, provide the actual original asset to the image-editing tool, request only the required changes, and compare the candidate beside the original at the same visible size. Revise material/shading/texture drift before accepting it. Matching colors alone is insufficient. Preserve all gameplay callbacks and input behavior.

Implement the UI in its owning scene, prefab, or runtime builder. Preserve unrelated work. Validate the result against the reference in Play Mode and relevant landscape/mobile sizes when available, and state any validation limitations. Do not introduce a new visual theme unless I explicitly request one. Update artstyle.md only if this task intentionally establishes a reusable design convention.
```

This file is the durable reference. Supply it or explicitly ask the next task to read it; the prompt does not depend on memory of an earlier chat.
