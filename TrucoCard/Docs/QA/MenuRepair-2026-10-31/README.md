# Original home restored; navigation corrected

- Removed the replacement `TrucoHomePresentation` and its installation call. `Assets/Scenes/MainMenu.unity` is unchanged, retaining its original wood header, avatar, user/balance labels, logo and two illustrated action buttons.
- Navigation now handles all original icon names (`Icon`, `Icon (1)`, `Icon (2)`, `Icon (3)`), keeps their proportions, uses consistent bounds and animates the active icon without changing the clickable area.
- Reuses the existing wooden header sprite for the navigation frame. Active tab uses a cream tile and dark caption; other tabs use muted icons and cream captions. Slot widths, icon centers and caption baselines are consistent, with device safe-area insets.
- Runtime C# compilation passed. Three targeted Unity EditMode regression tests passed (see `MenuRepair-Tests.xml`). Checks cover original scene content, all four icon names, repeated refreshes, selection on each tab, and retained click listeners.
- Rendered the actual MainMenu scene at 480x800, 1080x1920 and 720x1600, with every selected-tab state. Inspected compact, standard and tall portrait renders. Preview labels use the authored scene values; live user/balance binders are retained.
- Existing Play Mode instances need one stop/start to discard the old runtime-generated `ClubHome` object. The source scene and future loads use the restored home.

No gameplay, tournament, backend or wallet logic was changed in this correction.
