# Prompt: FaeLantern fascination logging cleanup

You are updating the FaeMaze project to clean up FaeLantern/Visitor logging.

Goals
- Remove existing ad-hoc `Debug.Log*` statements that were temporarily added for fascination/pathing debugging around the FaeLantern and Visitor fascination flow.
- Add structured log statements that clearly describe when a visitor becomes fascinated, retargets pathing toward a FaeLantern, reaches it, and switches to random-walk movement.
- Keep noise low: log only key lifecycle events and path transitions that help trace fascination effects.

Scope to inspect
- `MazeAttractor` (FaeLantern triggers and fascination chance rolls): `Assets/Scripts/Maze/MazeAttractor.cs`
- `VisitorController` fascination state and path handling: `Assets/Scripts/Visitors/VisitorController.cs`

Implementation instructions
- Remove/replace the current fascination-related `Debug.Log*` lines in these files (e.g., trigger entry, roll results, fascination reach/retarget logs) so only the new structured messages remain.
- Introduce concise, context-rich logs for:
  - Fascination chance evaluation (visitor name, lantern position, roll vs. threshold, result).
  - Visitor fascination state changes (becomes fascinated, fascination rejected/disabled, fascination cleared on escape/death).
  - Path retargeting to the lantern (old destination vs. lantern target, path length/remaining waypoints).
  - Lantern reached and transition to random-walk mode.
  - Random-walk intersections/decisions only when the decision materially changes direction (avoid per-frame spam).
- Prefer a consistent tag format (e.g., `[Fascination]`) to make filtering easy; avoid duplicating messages for the same event.
- Do not alter behavior logic—only replace/remove logging. Leave warnings/errors unrelated to fascination intact.

Validation checklist
- After edits, fascination behavior is unchanged, but logs now show a clear story of fascination lifecycle and path decisions.
- No leftover temporary debug lines remain for the fascination feature.
