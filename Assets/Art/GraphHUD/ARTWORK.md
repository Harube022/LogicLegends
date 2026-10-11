# Graph HUD artwork

These three new raster assets were generated with the built-in imagegen tool for the Graph scene. They replace the reused settings/scroll/card panels. This is a Graph-local design, not a new project-wide convention.

The original `Assets/Interface_Sprites/settings_interface/settings_background.png` and `Assets/Interface_Sprites/main_menu_interface/btn_play.png` were supplied to the tool as wood/material/rendering references. All labels, graph diagrams, nodes, route lines, countdowns and result text remain editable Unity UI elements. The field guide retains the existing book treatment. Buttons retain their existing artwork and the requested Pixels Per Unit Multiplier of 6.

## Generation prompts

Shared prompt: Use case: style-transfer. New Graph gameplay HUD raster panel sprite, no text. Input 1 is the original settings panel, used only as authority for warm brown painted wood, dark outline, matte smooth bevel, broad subdued upper-left highlights, shadow depth and low texture density. Input 2 is the original button, used only for understated smooth painted rendering. Create newly designed artwork rather than reusing the old panel or scroll. Slim rectangular wooden picture-frame surround, simple clipped corners, calm cream parchment inset (#EFE0BB), no rolled scroll ends. Wood hue/material must match input 1; consistent border about 6 percent of short side. Straight-on orthographic 2D illustration, transparent outside silhouette with modest close lower-right shadow, entire silhouette inside canvas, small transparent margin, nine-slice-safe straight middle edges and plain center. No labels, symbols, diagrams, numbers, letters, logos, watermark, ornate corner blocks, metal, gold, stone, cracks, jewels, glows or shine.

- GraphMapFrame: square 1:1 panel, modest clipped corners, two plain slim nested brown rim layers, large unmarked cream drawing surface for a minimap.
- GraphObjectiveFrame: wide landscape 2.5:1 compact notice plaque, slim brown wooden rim, simple clipped corners, cream inset, blank objective area without a header stripe.
- GraphStatusFrame: shallow landscape 3.6:1 status plaque, matching slim rim, cream center, less decoration than reference, blank interior for clock, route or feedback.

Unity sprite slicing trims transparent margins without altering the generated PNG pixels, and preserves the corners and frame thickness when panels resize.
