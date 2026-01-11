using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using FaeMaze.Systems;
using FaeMaze.Maze;
using FaeMaze.Audio;

namespace FaeMaze.Props
{
    /// <summary>
    /// Handles player placement of props on the maze by spending essence.
    /// Uses world-space coordinates for all placement logic.
    /// Tracks position occupancy to prevent duplicate props at the same location.
    /// Supports multiple placeable item types (FaeLantern, FairyRing, etc.).
    /// </summary>
    public class PropPlacementController : MonoBehaviour
    {
        #region Data Structures

        /// <summary>
        /// Build mode state for prop placement.
        /// </summary>
        public enum BuildModeState
        {
            Inactive,
            Active
        }

        /// <summary>
        /// Defines a placeable item type with its properties.
        /// </summary>
        [System.Serializable]
        public class PlaceableItem
        {
            [Tooltip("Unique identifier for this item (e.g., 'fae_lantern', 'fairy_ring')")]
            public string id;

            [Tooltip("Display name shown in UI")]
            public string displayName;

            [Tooltip("Prefab to instantiate when placed")]
            public GameObject prefab;

            [Tooltip("Essence cost to place this item")]
            public int essenceCost;

            [TextArea]
            [Tooltip("Description of the item's effect (shown in tooltip)")]
            public string description;

            [Header("Placement Constraints")]
            [Tooltip("Maximum number of this item that can be placed per maze (0 = unlimited)")]
            public int maxPerMaze = 0;

            [Tooltip("Minimum distance in world units between instances of this item (0 = no restriction)")]
            public float minDistanceBetweenProps = 0f;

            [Tooltip("If true, only allow placement on walkable locations")]
            public bool requiresWalkable = true;

            [Header("Preview Settings")]
            [Tooltip("Sprite to use for preview (optional; falls back to prefab's sprite)")]
            public Sprite previewSprite;

            [Tooltip("Color for the preview ghost (semi-transparent recommended)")]
            public Color previewColor = new Color(1f, 1f, 1f, 0.5f);
        }

        /// <summary>
        /// Represents a placed prop with its world position.
        /// </summary>
        private class PlacedProp
        {
            public Vector3 WorldPosition;
            public GameObject PropObject;
            public string ItemId;

            public PlacedProp(Vector3 worldPos, GameObject prop, string itemId)
            {
                WorldPosition = worldPos;
                PropObject = prop;
                ItemId = itemId;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the maze grid behaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Reference to the Heart Power Panel Controller (optional - prevents prop placement during targeting)")]
        private FaeMaze.UI.HeartPowerPanelController heartPowerPanel;

        [SerializeField]
        [Tooltip("Reference to the Heart Power UI (optional - prevents prop placement during targeting)")]
        private FaeMaze.HeartPowers.HeartPowerUI heartPowerUI;

        [Header("Placeable Items")]
        [SerializeField]
        [Tooltip("List of all placeable item types")]
        private List<PlaceableItem> placeableItems = new List<PlaceableItem>();

        [Header("Placement Settings")]
        [SerializeField]
        [Tooltip("Minimum distance between any two props in world units")]
        private float occupancyRadius = 0.5f;

        [Header("Preview")]
        [SerializeField]
        [Tooltip("Parent transform for preview objects (optional; will create if null)")]
        private Transform previewRoot;

        [SerializeField]
        [Tooltip("Color tint for invalid placement (e.g., non-walkable locations)")]
        private Color invalidPlacementColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        [Header("Build Mode")]
        [SerializeField]
        [Tooltip("Current build mode state (Inactive = disabled, Active = player can place props)")]
        private BuildModeState buildModeState = BuildModeState.Inactive;

        [Header("Cursor")]
        [SerializeField]
        [Tooltip("Cursor texture to use in build mode")]
        private Texture2D buildCursorTexture;

        [SerializeField]
        [Tooltip("Hotspot for build cursor (usually center or top-left)")]
        private Vector2 buildCursorHotspot = Vector2.zero;

        [SerializeField]
        [Tooltip("Default cursor texture (null = system default)")]
        private Texture2D defaultCursorTexture;

        [SerializeField]
        [Tooltip("Hotspot for default cursor")]
        private Vector2 defaultCursorHotspot = Vector2.zero;

        #endregion

        #region Private Fields

        private Camera mainCamera;
        private List<PlacedProp> placedProps;
        private PlaceableItem currentSelection;

        // Preview fields
        private GameObject previewInstance;
        private SpriteRenderer previewSpriteRenderer;
        private LineRenderer previewRadiusRenderer;
        private float previewRadius;
        private bool isPreviewValid;

        #endregion

        #region Properties

        /// <summary>Gets the currently selected placeable item</summary>
        public PlaceableItem CurrentSelection => currentSelection;

        /// <summary>Gets the list of all placeable items</summary>
        public List<PlaceableItem> PlaceableItems => placeableItems;

        /// <summary>Gets whether the controller is currently in build mode</summary>
        public bool IsInBuildMode => buildModeState == BuildModeState.Active;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Checks if Heart Power targeting is currently active.
        /// DISABLED: Targeting mode has been removed. All powers now automatically target the focal point.
        /// </summary>
        private bool IsHeartPowerTargetingActive()
        {
            // Targeting mode is disabled - all powers now automatically target the focal point
            return false;
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Get main camera
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            // Initialize placed props list
            placedProps = new List<PlacedProp>();

            // FORCE build mode to Inactive - props are hazards and should not be player-placeable
            // This overrides any Inspector settings to ensure build mode is always disabled
            if (buildModeState == BuildModeState.Active)
            {
                buildModeState = BuildModeState.Inactive;
            }

            // Validate references
            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Create preview root if not assigned
            if (previewRoot == null)
            {
                GameObject rootObj = new GameObject("PlacementPreviewRoot");
                previewRoot = rootObj.transform;
            }

            // Set default selection (first item in list for backward compatibility)
            if (placeableItems.Count > 0)
            {
                SelectItemById(placeableItems[0].id); // Use SelectItemById to trigger preview creation
            }
            else
            {
                return;
            }

            // Set initial cursor
            UpdateCursor();
        }

        private void Update()
        {
            // Handle cancel keys (Escape or Right-click)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExitBuildMode();
                return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                ExitBuildMode();
                return;
            }

            // Early exit if not in build mode
            if (buildModeState != BuildModeState.Active)
            {
                return;
            }

            // Update preview position
            UpdatePreviewPosition();

            // Check for left mouse button click using new Input System
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryPlaceProp();
            }
        }

        #endregion

        #region Selection Management

        /// <summary>
        /// Selects a placeable item by its ID.
        /// This will be called by UI in future tasks.
        /// </summary>
        /// <param name="id">Unique identifier of the item to select</param>
        public void SelectItemById(string id)
        {
            PlaceableItem item = placeableItems.FirstOrDefault(p => p.id == id);
            if (item != null)
            {
                currentSelection = item;

                // Create or update preview for new selection
                CreateOrUpdatePreview();

                // Enter build mode when selecting an item
                EnterBuildMode();
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// Gets the currently selected placeable item.
        /// </summary>
        /// <returns>The current selection, or null if none selected</returns>
        public PlaceableItem GetCurrentSelection()
        {
            return currentSelection;
        }

        /// <summary>
        /// Gets a placeable item by its ID.
        /// </summary>
        /// <param name="id">Unique identifier of the item</param>
        /// <returns>The item if found, null otherwise</returns>
        public PlaceableItem GetItemById(string id)
        {
            return placeableItems.FirstOrDefault(p => p.id == id);
        }

        /// <summary>
        /// Gets all placeable items as a read-only list.
        /// Useful for UI to iterate through all available items.
        /// </summary>
        /// <returns>Read-only list of all placeable items</returns>
        public IReadOnlyList<PlaceableItem> GetAllPlaceableItems()
        {
            return placeableItems.AsReadOnly();
        }

        /// <summary>
        /// Gets a placeable item by its ID (alias for GetItemById for clarity).
        /// </summary>
        /// <param name="id">Unique identifier of the item</param>
        /// <returns>The item if found, null otherwise</returns>
        public PlaceableItem GetPlaceableItemById(string id)
        {
            return GetItemById(id);
        }

        #endregion

        #region Placement Logic

        /// <summary>
        /// Attempts to place the currently selected prop at the mouse cursor position.
        /// </summary>
        private void TryPlaceProp()
        {
            // Check if mouse is over UI - prevent placing props when clicking UI buttons
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Check if Heart Power targeting is active - prevent placing props during targeting
            if (IsHeartPowerTargetingActive())
            {
                return;
            }

            // Early exit if no items configured - avoid spam warnings
            if (placeableItems.Count == 0)
            {
                return; // Already warned in Start()
            }

            // Validate preconditions
            if (currentSelection == null)
            {
                return;
            }

            if (mainCamera == null || mazeGridBehaviour == null)
            {
                return;
            }

            if (currentSelection.prefab == null)
            {
                return;
            }

            // Get mouse position using new Input System
            if (Mouse.current == null)
            {
                return;
            }

            // Get mouse position in world space
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mouseWorldPos.z = 0; // Ensure z is 0 for 2D

            // Check if position is already occupied
            if (IsPositionOccupied(mouseWorldPos))
            {
                return;
            }

            // Check if position is walkable (if required)
            if (currentSelection.requiresWalkable)
            {
                if (!mazeGridBehaviour.IsWalkableAtWorldPos(mouseWorldPos))
                {
                    return;
                }
            }

            // Try to spend essence
            if (GameController.Instance == null)
            {
                return;
            }

            if (!GameController.Instance.TrySpendEssence(currentSelection.essenceCost))
            {
                return;
            }

            // Place the prop
            PlaceProp(mouseWorldPos, currentSelection);
        }

        /// <summary>
        /// Places a prop at the specified world position.
        /// </summary>
        /// <param name="worldPos">World position where the prop should be placed</param>
        /// <param name="item">The placeable item to instantiate</param>
        private void PlaceProp(Vector3 worldPos, PlaceableItem item)
        {
            // Instantiate the prop
            GameObject prop = Instantiate(item.prefab, worldPos, Quaternion.identity);
            prop.name = $"{item.id}_{worldPos.x:F1}_{worldPos.y:F1}";

            // Track prop placement for statistics
            if (Systems.GameStatsTracker.Instance != null)
            {
                Systems.GameStatsTracker.Instance.RecordPropPlaced(item.displayName);
            }

            // Track placed prop
            placedProps.Add(new PlacedProp(worldPos, prop, item.id));

            // Play placement sound
            SoundManager.Instance?.PlayLanternPlaced();
        }

        /// <summary>
        /// Removes a prop from tracking (useful if props are destroyed).
        /// </summary>
        /// <param name="worldPos">World position of the prop to remove</param>
        public void RemoveProp(Vector3 worldPos)
        {
            var propToRemove = placedProps.FirstOrDefault(p =>
                Vector3.Distance(p.WorldPosition, worldPos) < occupancyRadius);

            if (propToRemove != null)
            {
                placedProps.Remove(propToRemove);
            }
        }

        /// <summary>
        /// Checks if a position is occupied by a prop.
        /// </summary>
        /// <param name="worldPos">World position to check</param>
        /// <returns>True if occupied, false otherwise</returns>
        public bool IsPositionOccupied(Vector3 worldPos)
        {
            return placedProps.Any(p =>
                Vector3.Distance(p.WorldPosition, worldPos) < occupancyRadius);
        }

        /// <summary>
        /// Gets the prop GameObject at the specified world position.
        /// </summary>
        /// <param name="worldPos">World position to query</param>
        /// <returns>The prop GameObject if found, null otherwise</returns>
        public GameObject GetPropAt(Vector3 worldPos)
        {
            var prop = placedProps.FirstOrDefault(p =>
                Vector3.Distance(p.WorldPosition, worldPos) < occupancyRadius);
            return prop?.PropObject;
        }

        #endregion

        #region Preview Management

        /// <summary>
        /// Creates or updates the preview instance for the currently selected item.
        /// </summary>
        private void CreateOrUpdatePreview()
        {
            if (currentSelection == null)
            {
                HidePreview();
                return;
            }

            // Create preview instance if it doesn't exist
            if (previewInstance == null)
            {
                previewInstance = new GameObject("PlacementPreview");
                previewInstance.transform.SetParent(previewRoot);

                // Add SpriteRenderer
                previewSpriteRenderer = previewInstance.AddComponent<SpriteRenderer>();
                previewSpriteRenderer.sortingOrder = 100; // High order to render on top

                // Create radius indicator as child
                GameObject radiusObj = new GameObject("RadiusIndicator");
                radiusObj.transform.SetParent(previewInstance.transform);
                radiusObj.transform.localPosition = Vector3.zero;

                previewRadiusRenderer = radiusObj.AddComponent<LineRenderer>();
                ConfigureRadiusRenderer();
            }

            // Determine sprite to use
            Sprite sprite = currentSelection.previewSprite;
            if (sprite == null && currentSelection.prefab != null)
            {
                // Try to get sprite from prefab's SpriteRenderer
                SpriteRenderer prefabSpriteRenderer = currentSelection.prefab.GetComponent<SpriteRenderer>();
                if (prefabSpriteRenderer != null)
                {
                    sprite = prefabSpriteRenderer.sprite;
                }
            }

            if (previewSpriteRenderer != null)
            {
                previewSpriteRenderer.sprite = sprite;
                previewSpriteRenderer.color = currentSelection.previewColor;
            }

            // Determine radius from MazeAttractor if present
            previewRadius = 0f;
            if (currentSelection.prefab != null)
            {
                MazeAttractor attractor = currentSelection.prefab.GetComponent<MazeAttractor>();
                if (attractor != null)
                {
                    previewRadius = attractor.Radius;
                }
            }

            UpdateRadiusVisualization();
        }

        /// <summary>
        /// Configures the LineRenderer for the radius indicator.
        /// </summary>
        private void ConfigureRadiusRenderer()
        {
            if (previewRadiusRenderer == null) return;

            previewRadiusRenderer.loop = true;
            previewRadiusRenderer.useWorldSpace = false;
            previewRadiusRenderer.startWidth = 0.05f;
            previewRadiusRenderer.endWidth = 0.05f;
            previewRadiusRenderer.material = new Material(Shader.Find("Sprites/Default"));
            previewRadiusRenderer.startColor = new Color(1f, 1f, 1f, 0.3f);
            previewRadiusRenderer.endColor = new Color(1f, 1f, 1f, 0.3f);
            previewRadiusRenderer.sortingOrder = 99;
        }

        /// <summary>
        /// Updates the radius visualization circle.
        /// </summary>
        private void UpdateRadiusVisualization()
        {
            if (previewRadiusRenderer == null || previewRadius <= 0f)
            {
                if (previewRadiusRenderer != null)
                {
                    previewRadiusRenderer.enabled = false;
                }
                return;
            }

            previewRadiusRenderer.enabled = true;

            // Create circle points
            int segments = 32;
            previewRadiusRenderer.positionCount = segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * 2f * Mathf.PI;
                float x = Mathf.Cos(angle) * previewRadius;
                float y = Mathf.Sin(angle) * previewRadius;
                previewRadiusRenderer.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        /// <summary>
        /// Updates the preview position to follow the mouse cursor.
        /// </summary>
        private void UpdatePreviewPosition()
        {
            // Hide preview if no selection or mouse is over UI
            if (currentSelection == null || previewInstance == null)
            {
                HidePreview();
                return;
            }

            // Check if mouse is over UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HidePreview();
                return;
            }

            // Check if Heart Power targeting is active - hide preview during targeting
            if (IsHeartPowerTargetingActive())
            {
                HidePreview();
                return;
            }

            // Ensure we have required components
            if (mainCamera == null || mazeGridBehaviour == null || Mouse.current == null)
            {
                HidePreview();
                return;
            }

            // Get mouse position in world space
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mouseWorldPos.z = 0f;

            // Check if position is valid for placement
            bool isValid = IsPositionValidForPlacement(mouseWorldPos);
            isPreviewValid = isValid;

            // Position preview at mouse position
            previewInstance.transform.position = mouseWorldPos;

            // Show preview
            if (!previewInstance.activeSelf)
            {
                previewInstance.SetActive(true);
            }

            // Update color based on validity
            if (previewSpriteRenderer != null)
            {
                previewSpriteRenderer.color = isValid ? currentSelection.previewColor : invalidPlacementColor;
            }

            // Update radius indicator color
            if (previewRadiusRenderer != null)
            {
                Color radiusColor = isValid ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f, 0.3f);
                previewRadiusRenderer.startColor = radiusColor;
                previewRadiusRenderer.endColor = radiusColor;
            }
        }

        /// <summary>
        /// Checks if a position is valid for placement using world-space checks.
        /// </summary>
        private bool IsPositionValidForPlacement(Vector3 worldPos)
        {
            if (currentSelection == null || mazeGridBehaviour == null)
            {
                return false;
            }

            // Check if position is occupied
            if (IsPositionOccupied(worldPos))
            {
                return false;
            }

            // Check if position is walkable (if required)
            if (currentSelection.requiresWalkable)
            {
                if (!mazeGridBehaviour.IsWalkableAtWorldPos(worldPos))
                {
                    return false;
                }
            }

            // Check max per maze constraint
            if (currentSelection.maxPerMaze > 0)
            {
                int currentCount = CountPlacedItemsOfType(currentSelection.id);
                if (currentCount >= currentSelection.maxPerMaze)
                {
                    return false;
                }
            }

            // Check minimum distance constraint
            if (currentSelection.minDistanceBetweenProps > 0)
            {
                if (!IsMinDistanceSatisfied(worldPos, currentSelection.id, currentSelection.minDistanceBetweenProps))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Counts how many items of a specific type have been placed.
        /// </summary>
        private int CountPlacedItemsOfType(string itemId)
        {
            return placedProps.Count(p => p.ItemId == itemId);
        }

        /// <summary>
        /// Checks if the minimum distance constraint is satisfied.
        /// </summary>
        private bool IsMinDistanceSatisfied(Vector3 worldPos, string itemId, float minDistance)
        {
            foreach (var prop in placedProps)
            {
                // Only check distance to same item type
                if (prop.ItemId == itemId)
                {
                    float distance = Vector3.Distance(worldPos, prop.WorldPosition);
                    if (distance < minDistance)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Hides the preview instance.
        /// </summary>
        private void HidePreview()
        {
            if (previewInstance != null && previewInstance.activeSelf)
            {
                previewInstance.SetActive(false);
            }
        }

        #endregion

        #region Build Mode Management

        /// <summary>
        /// Enters build mode, enabling placement and showing custom cursor.
        /// </summary>
        public void EnterBuildMode()
        {
            buildModeState = BuildModeState.Active;
            UpdateCursor();
        }

        /// <summary>
        /// Exits build mode, clearing selection and restoring default cursor.
        /// </summary>
        public void ExitBuildMode()
        {
            buildModeState = BuildModeState.Inactive;
            ClearSelection();
            UpdateCursor();
        }

        /// <summary>
        /// Clears the current selection and hides the preview.
        /// </summary>
        public void ClearSelection()
        {
            currentSelection = null;
            HidePreview();
        }

        /// <summary>
        /// Updates the cursor based on build mode state.
        /// Handles null textures gracefully by using system default.
        /// </summary>
        private void UpdateCursor()
        {
            if (buildModeState == BuildModeState.Active && buildCursorTexture != null)
            {
                Cursor.SetCursor(buildCursorTexture, buildCursorHotspot, CursorMode.Auto);
            }
            else
            {
                // Set to default cursor (null = system default)
                Cursor.SetCursor(defaultCursorTexture, defaultCursorHotspot, CursorMode.Auto);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (placedProps == null)
                return;

            // Draw placed props
            Gizmos.color = Color.red;
            foreach (var prop in placedProps)
            {
                Gizmos.DrawWireSphere(prop.WorldPosition, occupancyRadius);
            }

            // Draw current selection info
            if (currentSelection != null)
            {
                Gizmos.color = Color.cyan;
                // Could draw selection indicator near mouse position in future
            }
        }

        #endregion
    }
}
