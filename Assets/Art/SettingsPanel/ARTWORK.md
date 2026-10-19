# Settings section header

`SettingsSectionHeader.png` was created with the built-in image_gen tool for the Main Menu Graphics tab. It replaces the narrow clipped-corner DISPLAY and EFFECTS plaques with the user's wider, softly rounded wooden header reference. Text remains editable IMPACT SDF in Unity.

References: the user's `codex-clipboard-9d807ae6-2de7-424f-b6a0-6ab5d575078f.png` header crop for silhouette, and `Assets/Interface_Sprites/settings_interface/settings_background.png` for the original illustrated wood material, per artstyle.md. At the user's request, the same header is applied to all ten section headings across the five Settings tabs.

Prompt:

+ Use case: stylized-concept. Asset type: Unity 2D game UI section header background sprite, reusable with editable text added in-engine.
 Create ONE blank horizontal warm wooden header matching the DISPLAY and EFFECTS header boards in reference image 1, using the existing game's original painted wood material in reference image 2. Reference image 1 is the exact target silhouette and visual simplicity: a long rounded rectangle with gently clipped/rounded corners, a restrained thin dark-brown rim and shallow inset bevel, broad low-density horizontal wood grain, matte medium warm brown center, subdued top highlight, slightly darker lower edge. The header should be thicker/taller than a thin title strip, approximately 9:1 width to height. Front-facing orthographic, straight horizontal, no perspective. Use reference 2 only for original brown wood hue and illustrated rendering; do not copy its large square shape, corner blocks or side rails.
 Output a single standalone header, centered and fully visible with tight transparent padding, true transparent background. The entire center is plain warm wood, with room for a cream text label added separately later. Absolutely NO TEXT, letters, symbols, icons, extra ornaments, gold or metallic trim, stone, cracks, glow, glossy highlights, extra side extensions, other UI, scene background or second board. Preserve the original game's simple friendly illustrated material and restrained bevel. Intended as a nine-sliced sprite with protected corner caps, not a full settings mockup.

SettingsSliderTrack.asset is a native Unity Sprite derived from the original slider texture. It crops two outer pixels and defines protected rounded caps for nine-slicing. It does not modify the original PNG or sprite metadata. The fill and handle share a 5.5 px horizontal groove inset; the fill sits 4.5 px inside the track vertically.
