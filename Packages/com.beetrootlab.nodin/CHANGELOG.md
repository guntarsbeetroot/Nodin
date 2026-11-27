# Changelog

## [Unreleased]

### Changed

- Standardized type names on the Nodin prefix. Custom integrations using the renamed types must update their references, including `NodinWindow`, `NodinPropertyTree`, `NodinInspectorExtensions`, and `INodinDropdownItem`.
- `Toggle` now draws a full-width header with a separate checkbox and foldout, and applies `GUIColor` to the header.

### Fixed

- `FoldoutGroup` arrows now stay inside their headers, and expanded panels enclose their nested groups.
- Foldout arrows now stay inside `TabGroup` panels.
- Adjacent tabs in a `TabGroup` share a single divider.

## [1.0.0]

### Added

- Attribute-driven IMGUI inspectors with layout groups, conditional controls, validation, collection editors, and method buttons.
- Editor windows, property trees, and registration hooks for custom drawers and panels.
- Build validation that keeps Nodin assemblies and package assets out of player builds.
- MIT licensing and links to release notes and license information in Unity Package Manager.
