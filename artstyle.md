# Logic Legends — UI art style and implementation guide

Original audit: 2026-10-06. Strict visual-matching revision: 2026-10-07. Graph HUD and current shared-artwork update: 2026-10-11. This file is the reusable design reference for new gameplay, UI, button variants, and UI reskins in Logic Legends. Read it before designing or implementing a screen. All asset paths are relative to the LogicLegends project root, even when this document is supplied from another folder.

**Current design policy (2026-10-12):** reuse GraphHUD artwork across the project for objectives, the current stage/challenge, timers, and other suitable UI. Do not default every panel to the settings background or a rolled parchment scroll. Use the button dimensions in section 7 and always set a button background Image's Pixels Per Unit Multiplier to **4.5**. These rules supersede older local-only Graph guidance and earlier button multipliers. This update changes documentation only; existing scenes, prefabs, and builders may still need these rules applied during an authorized UI task.

## 1. Design identity

Logic Legends is a friendly fantasy learning adventure. Its interface combines illustrated wooden signs, parchment scrolls, books, gold trim, bold condensed lettering, and simple recognizable icons. The world uses stylized low-poly gardens, ruins, stone chambers, magical books, crystals, and portals. UI should belong to that world while keeping logic questions and controls easy to read.

The **original illustrated assets are the visual authority**. Gameplay colors and layouts may vary by activity, but their variation does not authorize a different material, rendering style, or ornamental treatment.

1. **Illustrated menus and exploration UI:** warm wood and parchment, thick beveled frames, colorful inset buttons, cream/white lettering, and pictorial icons. Keep established menus, settings, inventory/shop, achievements, and books in this family. For new objective and status UI, prefer the shared GraphHUD frames below.
2. **Gameplay puzzle content:** deep teal/blue-green surfaces, subdued teal controls, warm gold headings and dividers, pale text, and clear rectangular work areas. These define a content palette and functional layout. When adding artwork to them, use the original illustrated frame, bevel, shading, and texture treatment described below.
3. **Shared GraphHUD frame family:** slim matte wooden frames with clipped corners and cream interiors. Use `GraphObjectiveFrame` for objectives and current challenges, `GraphStatusFrame` for timers and compact stage/status displays, and `GraphMapFrame` for maps and suitable larger cards. These assets are available to any gameplay mode or suitable menu, dialog, feedback, result, or chat UI. Their folder name does not restrict reuse to Graph. Preserve each screen's functional colors and interaction behavior.

These treatments share the original project's illustrated visual language. Choose wood, parchment, a colored button face, or a calm puzzle interior according to function. Match appearance to a named original reference asset; a generic fantasy description or a matching palette alone is insufficient.

**For a new gameplay mode:** start objectives/current challenges with `GraphObjectiveFrame` and timers/stage status with `GraphStatusFrame`; use `GraphMapFrame` where a larger framed surface fits. Retain an appropriate readable content palette inside puzzle workspaces, original brown-rimmed actions, and established exploration controls. Use the book-reader treatment for extended lessons. A teal button may have a teal face; its brown frame must still match the source button.

**Strict matching rule:** for color/text variants, change only the requested face color, label, or icon. Preserve source shape, frame material and hue, edge thickness, bevel profile, highlight width/direction, shadow, texture density, and visual simplicity. A new panel may require different dimensions and content organization, but its frame and surface rendering must remain consistent with the source. Preserve original assets and create separate derivatives as needed.

New component geometry can be designed when explicitly requested, as with the GraphHUD clipped-corner frames. Retain the original material, rendering and lighting while documenting the new silhouette and intended reuse. The existing GraphHUD family is now approved for shared use. This permission does not allow an ordinary recolor/relabel task to change its source geometry.

### Reference priority and style boundaries

| Component | Primary original reference | Features to retain |
| --- | --- | --- |
| Capsule action | `Assets/Interface_Sprites/main_menu_interface/btn_play.png` | Brown illustrated rim, restrained layered bevel, smooth colored inset, broad upper highlight, darker lower edge. |
| Rectangular action | `Assets/Interface_Sprites/play_interface/btn_solo.png` | Existing rectangular silhouette and layered brown border; use the source's proportions and lighting. |
| Settings or established wood-panel screen | `Assets/Interface_Sprites/settings_interface/settings_background.png` | Warm brown wood, broad painted grain, carved edge shapes and modest corner details. Keep its specific role; do not use it as the automatic background for every new UI. |
| Extended reading, shop scroll, or book content | `Assets/Interface_Sprites/customizationsh_shop_interface/customization_scroll.png` or `shop_scroll.png`; existing study-reader artwork | Cream paper, brown edges, rolled ends or book pages, and soft painted shading. Use when the reading/scroll metaphor fits, rather than as the default objective/timer panel. |
| Small navigation action | `Assets/Interface_Sprites/back_btn/btn_back.png` or `btn_settings.png` | Chunky square wooden surround, illustrated icon and existing depth treatment. |
| Gameplay touch control | `Assets/Assets/Interface_Sprites/gameplay_interface/` | Existing white symbols and circular silhouettes; retain their intentionally simple treatment. |
| Shared objective, current challenge, timer, status, map, and suitable other panels | `Assets/Art/GraphHUD/GraphObjectiveFrame.png`, `GraphStatusFrame.png`, `GraphMapFrame.png` | Clipped-corner brown frames, cream interiors, subdued matte bevels and dark editable text; follow section 4. Original settings wood and Play button remain material/rendering references. |

Use the exact original sprite wherever it already satisfies the task. When a blank or recolored derivative is needed, supply the actual original as its reference. Match unchanged regions directly; adapting the surface color must not transform wood into stone or brown trim into reflective gold.

Do not introduce cracked marble/slate, hammered metal, polished gold rims, jewel-like surfaces, extra ornamental corners, bloom, or stronger specular shine unless the selected original already contains that treatment or the user explicitly requests a redesign. Existing gold artwork, such as branding and achievement icons, remains valid in its original role; it is not a reason to add gold frames everywhere.

The historical `Assets/Resources/StageUI/TealPanel.png` and `TealButton.png` introduce stronger stone texture and metallic gold than the original panel/button references. The current `StageUITheme` loads `WoodPanel`, `ReadingPaper`, and brown-rimmed `WoodButtonTeal/Green/Red` assets instead. Reuse those button variants and use the wood/book surfaces where their role fits; do not reintroduce the historical stone/gold sprites. Prefer the shared GraphHUD frames for new objectives, current challenges, timers, and suitable compact panels, even where a builder currently defaults to `WoodPanel` or `ReadingPaper`.

## 2. Audit scope and confidence

This guide is based on visual inspection of all **73 PNG sprites** under `Assets/Interface_Sprites/` and `Assets/Assets/Interface_Sprites/`, inspection of serialized UI in all **12 scenes** directly under `Assets/Scenes/`, relevant UI prefabs, TMP font assets/settings, and runtime/editor UI-building code. It also uses the documented Sets environment art direction.

This was a source-and-asset audit, not a Play Mode visual test of every screen. Serialized inactive objects, legacy panels, prefab overrides, and runtime-created UI can differ from what a player sees. No gameplay was changed. Values described as recommendations below are design guidance, not claims that a centralized theme system already implements them.

The 2026-10-07 review additionally inspected the newer StageUI sprites, their theme code, and saved screenshots under `Temp/StageUIQA/`. Those screenshots are supporting evidence, not a fresh runtime test. Palette/font compatibility was observed, while stone and shiny gold material choices differed from the original illustrations. Original audit measurements and scene descriptions below are historical references; verify current assignments before implementation.

The 2026-10-11 update visually inspected all three GraphHUD PNGs, their `.meta` slicing records, `GraphUIController`, `GraphHUDArtSetup`, the local artwork/prompt records, and the saved `Temp/GraphQA/graph-generated-surfaces-fixed.png` screenshot. It also checked the current shared `StageUITheme` assignments and artwork notes. This update did not run fresh Play Mode or device checks; validation claims in Graph's README belong to that implementation's earlier checks.

The 2026-10-12 policy update checked the current guide, GraphHUD asset records, shared theme and HUD builders, Graph controls, and multiplayer UI helpers against the requested shared-frame and button standards. It establishes future implementation requirements; it does not claim existing buttons already have consistent sizes or multiplier `4.5`, and it includes no new Play Mode validation.

Use the root Unity project (`Assets/`, `Packages/`, `ProjectSettings/`). The nested `My project (1)/`, vendor demo scenes, generated caches, and `Library/` are not the design authority. Check current build settings before inferring a scene's production status. Sets was present in the original audit but `Sets.unity` is absent from the current checkout; its controller remains a historical UI reference.

## 3. Screen families and reference locations

| Surface | Existing design and reuse target |
| --- | --- |
| Main menu and stage selection | `Assets/Scenes/Main Menu.unity`; branded blue-and-gold banner, illustrated colored buttons; stage locks use gold text. `Assets/Scripts/MainMenuManager.cs` switches login, signup, play, shop, customization, achievements, settings, and stage-selection panels. |
| Login/signup/password reset | Same scene; framed parchment form, muted inset input backgrounds, green login, blue signup, red logout; white eye icons for password visibility. |
| Play/mode selection | Blue Solo, green Multiplayer, red PvP, burgundy Logic Garden. There are both icon-bearing and text-only sprite variants; follow the variant actually referenced by the neighboring screen. |
| Shop/customization | Cream parchment scrolls or wood scroll panel, light item cards, coin/gem counters, green Buy/Select controls and gray purchased/selected variants; purple Clothes and green Pets tabs. |
| Achievements | Parchment body inside a wooden frame and header; pale horizontal entries and green Claim buttons. |
| Settings/back navigation | Wooden panel and banner, brown slider trough, amber fill, wooden handle, gold speaker; square wooden back/settings icons. Exit/return actions use red buttons. |
| Exploration HUD/tutorial/objectives | `TUTORIAL.unity`, `MAP1_LEVEL1.unity`, `Stage 1.unity`, `LogicGarden.unity`, and gameplay canvases; existing red hearts, white circular touch controls, parchment objectives, gold exclamation marker and eye toggles. For new or requested restyled objective/challenge/timer UI, use the shared GraphHUD mapping in section 4. |
| Dialogue and stage results | Existing scene panels and `DialogueManager`, `GameOverManager`, `StageCompleteManager`. Reuse the host screen's treatment and manager flow. Dialogue can hide gameplay controls; result screens restore/hide controls through existing managers. |
| Propositional logic and truth tables | `Assets/Scenes/PRELIM.unity`; dark teal tutorial card/overlays, gold divider/primary tutorial action, muted teal choices, white/cream question text, outlined HUD labels and teal-framed minimap. |
| Rules of Inference | `Assets/Scenes/RulesOfInference.unity`, `InferenceBoardUI.cs`, `InferenceChallenge.cs`, `RulesOfInferenceBoard.cs`; deep teal board, gold heading, pale text, teal word tiles, distinct blanks and a staged conclusion reveal. |
| Logic Garden study reader | `Assets/Scripts/Scripts/LogicGarden/StudyLibraryController.cs`; brown book cover, two cream pages, dark ink, central spine, gold title, bottom navigation, dark backdrop. Lesson PNGs are content, not examples of the game's UI chrome. |
| Sets (historical scene) | `Assets/Scripts/Scripts/Sets/SetsUIController.cs`; runtime overlay with challenge/set information, top-right timer, bottom prompt, Place action and result panels. The previously audited Sets scene is absent from this checkout. Its controller's simpler navy/teal/red/green panels are a historical variation, not a new global theme. |
| Graph | `Assets/Scenes/Graph.unity`, `Assets/Scripts/Scripts/Graph/GraphUIController.cs`, and `Assets/Art/GraphHUD/`; cream map/objective/status surfaces in slim brown frames, dark ink, editable diagrams/routes, original touch controls, existing wood-rimmed actions, and book-style field guide. |
| Multiplayer | `Stage 1 Multiplayer.unity`, `Stage 2 Multiplayer.unity`; retain the same visual family as solo and preserve network-aware manager behavior. |
| Private multiplayer UI (2026-10-12) | `Main Menu.unity` lobby uses `GraphMapFrame` for Host/Join choices, the invite-code form, party list and chat, with `GraphStatusFrame` input fields: clipped wooden trim, cream interiors and dark editable text. Brown-rimmed lobby actions follow section 7, with matching Host/Join geometry and multiplier 4.5. The waiting party list and chat are taller, using the former back-button/footer space. The `MultiplayerStage`-only chat HUD uses the same frame family; CHAT is below Settings on the left and opens a dismissible conversation panel while locally blocking movement/look. |
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

### Shared GraphHUD artwork (created 2026-10-11; shared-use policy 2026-10-12)

These picture-frame/plaque silhouettes originated in Graph. Their material references were `settings_background.png` for brown wood and `btn_play.png` for understated painted rendering. They are now shared UI assets: reuse them across gameplay and other suitable screens instead of generating another frame or defaulting every surface to the settings background or a parchment scroll. The cream center is part of the approved artwork; it does not require an additional scroll/paper layer.

| UI purpose in any mode | Preferred existing sprite | Composition |
| --- | --- | --- |
| Objective, task list, current challenge of a stage | `GraphObjectiveFrame.png` | Short heading plus readable dark instruction text; wrap within the clear center. |
| Timer/countdown, current stage, progress/counter, short status or feedback | `GraphStatusFrame.png` | Compact plaque with a concise label/value; timer and stage may use separate matching plaques. |
| Minimap, diagram, larger card, dialog, result, lobby, chat, or another suitable content panel | `GraphMapFrame.png` | Larger framed content area; use its existing slicing and check the frame at the intended aspect ratio. A wide compact card may suit Objective/Status better. |
| Settings, inventory/shop, extended lessons | Existing role-specific wood, scroll, or book artwork; GraphHUD where appropriate | Preserve the established metaphor where useful. Avoid forcing every screen into one large settings panel or scroll. |

Use this mapping for new UI and explicitly requested reskins. It permits reuse in other screens without a separate art exception; it does not request changes to every existing screen whenever a new UI is added. Preserve gameplay palettes in editable diagrams/workspaces, but keep dark ink readable on the GraphHUD cream center.

| Sprite under `Assets/Art/GraphHUD/` | Role in current Graph UI | Recorded sprite region `(x, y, width, height)` | Nine-slice borders, all sides |
| --- | --- | --- | --- |
| `GraphMapFrame.png` | Square map; also selected for other large Graph panels by the local builder | `(75, 73, 1116, 1095)` | `262.8 px` |
| `GraphObjectiveFrame.png` | Compact objective plaque | `(58, 106, 1892, 569)` | `136.56 px` |
| `GraphStatusFrame.png` | Timer, recorded route and feedback plaques | `(87, 143, 1809, 508)` | `121.92 px` |

- **Material and shape:** warm painted brown wood, dark nested rim, simple clipped corners, broad subdued highlights, modest close shadow, and low texture density. The paper is cream with soft shading and an unmarked center. Preserve these qualities wherever these frames are reused. Avoid metal, reflective gold, stone, cracks, ornate corner blocks, glows and rolled scroll ends in these HUD frames.
- **Content stays editable:** PNGs contain only frame/paper artwork. Labels, countdowns, route arrows, graph nodes/edges, player marker, objectives and feedback are Unity UI elements layered inside. Do not bake them into derivatives or use the paper center to reveal answers.
- **Paper and ink:** generation targeted paper `#EFE0BB`; the PNG includes painted shading, so this is a reference color rather than a uniform runtime tint. `GraphUIController` uses ink `#291A0F`, brown map-node fill `#503322`, and cream node labels `#FFF2D4`. Keep white Image tint on the full-color artwork. Use dark ink on paper, rather than carrying pale-on-teal text into this family.
- **Graph meanings:** available edges are orange `(1, 0.35, 0.035)`; walked edges are blue `(0.04, 0.42, 1)`, matching world lighting. The player pointer is green `#16784C`. Keep these functional states in editable overlays. Do not recolor routes merely to match decorative brown trim.
- **Typography:** IMPACT for short headings, timers and actions; Liberation Sans for objectives, route strings and feedback. Route text uses a local reading-font copy with `LogicFallback_SDF` for arrow glyphs. Preserve shared font assets and verify arrows in the rendered result.
- **Current Graph layout reference:** map 270 × 270 at upper-right; timer 270 × 100 below it; left-column objective 360 × 145, route 360 × 60 and feedback 360 × 100. These describe Graph's implementation, not mandatory dimensions for all modes. Field Guide sits beside Settings, and the center remains open. Reuse the artwork with role-appropriate sizes in other screens, preserving safe areas and original touch controls. Button sizes follow section 7.
- **Slicing:** the visual wood rim is slimmer than the numerical slice margins; the latter also protect shaded paper/corners. `GraphHUDArtSetup` records one cropped sub-sprite per PNG and sets each border to 24% of the cropped short side. Do not substitute the generation prompt's approximate 6% visual-border target for these import values. `GraphUIController.Panel()` uses Image Type Sliced and `pixelsPerUnitMultiplier = min(sprite.rect.width, sprite.rect.height) * 0.01`. Keep corresponding crop, borders and sizing together; verify corners at final display size.
- **Buttons and books:** retain shared wood-rimmed button artwork with editable labels and the field guide's book composition. The inspected Graph action code used multiplier `6`; this is a legacy implementation value, superseded by the mandatory button multiplier **4.5** and sizing rules in section 7. Degree choices may keep Simple/aspect-preserved rendering, with the background Image multiplier still set to `4.5`.

Source records: `Assets/Art/GraphHUD/ARTWORK.md` and `generation-prompts.json`. Those records describe creation; actual PNGs, current metadata and runtime assignments determine the delivered result. Prompt aspect-ratio targets are not a substitute for measuring the saved sprite region. Preserve this frame family's appearance across all reuse. Shared use is approved here; changes to shared source artwork still require checking every dependent UI.

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
| Graph chart ink | `(41/255, 26/255, 15/255, 1)` | `#291A0F` | `GraphUIController.cs` |
| Graph map-node fill | `(80/255, 51/255, 34/255, 1)` | `#503322` | Same |
| Graph map-node label | `(1, 242/255, 212/255, 1)` | `#FFF2D4` | Same |
| Graph player pointer | `(22/255, 120/255, 76/255, 1)` | `#16784C` | Same |

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

### Consistent button dimensions and mandatory multiplier

Use the following **new design standards** for text actions across menus, gameplay, dialogs, and results. Dimensions are visible reference-canvas units at **1920 × 1080**, after accounting for parent scale; they are not device pixels or a claim about existing prefabs. On a different host canvas, convert to the equivalent visible size rather than copying unscaled numbers blindly.

| Button role | Width × height | Use |
| --- | --- | --- |
| Standard text action | **280 × 88** | Default for Confirm, Retry, Continue, Host Game, Join Game, and similar actions. |
| Compact text action | **220 × 88** | Short supporting actions such as Back, Help, or Send when space calls for a smaller width. |
| Longer text action | **340 × 88 maximum** | A label that cannot fit the standard width with readable text and padding. Shorten wording or adapt the layout before exceeding this width. |
| Icon-only navigation | **80 × 80** | Back, Settings, close, or similar square icons; preserve the source silhouette. |
| Single-number/symbol puzzle choice | **96 × 72** | Repeated compact answer choices. Keep every choice in that group identical. |

- Use one text-action size per peer group: Host/Join, Retry/Continue, Yes/No, or sibling menu actions must have matching visible dimensions. Use `280 × 88` by default; if one label requires the longer size, use that size for its peers. Color and label changes must not change geometry.
- Keep text actions at height `88`. Do not stretch a button across most of a large card, scale its transform to fit a label, or make every action a different width. Use a centered fixed-size RectTransform or a LayoutElement with explicit preferred size; prevent layout groups or stretch anchors from expanding it to the full card width. The label can stretch inside the fixed button.
- Start with at least `24` horizontal and `10` vertical units of label inset, increasing it where the painted rim requires more room. Use cream/white IMPACT text, normally `22–30` font-size units at this reference scale. Keep short labels on one line; do not compensate for a tiny button by shrinking its text below the readable range.
- At narrower aspect ratios, reflow or stack buttons and preserve safe areas instead of squeezing them into thin strips. Existing movement, joystick, jump, and interaction controls keep their functional shapes and placement; do not convert them into text-action capsules.
- **Always set `UnityEngine.UI.Image.pixelsPerUnitMultiplier = 4.5f` on each button's background Image**, including icon and puzzle-choice buttons. In the Inspector, Pixels Per Unit Multiplier must read **4.5**. Apply the same value to button frame/background Image layers if the component uses separate layers. Keep it through normal, pressed, selected, and disabled artwork swaps, prefab overrides, scene setup, and runtime rebuilds. Do not retain older button values such as `4` or `6`.
- Use Image Type Sliced for a resizable button only when its sprite has valid borders. A Simple/aspect-preserved icon or choice still stores multiplier `4.5`; Simple rendering does not use the multiplier for nine-slicing. Do not invent slice borders or distort a baked-label sprite just to meet dimensions.
- **The multiplier is not the button's width/height, the sprite import Pixels Per Unit, or the CanvasScaler setting.** Preserve the source import PPU (GraphHUD currently uses `100`) and valid borders. Panel Images that are not buttons retain their appropriate slicing multiplier; do not apply the button value to all GraphHUD panels.

The existing shared theme and some local builders still set older multipliers or stretch action buttons. When implementing an authorized UI change, set `4.5` after any helper that would overwrite it, or update the owning shared helper within scope. Inspect the final rendered size and the value after rebuilding the UI. This documentation update alone does not modify those implementations.

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
| Current shared gameplay artwork | `Assets/Resources/StageUI/` — `WoodPanel.png`, `ReadingPaper.png`, `WoodButtonTeal.png`, `WoodButtonGreen.png`, `WoodButtonRed.png`; see that folder's `ARTWORK.md` for current crop and slicing records. |
| Shared objective/challenge, timer/status, map and suitable other surfaces | `Assets/Art/GraphHUD/` — `GraphObjectiveFrame.png`, `GraphStatusFrame.png`, `GraphMapFrame.png`; approved across the project, with roles, colors and slicing in section 4. |

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
- Keep normal/pressed/disabled variants aligned to identical bounds and pivots so they do not jump during interaction. Use section 7's standard dimensions and multiplier `4.5`. For long labels, use the bounded longer size or adapt wording/layout rather than creating an excessively wide action or shrinking text excessively.
- Use the available image-generation/editing tool for new illustrated raster artwork or edits, supplying the actual project sprite as a reference. Inspect local references first. Request transparent surroundings and no baked text for reusable backgrounds. If image editing is unavailable, reuse suitable existing artwork and identify any unfinished asset work rather than claiming a generated replacement exists.
- Record each new reusable asset's source reference, dimensions, intended size range, border/slicing settings, supported states, and prefab usage after creating it. Add its actual path to this guide only once it exists.

For raster edits, describe the limited change precisely: "Use this original project button. Preserve the brown frame, silhouette, bevel, highlights, shadows, texture detail and lighting. Remove the baked label cleanly and change only the inset face to [color]. Keep the face smooth. Transparent surroundings; no text, extra trim, stone texture or metallic gold." Provide the original image as the reference, rather than relying on this wording alone. Keep exact labels in TMP. If the result fails the strict comparison, revise it or reuse an appropriate existing asset and report the remaining limitation.

### Replacing plain UI with finished themed components

When asked to style a screen, complete its visual treatment within the requested scope. Changing only the background color is insufficient for a placeholder panel or button that needs a design. Keep information surfaces calm and preserve intentional simplicity in touch controls, dimmers, diagram lines, and table cells.

| Plain element | Matching treatment |
| --- | --- |
| Objective/current challenge panel | `GraphObjectiveFrame`, editable dark heading/body text, and clear padding inside the cream center. |
| Menu/settings/other content panel | Choose a suitable GraphHUD frame or the established role-specific wood/scroll artwork. Keep headers and padding consistent; avoid automatically reusing the settings background or parchment for every panel. |
| Puzzle/question panel | Existing illustrated wood frame or a faithful derivative around a calm deep teal content inset, gold heading/divider, and readable pale text. Preserve the original frame material and painted detail; keep the content inset smooth and quiet. |
| Action rectangle | Blank brown-rimmed themed button, section 7 dimensions, background Image multiplier `4.5`, editable label, and interaction states. |
| Timer/current stage/challenge counter | `GraphStatusFrame`, dark readable value/text, and placement that respects the minimap and gameplay view. Longer challenge instructions belong in `GraphObjectiveFrame`. |
| Correct/incorrect message | `GraphStatusFrame` or a suitable themed inset with readable feedback text and a status cue; preserve existing green/red meanings without tinting the entire illustrated frame. |
| Result modal | Suitable `GraphMapFrame` or host-family frame, clear result heading, readable summary, and matching standard-size Retry/Continue/Return buttons. |

Treat this as a visual reskin unless behavior changes are requested. Preserve Button callbacks, object/script references, input fields, drag/drop targets, scroll/mask behavior, navigation, timer/progression rules, and modal visibility. Update runtime builders as well as scene components where needed so the styling survives entering Play Mode. Adding decorative borders may require more content padding; verify that the usable area and touch targets remain adequate.

Compare before/after at the same resolution and compare new components beside their original source reference at the same visible size. Compare texture density, frame hue/material, corner silhouette, bevel, shine, shadow and label treatment independently of the changed color or wording. Check actual Unity rendering, including transparency edges, frame stretching, label fit, selected/disabled states, and touch/drag interaction. Do not restyle unrelated scenes solely because this guide allows themed replacements.

## 9. How UI is created in this project

Appearance is distributed across the following layers. Newer code adds `StageUITheme`, but it is not a universal theme for all project UI, and its current generated artwork is not the original visual authority:

- **Scene-authored UI:** serialized canvases, image sprites, font references, anchors, colors, and button callbacks in `Assets/Scenes/*.unity`.
- **Prefab-authored UI:** reusable objects under `Assets/Prefabs/`, including `PREFABS/WORLD_UI/`. Inspect before reuse: `Canvas.prefab` contains older legacy controls, and `MobileInputUI.prefab` is a controller object with unassigned joystick references, not a complete ready-styled HUD.
- **Runtime UI builders:** `SetsUIController.BuildHudIfNeeded()`, `StudyLibraryController`, and `TruthTableStageClock` create or extend UI in C#. Changes must be made where these elements are created or they can be overwritten on startup.
- **Shared gameplay presentation:** `Assets/Scripts/Scripts/GameManager/StageUITheme.cs` assigns the current `WoodPanel`, `ReadingPaper`, and `WoodButtonTeal/Green/Red` resources, fonts and states; `StageJourneyUI.cs` creates HUD, tutorial and result components. Inspect these assignments when reskinning those screens; do not assume updating a scene Image alone will survive runtime setup. Historical TealPanel/TealButton resources are not the loaded style baseline.
- **Graph-local presentation:** `GraphUIController` builds Graph-owned surfaces and editable chart content using `hudMap`, `hudObjective`, and `hudStatus` sprite fields saved in `Graph.unity`. `Assets/Editor/GraphHUDArtSetup.cs` imports these sprites and assigns them only in the Graph scene. Its Apply command saves the scene; do not run it merely to read this guide. Graph's panel builder selects the correct sprite and applies local slicing/multiplier rules. Preserve the connected exploration canvas and callbacks when changing these surfaces.
- **Editor authoring helpers:** `Assets/Editor/InferenceSetup.cs`, `InferenceDragSetup.cs`, and `ConfigureSetsStage.cs` build/configure scene content. Read their scope before running them. They are not interchangeable theme installers.
- **Behavior managers:** dialogue, objectives, game-over, stage-completion, menu navigation, input, and puzzle managers handle visibility and progression. Connect the new UI to those flows rather than reproducing them as unrelated buttons.

**Applying the shared policy in future work:** GraphHUD art may be reused outside Graph, but the current Graph editor command is not a project-wide installer. Supply the sprites through serialized references or an appropriate shared configuration in the UI's owning layer. Assets under `Assets/Art/GraphHUD/` are not automatically loadable with `Resources.Load`; do not assume the folder name is a Resources path or use editor-only asset loading in a runtime build. Reuse existing cropped sub-sprites and metadata. Select the Objective/Status/Map frame deliberately instead of accepting a helper's generic `WoodPanel`/`ReadingPaper` default. Likewise, ensure shared theme calls and local action builders leave every button background at multiplier `4.5` and the section 7 size. Panel slicing remains independent of the button rule.

For new gameplay:

1. Read this guide, inspect the original reference asset for each component, and inspect the host scene plus its prefab/runtime builder.
2. Choose the content palette and list the art/font assets to reuse. Start objectives/current challenges with `GraphObjectiveFrame`, timers/stage/status with `GraphStatusFrame`, and suitable larger cards with `GraphMapFrame`. Keep role-specific books/settings artwork where appropriate. Record which source features must be preserved.
3. Identify which layer owns the UI: scene, prefab, runtime builder, or editor setup. Implement in that layer.
4. Build the challenge layout, instruction, progress/timer if required, interaction affordance, feedback, and completion/retry/return states appropriate to the requested gameplay.
5. Assign fonts and colors explicitly; preserve the host canvas/input conventions. Use section 7's consistent button dimensions and always set background Image multiplier `4.5`, including after theme/helper calls. Prefer reusable components or style constants where useful, without silently redesigning existing screens.
6. Verify the result in Play Mode and at target resolutions. Update this guide only for intentional reusable design decisions, labeling new conventions clearly.

## 10. Known inconsistencies to avoid propagating

- Older exploration scenes reference font GUID `b86979d84307d064da1c84f6fcc3eb3e`, which was not resolved to an asset in this audit. Do not copy that reference blindly; inspect the rendered scene and assign an existing font deliberately.
- Some prefabs and scene objects contain legacy/default gray controls, placeholder text, or generic object names. Their existence does not make them the desired look for a new screen.
- Sets creates its own flat-color HUD and assigns the TMP default. Although the fields are headed "Optional Scene UI Overrides", `BuildHudIfNeeded()` currently creates and assigns new elements; do not assume assigning those fields preserves a custom scene HUD.
- There are duplicate sprite variants in two interface folders, mixed canvas scale settings, and different fonts for display versus reading. Check actual references rather than selecting the first matching filename.
- Gameplay palettes and original illustrated artwork serve different purposes: the palette can vary, while frame materials and rendering must stay consistent with the selected reference. GraphHUD is an approved shared frame family for new UI and requested reskins; reuse its actual artwork rather than inventing a different silhouette for every mode.
- Existing button builders and serialized controls may still use different dimensions and multipliers such as `4` or `6`. Those are implementation inconsistencies, not alternate standards. Section 7's sizes and mandatory `4.5` apply to new or requested restyled buttons. A panel's independent slicing multiplier is not a button exception.
- Historical ornate TealPanel/TealButton sprites remain in Resources but are not loaded by the current shared theme. Use current brown-frame variants instead. This document does not authorize an unrelated full-project reskin.

## 11. Acceptance checklist for new gameplay UI

- [ ] Names the material/style reference for every new or reskinned component. Objectives/current challenges use `GraphObjectiveFrame`, timers/stage/status use `GraphStatusFrame`, and suitable larger surfaces reuse `GraphMapFrame` across modes.
- [ ] Preserves source silhouette, frame material/hue, border thickness, bevel profile, highlight intensity/direction, shadow and texture density.
- [ ] A requested color/text variant changes only its permitted face, label or icon; no added stone, reflective gold or ornament absent from the source.
- [ ] Compared beside the original at the same visible size; differences beyond the intended edits have been corrected or explicitly reported.
- [ ] Reuses existing artwork and appropriate fonts; no duplicate baked labels.
- [ ] Correct sprite/sub-sprite references; artwork and frames are not distorted.
- [ ] New button variants preserve the reference frame/shading and use independently editable labels; recoloring does not muddy the frame or icons.
- [ ] Buttons use section 7's bounded sizes; peer actions match, labels remain readable, and stretch anchors/layout groups do not create excessively long or tiny buttons.
- [ ] Every button background/frame Image has Pixels Per Unit Multiplier **4.5**, including after theme calls, runtime rebuilding, prefab overrides, and state changes. Sprite import PPU and non-button panel multipliers are preserved separately.
- [ ] Requested placeholder replacements have a complete themed treatment, with restrained decoration around readable content.
- [ ] Reskinned components preserve callbacks, references, hit areas, and runtime-generated styling; before/after appearance and interaction have been compared where available.
- [ ] Logical/mathematical glyphs render correctly and long text remains readable.
- [ ] Normal, interaction, disabled/locked, feedback, and result states are understandable.
- [ ] Timer, minimap, objectives, movement, look, jump, and interaction controls do not overlap.
- [ ] Touch targets work at the target device size; layout respects safe areas.
- [ ] Decorative graphics do not block clicks, taps, or dragging.
- [ ] Modal UI, dialogue, retry, return, and completion work with existing managers.
- [ ] No visual cue reveals an answer before the gameplay permits it.
- [ ] Shared GraphHUD use preserves cream interiors/dark ink, clipped matte wood frames, correct sub-sprite slicing, and editable content. Graph diagrams retain their functional orange/blue edge meanings; other screens retain their own gameplay semantics.
- [ ] Compared in Play Mode with the reference at 1920 × 1080 and relevant phone/tablet aspect ratios; report any checks that could not be run.

## 12. Reusable Codex prompt

Copy the following prompt and replace the bracketed gameplay description. Attach this file when the repository is not already available.

```text
Implement [describe the new gameplay, scene, rules, and required UI] in Logic Legends.

Before making changes, read the supplied artstyle.md or the copy in the project root. Treat the original project sprites listed in its reference-priority table as the visual authority. Inspect those images and the host scene's UI code/prefabs. Use the original wood/parchment illustration style, the appropriate gameplay content palette, and the existing book treatment for lessons.

For every gameplay mode, use Assets/Art/GraphHUD/GraphObjectiveFrame.png for objectives/current challenges and GraphStatusFrame.png for timers/current stage/compact status. Reuse GraphMapFrame.png for maps and suitable larger panels. These sprites may also be used for other suitable UI across the project; they are not Graph-only. Preserve matte brown clipped frames, cream interiors, dark editable content, and actual sub-sprite slicing. Do not default every surface to the settings background or a rolled parchment scroll. Keep role-specific books/settings artwork where appropriate, shared wood-rimmed actions, and original touch controls. Preserve Graph's diagram colors when working on Graph. Limit changes to the requested screen/features. For explicitly requested new panel geometry, preserve original material/rendering; ordinary recolor/relabel variants preserve source silhouette.

Reuse the project's sprites, fonts, controls, layout conventions, and managers. Follow artstyle.md for exact asset paths and colors. Assign fonts explicitly, verify mathematical glyphs, preserve sprite proportions, avoid duplicate baked labels, and keep movement controls, minimap, timer, and gameplay visible. Include the interaction, feedback, and result states this gameplay needs. Distinguish intentional design guidance from legacy/default UI and existing inconsistencies documented in the guide.

For new buttons, preserve the original frame material and hue, silhouette, border thickness, bevel, highlight width/direction/intensity, shadow and texture density. Change only the requested face color, label or icon. Prefer the exact existing artwork or a clean blank derivative with separate editable TMP text. A teal face must retain the original brown rim and smooth painted surface. Do not add stone cracks, hammered metal, reflective gold, ornament or stronger shine absent from the original. Existing ornate StageUI derivatives are not the matching baseline.

Use consistent button sizes from section 7: standard text actions 280 x 88 at a 1920 x 1080 reference, compact 220 x 88, longer labels at most 340 x 88; peer actions match. Icon actions and symbol choices use their documented role sizes. Do not stretch actions across large cards or shrink them into tiny controls. Always set each button background/frame Image's pixelsPerUnitMultiplier to 4.5f, including after theme calls, runtime construction, prefab setup, and state changes. This is an Image setting, not sprite import PPU or CanvasScaler. Preserve non-button panel slicing settings.

Within the requested scope, replace unfinished panels/buttons with artwork reused or faithfully derived from the original assets. Keep puzzle content uncluttered and preserve intentional simple controls. When new raster artwork is needed, provide the actual original asset to the image-editing tool, request only the required changes, and compare the candidate beside the original at the same visible size. Revise material/shading/texture drift before accepting it. Matching colors alone is insufficient. Preserve all gameplay callbacks and input behavior.

Implement the UI in its owning scene, prefab, or runtime builder. Preserve unrelated work. Validate the result against the reference in Play Mode and relevant landscape/mobile sizes when available, and state any validation limitations. Do not introduce a new visual theme unless I explicitly request one. Update artstyle.md only if this task intentionally establishes a reusable design convention.
```

This file is the durable reference. Supply it or explicitly ask the next task to read it; the prompt does not depend on memory of an earlier chat.
