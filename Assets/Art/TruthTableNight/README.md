# Truth Table former nighttime assets

TruthTable now uses sunset. The night visibility component has been removed from the scene: no facing cone, peripheral blur/darkness, forward light, offscreen world texture or presentation camera runs during gameplay.

The scene still uses this folder's yellow slot-arrow mesh/material and its dedicated Celestial Gold lantern material. Lamp emission has been reduced for sunset. The slot arrow's placement logic and all gameplay remain unchanged.

The unused night shader/material and component source are retained as unreferenced legacy assets. Old setup/check entry points redirect to the sunset setup/checks, so they cannot reinstall the retired camera effect accidentally.

See `Assets/Art/TruthTableSunset/README.md` for current lighting settings.
