# Willow-the-Wisp Model Setup Instructions

The `willowthewisp.fbx` file contains the 3D model and animations for the Willow-the-Wisp character.

## Current Setup

The WillowTheWisp prefab is currently configured to use **procedural sprite rendering** (a simple glowing circle). This works correctly but doesn't use the FBX model.

## How to Use the FBX Model

To use the actual 3D model from the FBX file, follow these steps in Unity Editor:

### Step 1: Create a Prefab from the FBX

1. In Unity Editor, navigate to `Assets/Animations/`
2. Find `willowthewisp.fbx` in the Project window
3. Expand the FBX file to see its contents
4. Drag the root GameObject (usually named "willowthewisp" or "WISP_Armature") into your scene Hierarchy
5. Select the instantiated object in the Hierarchy
6. Drag it from the Hierarchy into `Assets/Prefabs/Props/` to create a prefab
7. Name it `WillowWispModel`
8. Delete the instance from the Hierarchy

### Step 2: Configure the WillowTheWisp Prefab

1. Open `Assets/Prefabs/Props/WillowTheWisp.prefab` in the Inspector
2. Find the "Willow The Wisp (Script)" component
3. Under "Model Settings":
   - Set **Use Procedural Sprite** to `false` (unchecked)
   - Assign the `WillowWispModel` prefab you created to **Wisp Model Prefab**
   - The **Wisp Controller** should already be set to `WillowWisp` animator controller
4. Save the prefab

### Step 3: Test

1. Open `FaeMazeScene`
2. Enter Play mode
3. Place a Willow-the-Wisp in the maze using the build mode
4. The 3D model should now appear and animate correctly

## Troubleshooting

- **"Missing (Game Object)" error**: The prefab reference is broken. Follow Step 1 again to create a fresh prefab.
- **InvalidCastException**: Cannot instantiate directly from FBX. You must create a prefab variant first (Step 1).
- **Model doesn't animate**: Check that the Wisp Controller is properly assigned in the WillowTheWisp prefab.
- **Model is invisible**: Check that the WillowWispModel prefab has a MeshRenderer component enabled.

## Technical Notes

Unity FBX files cannot be directly instantiated at runtime using `Instantiate<GameObject>()`. They must first be converted to Unity prefabs through the Editor. This is why the procedural sprite rendering is used by default - it works without requiring manual prefab creation.
