Status: Done

# PR #19 integration with main

The developer requested resolving history and merge conflicts so PR #19 can merge directly into main.

Merge main commit `9d2d121d948d2d2f93997e85f1ac5232be576e29` into `cfb/fuel-gauge-model` with a merge commit, preserving the full reviewed stack. Main has the UI commits under different commit IDs: its complete file tree is identical to existing PR #19 ancestor `1c326550d25f1d7ebac7626bc41aaabae4d6bf09` (tree `3acf2e210af98c4ca8459e4132bc9463953c2761`). There are no additional main changes to apply.

The six conflicts concern `.docs/ARCHITECTURE.md`, `.docs/UI_FRAMEWORK.md`, `Assets/Prefabs/Singletons/GameLifetimeScope.prefab`, `Assets/Scripts/Core/Infrastructure/ProjectLifetimeScope.cs`, `Assets/Scripts/UI/HudOverlayView.cs` and `Assets/Scripts/UI/MenuOverlayView.cs`. Retain the reviewed PR #19 versions, including installer-based UI composition and the architecture corrections. Restore the existing prefab version without editing its serialized contents.

Verify the resolved source and assets are unchanged from pre-merge PR #19, compile in Unity, run all EditMode tests, and check GitHub mergeability after pushing. Leave the actual merge into main to the developer.

Validation: the resolved tree matches pre-merge PR #19 except for this integration record. No unresolved index entries remain. Unity 6000.4.5f1 reports compilation up to date with no compiler errors, and all 161 EditMode tests pass. Runtime behavior and assets are unchanged, so the host/client and late-join human playtest notes in `pr-review-fixes.md` still apply.
