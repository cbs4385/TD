# PropPlacementController - Multi-Item Placement System

## Overview

The **PropPlacementController** has been refactored to support placement of multiple prop types (FaeLantern, FairyRing, etc.) instead of being hardcoded to a single prop type.

## Changes from Previous Version

### Before (Hardcoded):
- Fixed `faeLanternPrefab` field
- Fixed `faeLanternCost` field
- Method: `TryPlaceLantern()`
- Only supported FaeLantern placement

### After (Generic Multi-Item):
- **PlaceableItem** data structure with configurable properties
- List of **placeableItems** supporting multiple prop types
- **currentSelection** tracking which item is active
- Method: `TryPlaceProp()` - works with any placeable item
- **SelectItemById(string)** for UI integration
- Default selection for backward compatibility

## PlaceableItem Data Structure

```csharp
[System.Serializable]
public class PlaceableItem
{
    public string id;              // Unique identifier (e.g., "FaeLantern")
    public string displayName;     // Display name for UI (e.g., "Fae Lantern")
    public GameObject prefab;      // Prefab to instantiate
    public int essenceCost;        // Essence cost to place
}
```

## Unity Editor Setup

### 1. Configure Placeable Items List

1. Select the GameObject with **PropPlacementController** component
2. In the Inspector, find **Placeable Items** section
3. Set **Size** to the number of item types (e.g., 2 for FaeLantern + FairyRing)
4. Configure each element:

#### Element 0: FaeLantern
- **Id**: `FaeLantern`
- **Display Name**: `Fae Lantern`
- **Prefab**: Drag FaeLantern prefab from `Assets/Prefabs/Props/`
- **Essence Cost**: `20` (or your preferred cost)

#### Element 1: FairyRing
- **Id**: `FairyRing`
- **Display Name**: `Fairy Ring`
- **Prefab**: Drag FairyRing prefab from `Assets/Prefabs/Props/`
- **Essence Cost**: `15` (or your preferred cost)

### 2. Assign MazeGridBehaviour Reference

In the **References** section:
- **Maze Grid Behaviour**: Drag the GameObject with MazeGridBehaviour component

### 3. Default Selection

The first item in the **Placeable Items** list becomes the default selection on Start(). This ensures backward compatibility - if you configured FaeLantern as Element 0, left-click placement will work as before.

## Public API for UI Integration

### Selection Methods

```csharp
// Select an item by its ID (call this from UI buttons)
void SelectItemById(string id)

// Get current selection
PlaceableItem GetCurrentSelection()

// Get an item by ID
PlaceableItem GetItemById(string id)

// Access the full list
List<PlaceableItem> PlaceableItems { get; }
```

### Example UI Integration

```csharp
// From a UI button click handler:
void OnLanternButtonClicked()
{
    propPlacementController.SelectItemById("FaeLantern");
}

void OnFairyRingButtonClicked()
{
    propPlacementController.SelectItemById("FairyRing");
}
```

### Temporary Debug Selection

For testing different items before UI is built:

```csharp
// In some debug script's Update():
if (Keyboard.current.digit1Key.wasPressedThisFrame)
{
    propPlacementController.SelectItemById("FaeLantern");
}

if (Keyboard.current.digit2Key.wasPressedThisFrame)
{
    propPlacementController.SelectItemById("FairyRing");
}
```

## Placement Behavior

### Validation Checks (in order):
1. **Current selection exists** - Must have selected an item
2. **Prefab assigned** - Selected item must have a valid prefab
3. **Mouse in bounds** - Click must be within maze grid bounds
4. **Tile not occupied** - Cannot place on already occupied tiles
5. **Node exists** - Grid must have a valid node at position
6. **Tile is walkable** - Can only place on walkable path tiles
7. **Essence available** - Player must have enough essence

### Placement Process:
1. Deduct essence cost from player
2. Instantiate prefab at grid position
3. Name GameObject: `{itemId}_{gridX}_{gridY}`
4. Mark tile as occupied in occupancy dictionary
5. Play placement sound
6. Prop components (MazeAttractor, FairyRing, etc.) auto-initialize

## Occupancy Tracking

The system maintains a `Dictionary<Vector2Int, GameObject>` to track occupied tiles:

```csharp
// Check if a tile is occupied
bool IsTileOccupied(Vector2Int gridPos)

// Get the prop at a position
GameObject GetPropAt(Vector2Int gridPos)

// Remove a prop from tracking (if destroyed)
void RemoveProp(Vector2Int gridPos)
```

This prevents placing multiple props on the same tile.

## Backward Compatibility

✅ **Preserved Behaviors**:
- Left-click placement still works
- Essence cost checking preserved
- Walkable tile validation preserved
- Occupancy tracking preserved
- Position snapping to grid preserved
- Sound effects preserved

✅ **Default Selection**:
- First item in list is auto-selected on Start()
- If you configure FaeLantern as Element 0, behavior is identical to before

✅ **No Breaking Changes**:
- All existing validation logic preserved
- GameController.TrySpendEssence() still used
- SoundManager.PlayLanternPlaced() still called
- MazeGridBehaviour integration unchanged

## Debug Logging

The refactored system includes helpful debug logs:

```
// On startup
"PropPlacementController: Default selection set to 'Fae Lantern'"

// When selecting items
"PropPlacementController: Selected 'Fairy Ring' (cost: 15)"

// On successful placement
"PropPlacementController: Placed 'Fae Lantern' at (5, 10)"

// On validation failures
"PropPlacementController: Tile (5, 10) is already occupied"
"PropPlacementController: Not enough essence (need 20)"
"PropPlacementController: Tile (3, 7) is not walkable"
```

## Testing Checklist

### Basic Functionality
- [ ] Configure at least 2 PlaceableItems (FaeLantern + FairyRing)
- [ ] First item auto-selected on Start()
- [ ] Left-click places default item (first in list)
- [ ] Essence is deducted correctly
- [ ] Tile occupancy prevents duplicates
- [ ] Only walkable tiles can be used
- [ ] Props appear at correct grid positions

### Multi-Item Selection
- [ ] SelectItemById("FaeLantern") switches to lantern
- [ ] SelectItemById("FairyRing") switches to fairy ring
- [ ] Invalid IDs log warning
- [ ] GetCurrentSelection() returns correct item
- [ ] Essence costs differ per item type
- [ ] Both item types can be placed successfully

### Edge Cases
- [ ] Empty placeableItems list logs warning
- [ ] Null prefab logs error, blocks placement
- [ ] Click outside grid bounds does nothing
- [ ] Click on wall tiles does nothing
- [ ] Click on occupied tiles does nothing
- [ ] Insufficient essence blocks placement

### Validation Preserved
- [ ] Cannot place on walls
- [ ] Cannot place on already occupied tiles
- [ ] Cannot place without enough essence
- [ ] Placement sound plays on success
- [ ] Gizmos show occupied tiles (red spheres)

## Future UI Integration

When building the placement UI:

1. **Create Item Selection Buttons**:
   - One button per PlaceableItem
   - Display item.displayName and item.essenceCost
   - OnClick: call `SelectItemById(item.id)`

2. **Show Current Selection**:
   - Highlight the active button
   - Display current item's cost
   - Show essence remaining vs. cost

3. **Disable Unavailable Items**:
   - Disable buttons when essence < item.essenceCost
   - Gray out unaffordable items
   - Show tooltip with cost info

4. **Visual Feedback**:
   - Show selected item icon near cursor
   - Highlight valid placement tiles
   - Show red X on invalid tiles

## Example Configuration

Here's a suggested setup for two items:

### FaeLantern
- **ID**: `FaeLantern`
- **Display Name**: `Fae Lantern`
- **Essence Cost**: `20`
- **Effect**: Strong attraction, slows visitors
- **Use Case**: Primary defense placement

### FairyRing
- **ID**: `FairyRing`
- **Display Name**: `Fairy Ring`
- **Essence Cost**: `15`
- **Effect**: Entrances and slows visitors
- **Use Case**: Secondary crowd control

## Code Reference

### Key Methods

**Selection**:
- `SelectItemById(string id)` - Switch active item
- `GetCurrentSelection()` - Query active item
- `GetItemById(string id)` - Lookup by ID

**Placement**:
- `TryPlaceProp()` - Main placement logic (private)
- `PlaceProp(Vector2Int, PlaceableItem)` - Instantiation (private)

**Occupancy**:
- `IsTileOccupied(Vector2Int)` - Check if tile has prop
- `GetPropAt(Vector2Int)` - Get prop GameObject
- `RemoveProp(Vector2Int)` - Clear occupancy

### Properties

- `PlaceableItem CurrentSelection` - Get active selection
- `List<PlaceableItem> PlaceableItems` - Get full item list

## Acceptance Criteria Status

✅ **PropPlacementController no longer hardcodes FaeLantern**
- Uses PlaceableItem list instead

✅ **Way to select item by string ID**
- SelectItemById(string) implemented
- Ready for UI integration

✅ **Default selection maintains backward compatibility**
- First item auto-selected on Start()
- Left-click placement works as before

✅ **Both FaeLantern and FairyRing can be placed**
- Generic placement system supports any prefab
- Different costs per item type
- Selection changeable at runtime

✅ **All existing behaviors preserved**
- Essence checking
- Tile validation
- Occupancy tracking
- Position snapping
- Sound effects

## Summary

The PropPlacementController is now a **generic multi-item placement system**:

1. **Configure items in Inspector** - Add any number of placeable props
2. **Default selection** - First item auto-selected (backward compatible)
3. **Selection API** - `SelectItemById()` ready for UI buttons
4. **Generic placement** - Works with any prop type
5. **All validation preserved** - Essence, occupancy, walkability
6. **Ready for UI** - Clean API for future integration

Next steps: Build UI buttons that call `SelectItemById()` to switch between FaeLantern and FairyRing!
