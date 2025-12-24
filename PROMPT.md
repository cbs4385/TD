### Cleaned prompt tailored to this project

You are working in the Unity project located at `/workspace/TD`, using scene **FaeMazeScene**. The Main Camera has a `CameraController3D` component in **Assets/Scripts/Camera/CameraController3D.cs**, which already supports orbit, pan, dolly, and focus helpers with fields such as Pan Speed, Orbit Speed, Dolly Speed, Min/Max Pitch, Min/Max Distance, Collision Radius, and references to `MazeGridBehaviour`. It tracks `focusPoint`, `currentYaw`, `currentPitch`, `currentDistance`, and collision handling, and uses RMB drag to orbit, MMB drag to pan, and scroll to dolly.

Rewrite/implement the following behavior in that controller:

- Orbit the camera’s **position** on a perfect horizontal circle (XZ plane) around a focal tile while always **looking at** the focal point.
- Maintain constant radius during orbit unless dolly changes it; clamp to existing Min/Max Distance if they’re enabled.
- Preserve existing controls and speeds (pan/dolly/orbit); use `orbitSpeed` for yaw changes and respect pitch constraints if re-enabled.
- Keep a persistent focus API:
  - `public void SetFocusPoint(Vector3 worldPos)` (alias for `FocusOnPosition`)
  - optional `SetFocusTile` if tile type exists
  - `public Vector3 FocusPoint { get; }`
- Add an **Auto Orbit** toggle: when enabled, orbit continuously at `orbitSpeed` degrees/sec; when disabled, only orbit via input.
- Avoid drift: track yaw explicitly, recompute position from yaw/pitch/radius each frame, and re-LookAt the focus after collision adjustments.
- Respect collision adjustments so the camera still looks at the focus when pushed closer.
- Hook into existing tile/visitor focus as appropriate (see `FocusOnPosition/Heart/Entrance/Visitor` methods). When focus changes, preserve yaw/pitch and recompute radius from current camera position for stability.
- Input guidance: keep RMB drag orbiting; add keyboard orbit with **A/D** or **←/→** to adjust yaw using `orbitSpeed` (keep pitch within min/max if pitch is adjustable).
- Implementation scaffolding:
  - Cache `_focusPoint`, `_yawDeg`, `_pitchDeg`, `_radius` in Awake/Start from current transform and focus.
  - Position: `Quaternion rot = Quaternion.Euler(_pitchDeg, _yawDeg, 0f); Vector3 offset = rot * new Vector3(0, 0, -_radius); Vector3 desiredPos = _focusPoint + offset;` then apply collision shortening and `transform.LookAt(_focusPoint, Vector3.up)`.
  - Provide serialized default focus Transform/Vector3 for fallback if no selection system exists.
- Add an Inspector-friendly Auto Orbit checkbox and FocusPoint accessor; keep code allocation-free in Update (no LINQ).

Final delivery expectations:
- Modify `CameraController3D` (avoid creating a new controller unless necessary).
- Include any minimal helper for focus/tile integration if required.
- In your final explanation, list changed files and why, paste the full updated C# script(s), and describe how to set the focal tile in the Inspector (what to assign) and how to test in Play Mode.

### Testing
⚠️ Not run (per instructions: static review only).
