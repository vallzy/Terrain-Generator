# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Added
- Integer grid alignment for all vertex deformation types — all Z (and wall X) coordinates are now snapped to whole numbers via `Math.Round`, guaranteeing brushes are always on the Radiant grid regardless of noise or shape settings.
- Tunnel ceiling brushes now have a solid cap of `HeightZ` units above the cave ceiling surface, matching the solid thickness of the floor, so ceilings are no longer paper-thin at the tunnel center.
- Auto-detection for hint brush `func_group` when loading a `.map` file — no manual selection required.
- Manual mode default dimensions changed to 1024 × 1024 × 64 for more practical out-of-the-box results.

### Changed
- README overhauled: completed the cut-off CLI examples section, added five full usage examples covering valley, hill, crater, tunnel, and slope tunnel shapes, documented auto-detection, and clarified `--tunnelheight` usage for both tunnel types.

---

## [1.1.0] - 2026-02-28

### Added
- Configuration UI with persistent settings (Game Data Path, Output Folder, Default Target Mode, Default Noise Type, Default Texture) saved to `config.json`.
- Texture Browser with `.pk3` and directory mounting, hierarchical shader grouping, Favorites system, and raw shader info viewer.
- Shader Info Window for inspecting raw shader definitions.
- Tunnel (`cave`) and Slope Tunnel (`slopetunnel`) shape types with full floor, ceiling, and left/right wall heightmaps.
- 3D preview camera pan, zoom, and double-click focus controls.

### Changed
- Texture browsing now requires a valid Game Data Path to be configured before opening the browser.
- Configuration JSON synced correctly on save to prevent stale state.

---

## [1.0.0] - 2026-02-27

### Added
- Initial release of the Radiant Terrain Generator.
- Dual-interface design: WPF GUI with real-time 3D preview and headless CLI.
- Hint Brush mode: reads a `func_group` hint brush from an existing `.map` file and replaces it with generated terrain.
- Manual mode: generates standalone terrain centered at world origin.
- CSG math library for deriving 3D bounding boxes from plane intersections.
- Procedural landforms: Hill, Crater, Ridge, Slope, Volcano, Valley.
- Perlin, Simplex, and Random noise with adjustable frequency and variance.
- Terrace Step modifier for tiered/staircase terrain.
- Strict grid snapping to fit terrain bounds to sub-square sizes, preventing micro-leaks.
- GitHub Actions release workflow (`release.yml`).
- MIT license.
