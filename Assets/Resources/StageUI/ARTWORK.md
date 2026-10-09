# Stage UI artwork — strict original matching

Applied to PRELIM Stages 1–2 and RulesOfInference using artstyle_update.md (2026-10-07). Original brown wood and painted face shading are the references. Former TealPanel.png and TealButton.png remain unused historical assets; their stone/gold treatment is not a style authority.

## Sources and imports

| Asset | Original reference / creation | Texture size | Sprite region (x, y, w, h) | Borders (left, bottom, right, top) |
| --- | --- | --- | --- | --- |
| WoodPanel.png | Exact copy of Assets/Interface_Sprites/settings_interface/settings_background.png | 1600×1200 | 258, 144, 1068, 953 | 92, 90, 92, 90 |
| ReadingPaper.png | Exact copy of Assets/Interface_Sprites/customizationsh_shop_interface/customization_scroll.png | 1400×1200 | 247, 105, 911, 1004 | 44, 44, 44, 125 |
| WoodButtonTeal.png | Built-in ImageGen edit of original Assets/Interface_Sprites/main_menu_interface/btn_play.png | 1774×887 | 244, 257, 1286, 370 | 185, 40, 185, 40 |
| WoodButtonGreen.png | Same original Play reference; green face | 1774×887 | 245, 256, 1285, 371 | 185.5, 40, 185.5, 40 |
| WoodButtonRed.png | Same original Play reference; red face | 1774×887 | 238, 258, 1298, 370 | 185, 40, 185, 40 |

Imports: Sprite Multiple with one sub-sprite, transparent alpha, no mipmaps, uncompressed, maximum 4096. Original PNGs and metadata remain unchanged. Explicit sprite bounds remove transparent surroundings. Borders preserve edge shapes while allowing the center to resize. Image.Type.Sliced multipliers: large panels 1.7, small HUD/feedback plaques 5, reading pages 1.5, actions 4. Intended reference sizes: panels 1000–1700 wide / 650–950 high; HUD 520×148; buttons 220–720 wide / 80–110 high. Labels remain independently editable.

## Use

StageUITheme reuses the wooden frame with a separate calm #09161C content inset for results, HUD, door quizzes and feedback. Tutorials reuse the StudyLibraryController book composition: two cream pages, #291A0F ink, central brown spine, gold cover title, bottom navigation. The left page holds the current lesson; the right holds a concise stage reference. Existing tutorial controllers and label references remain connected.

Teal actions: Help, navigation and puzzle choices. Green: Begin/Continue/Retry. Red: Main Menu. Explicit fonts: IMPACT TMP for headings/actions; LiberationSans for reading. StageButtonFeedback differentiates idle, hover/focus, pressed and disabled using label colors, preserving the white-tinted painted frame and stable touch target. Existing callbacks, board-view HUD hiding and explicit stage navigation remain intact.

Used by StageJourneyUI's runtime HUD/results/inference tutorial and scene-authored PRELIM tutorial, help, quiz, submit and feedback controls. No prefab was introduced. Padding protects wood trim and paper rolls; the world view and original touch controls stay open.

## Image generation prompts

Built-in tool, precise-object-edit, one call per face color (#294F54 teal, #287543 green, #98352E red):

> Edit the supplied original Logic Legends Play button. Remove only PLAY and reconstruct the smooth painted face. Change only the blue face to the specified color, retaining the broad upper highlight and dark lower bevel. Preserve the brown wooden rim hue, silhouette, thickness, restrained layered bevel, texture density, shadows and lighting. Keep native capsule proportions, fully visible with transparent surroundings. No text, icons, added trim, ornamental corners, stone cracks, metallic gold, glossy reflections, sparkle or bloom. Output one blank button; this is a minimal reference edit, not a redesign.

Candidates were inspected beside the original at the same displayed size in Unity. The panel is an exact copy. Validation captures live under Temp/StageUIQA, not the source asset tree. Android hardware validation was not performed.
