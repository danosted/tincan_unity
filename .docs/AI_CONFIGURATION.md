# AI Configuration & Operational Guidelines for TinCan Unity

**Purpose:** Define how AI assistants operate on this project, so humans know what to expect and AI sessions
behave consistently. Humans: read this once so you can tell when an AI is off-script.

---

## 🤖 AI Assistant Operational Rules

### Rule 1: Two modes, and the developer picks

**Discussion mode (default).** Unless the developer asks for a change, the AI conceptualises and discusses:
clarify requirements, propose designs against `ARCHITECTURE.md`, point at the relevant TinCan integration points,
compare trade-offs, and ask when a material decision is needed. No files change.

**Implementation mode (when asked).** When the developer asks for code, assets or a whole feature, the AI does the
work end to end, and it must:

1. Follow [`TUTORIAL_NEW_FEATURE.md`](TUTORIAL_NEW_FEATURE.md) and [`TASK_GUIDES.md`](TASK_GUIDES.md): installer
   pattern, thin mediators, logic in `Features`, no edits to the "Do NOT touch" files.
2. Ship EditMode tests for every processor, use case and handler, and leave the suite green.
3. Recompile and check the console after every C# change (Rule 11).
4. Add a row to the feature index in [`CODE_MAP.md`](CODE_MAP.md#feature-index) for a new feature, and keep the
   docs true when behaviour changes.
5. End with a human-readable summary: what changed, which files to read first, what to look at in the Editor,
   what still needs a human playtest. Editor-side work it did unattended (assets, prefab placement) is flagged
   as "please review in the Inspector".

Switching from discussion to implementation is always an explicit developer request, never inferred.

### Rule 1.1: Architectural Constraints & Naming

**The AI MUST strictly adhere to the following architectural constraints defined in `.docs/ARCHITECTURE.md`:**

1.  **Mediator Naming:** Any new `NetworkBehaviour` MUST be suffixed with `NetworkMediator`. Never use `Controller` or `Manager` for networked components.
2.  **Input-Driven Simulation (Contextual):** Prioritize "Input Sync" for simulation-critical features (Movement, Abilities, Gunplay) where inputs must be replicated and simulated across clients. Use `InputState` structs and `SimulationUseCase` patterns. If an input or action does not require high-frequency network replication or simulation across clients, do not force it into the `InputState` pattern.
3.  **No Side-Channels (For Simulated State):** Avoid using `ServerRpc` for "triggering" actions that are part of the simulation loop. Pack these intents into the synchronized `InputState`. RPCs remain appropriate for discrete, non-simulated events (e.g., UI interactions, discrete game state changes like requesting possession).
4.  **Feature composition:** New features register through a `FeatureInstaller` asset. Never add feature registrations or `[SerializeField]` fields to `ProjectLifetimeScope`.

### Rule 2: Project Context - Unity3D, C#, Unity3D SDK

**This project is a Unity3D project, and all code discussions will be in the context of C# programming language and the Unity3D Software Development Kit (SDK).**

-   Assume standard Unity project structure and conventions.
-   Prioritize solutions that are idiomatic to Unity3D development.
-   Refer to Unity-specific APIs (e.g., `MonoBehaviour`, `GameObject`, `Transform`, `ScriptableObject`, `Vector3`, `Quaternion`, `Input`, `Physics`, `NetworkBehaviour` if applicable).

### Rule 3: Always Check Current Documentation (When Requested)

When discussing specific APIs or features, the AI should be ready to consult and reference current official documentation if there is uncertainty or if explicitly asked by the user.

**Official Sources to Reference (if needed):**
- [Unity Documentation](https://docs.unity.com/)
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/index.html)
- [GitHub Release Pages](https://github.com) for relevant packages (e.g., Netcode for GameObjects)
- [Package Manager Documentation](https://docs.unity.com/upm/manual/)

### Rule 4: Verify Evolving Tooling and APIs

When discussing Unity, NGO, package, or CLI behavior that may have changed, inspect the installed version and current official documentation before reaching a conclusion. State any remaining version uncertainty rather than relying on a fixed model knowledge cutoff.

### Rule 5: Version Management & Compatibility

When discussing implementations, the AI should be mindful of potential Unity version compatibility.

-   `.unity-version` is the source of truth for the Editor version.
-   If discussing a feature known to have changed across Unity versions, the AI should highlight this and ask the user for clarification on the target version or if specific version-dependent behavior is required.

### Rule 6: When to Ask the User

**AI should ask the human when:**
-   Unsure about specific project requirements or design preferences.
-   Multiple valid conceptual approaches exist, and user input is needed to choose.
-   Architectural decisions are being discussed.
-   Uncertain about version compatibility or major API changes since its knowledge cutoff.
-   The discussion requires context beyond the AI's current understanding.

**Don't guess. Ask.**

---

### Rule 7: Keep Responses Project-Focused

**AI MUST:**
- Keep all responses directly related to the project task or query.
- Avoid conversational filler, apologies, or unrelated pleasantries.
- Focus solely on providing technical discussions, context, or questions directly relevant to development.

---

### Rule 8: Be Concise in Communication

**AI MUST:**
- Keep output to an absolute minimum.
- Do not output large blocks of text or code unless explicitly asked.
- List options concisely when tasks are completed.
- Ask questions concisely and directly. If the user wants more information, they will ask.
- Avoid verbose explanations or summaries unless explicitly requested.

---

### Rule 9: Code Generation Preferences

**AI MUST:**
- Adhere strictly to `.docs/CODE_STANDARDS.md` when generating any code.
- Prefer dynamic property insertion and runtime discovery of components (e.g., `GetComponent`, VContainer injection, interfaces) instead of relying heavily on `[SerializeField]` attributes for component references.
- Use `[SerializeField]` primarily for primitive types (floats, ints, bools), custom structs/classes, or values that are genuinely intended to be tweaked directly in the Inspector and are not readily discoverable or configurable through code.
- Promote robust, less error-prone architectures where object references are established programmatically when possible.
- In docs, cite file paths; do not paste code that will drift.

---

### Rule 10: Mandatory Pre-Modification Context Check

**AI MUST always verify the current state of files before commencing any work:**

1.  **Check for External Edits:** The human developer may have formatted, refactored, or modified files since the AI's last action.
2.  **Read Before Replace:** Always inspect the exact target area before editing. Never assume the line numbers or surrounding whitespace match your previous output.
3.  **Acknowledge Context:** If the file structure has drastically changed, pause and re-evaluate the plan based on the current reality of the codebase.

### Rule 11: Mandatory Post-Modification Verification

**AI MUST always perform a verification pass after modifying code:**

1.  **Check for Errors:** Immediately after any file edit, check diagnostics on the modified files for compilation errors, syntax issues, or lint warnings.
2.  **Verify API Usage:** Check for deprecated API usage (e.g., ensure `linearVelocity` is used instead of `velocity` if required by the Unity version).
3.  **Validate Logic & Syntax:** Briefly review the resulting file to ensure no "end-of-file expected" or brace mismatch issues were introduced by the edit tool.
4.  **Unity Recompilation:** After modifying C# code, prefer dedicated Unity MCP compilation and status tools when exposed. If MCP is unavailable or disconnects during domain reload, run `unity cmd recompile && unity cmd recompile_status`. In either path, verify completion without compiler errors.
5.  **Tests:** `unity cmd run_tests --mode EditMode` must be green before reporting done.
6.  **Editor Objects:** Use Unity MCP tools or `unity cmd` for live Editor state and serialized object changes. Preserve Undo, explicitly save affected scenes or assets, and verify the result through Console logs or Scene captures as appropriate.

---

## ✅ Summary: How AI Should Operate in TinCan Unity

1.  **Discuss by default, implement on request.** The developer switches modes explicitly.
2.  **Contextualize:** Always frame discussions within Unity3D, C#, and the Unity3D SDK.
3.  **Follow the docs when implementing:** tutorial, task guides, installer pattern, tests, feature index, summary.
4.  **Verify Current Behavior:** Check installed tooling and current official documentation for evolving Unity APIs.
5.  **Ask for Clarification:** Proactively ask the user when details are unclear, or decisions are needed.
6.  **Verify Changes:** Recompile, test, and check the console after any modification.
