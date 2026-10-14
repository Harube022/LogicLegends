# Profile-style currency icons

Generated with the built-in image generation tool on 2026-10-13, using the approved profile concept as the visual reference. These are separate currency sprites intended for the shop and future profile UI. Existing currency sprites and scene assignments have not been replaced.

- `Coin.png`: 1254 × 1254 RGBA PNG; raised yellow-gold circular rim, unmarked inset face, warm orange depth, upper-left highlight; transparent surroundings.
- `Gem.png`: 1254 × 1254 RGBA PNG; upright hexagonal emerald-green gemstone, broad simple facets, light upper-left and dark lower facets; transparent surroundings.
- Both icons are text-free. Keep their aspect ratio, use white UI tint, and preserve alpha transparency. Use separate editable currency labels and amounts.

## Generation prompt set

### Coin

Create a standalone transparent PNG game currency icon derived precisely from the gold COIN shown in the COINS plaque in this supplied Logic Legends profile mockup. The reference image is the visual authority; extract/recreate ONLY that coin as a clean high-resolution sprite. One single chunky, round yellow-gold coin, nearly frontal with a very slight three-quarter tilt, broad raised circular rim, simple recessed unmarked golden center, subtly visible dark orange thickness on lower/right edge, broad painted upper-left highlight, warm orange lower shading. Match the reference icon's friendly illustrated 2.5D rendering, smooth clean painted shapes, restrained highlights and outline. Preserve original silhouette and simple unmarked face; NO letter, currency sign, number, logo or text. Not a photorealistic metal object, no tiny filigree, no extra rings beyond the reference rim, no cracked stone, no glow or star sparkles. Entirely remove the cream plaque, all text, all wooden framing, every other UI element and forest. Icon alone, centered on a square canvas with balanced 12 percent transparent padding around its outer silhouette. Real alpha transparency outside the coin, no baked white/black/checkerboard backdrop, no ground plane, no cast shadow outside the silhouette. Crisp smooth edges, suitable for Unity uGUI shop currency display at small size. Output only the coin image.

### Gem

Create one standalone transparent PNG game currency GEM icon derived precisely from the GREEN GEM in the GEMS plaque of reference image 1, the Logic Legends profile mockup. Reference 2 is the companion generated coin, supplied only to match scale, edge clarity and the friendly painted rendering. Recreate ONLY the green gem from reference 1 as a crisp clean high-resolution Unity UI sprite. One upright chunky hexagonal emerald-green gemstone, symmetrical silhouette with point at top and bottom, broad simple central hexagonal face and clear beveled polygon facets surrounding it. Top-left facet light vivid green, right facet medium green, lower facets deep forest/emerald green; neat darker green outline, restrained small pale green upper-left edge highlight. Match the gem's original simple faceted geometric construction and smooth illustrated 2.5D game icon style. Entirely green, no golden setting or metal, no ring, no jewelry mount, no letters/numbers/logo/text, no sparkles, no glow, no photorealistic refraction, no excessive tiny facets. Remove all panels, text, wood, characters and forest from the reference. Icon centered alone on a square canvas, with balanced roughly 12-percent transparent padding around the silhouette and similar occupancy to the companion coin. True alpha transparency outside the silhouette, no baked white/black/checkerboard backdrop, no ground or cast shadow. Output only the green gem image; do not include a coin or any other object.

## Profile preview

The companion preview is saved at `output/profile-design/profile-no-pets.png`. It removes the owl companion and pet equipment slot. All player values and the illustrated character are mock data; later UI implementation should use the existing player model and saved account data.
