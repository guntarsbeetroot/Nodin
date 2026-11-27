# Changelog

## [Unreleased]

## [1.1.0]

### Added

- An importable Attribute Examples sample with an inspector browser and prefab context demonstrations.
- Agent instructions for using Nodin in tools and inspector-configurable UI.
- Asset and child-object pickers, color palettes, and managed-reference type selection.
- Numeric bounds, range and progress controls, unit conversion, wrapping, delayed fields, and enum paging.
- Prefab and inline-editor conditions, validation, conditional and toggle groups, and responsive button layouts.
- Inspector lifecycle and collection callbacks, expandable help, dynamic tooltips, and drawer diagnostics.
- Matrix and dictionary editors, resizable table columns, and additional list, dropdown, title, and path options.

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
