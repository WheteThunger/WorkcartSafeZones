## Features

- Allows creating mobile safe zones on workcarts (can be automatic)
- Allows spawning NPC auto turrets on workcarts with the safe zone
- Prevents damage to workcarts that have safe zones

## Permissions

- `workcartsafezones.use` -- Required to use the `safecart.add` and `safecart.remove` commands

## Commands

- `safecart.add` -- Adds a safe zone to the workcart you are aiming at.
- `safecart.remove` -- Removes the safe zone from the workcart you are aiming at.

These commands will also find the workcart you are standing on or mounted on if you aren't aiming at one nearby.

## Configuration

Default configuration:

```json
{
  "AutoZones": false,
  "SafeZoneRadius": 0,
  "DisarmOccupants": false,
  "EnableTurrets": false,
  "TurretPositions": [
    {
      "Position": {
        "x": 0.85,
        "y": 2.62,
        "z": 1.25
      },
      "RotationAngle": 180.0
    },
    {
      "Position": {
        "x": 0.7,
        "y": 3.84,
        "z": 3.7
      },
      "RotationAngle": 0.0
    }
  ]
}
```

- `AutoZones` (`true` or `false`) -- While `true`, all workcarts will automatically have safe zones; the `safecart.add` and `safecart.remove` commands will be disabled.
- `SafeZoneRadius` -- Radius of the safe zone around the workcart.
  - Set to `0` to apply the safe zone only to players standing on the workcart. Otherwise, a radius of at least `5` is recommended.
- `DisarmOccupants` (`true` or `false`) -- While `true`, players who board the workcart with a weapon drawn will automatically have it holstered. This effectively prevents them from attacking others while they are on board.
  - Note: This only applies to the workcart itself, even if you have extended the safe zone radius.
- `EnableTurrets` (`true` or `false`) -- Whether to spawn NPC auto turrets with workcart safe zones.
  - Note: It's recommended to enable the NPC auto turrets **only if** you have disabled AI or disabled tunnel dwellers from spawning, since fighting them will consider you hostile, causing the NPC auto turrets to attack you.
- `TurretPositions` -- List of turret positions relative to the workcart. Only applies when `EnableTurrets` is `true`.
  - Each entry in this list will cause a separate auto turret to be spawned (on every workcart that has a safe zone).

## Localization

```json
{
  "Error.NoPermission": "You don't have permission to do that.",
  "Error.NoWorkcartFound": "Error: No workcart found.",
  "Error.AutoZonesEnabled": "Error: You cannot do that while automatic zones are enabled.",
  "Error.SafeZonePresent": "That workcart already has a safe zone.",
  "Error.NoSafeZone": "That workcart doesn't have a safe zone.",
  "Add.Success": "Successfully added safe zone to the workcart.",
  "Remove.Success": "Successfully removed safe zone from the workcart.",
  "Warning.Hostile": "You are <color=red>hostile</color> for <color=red>{0}</color>. No safe zone protection."
}
```

## Developer API

#### API_CreateSafeZone

Plugins can call this API to create a safe zone on a workcart.

```csharp
bool API_CreateSafeZone(TrainEngine workcart)
```

- Returns `true` if a safe zone was successfully added, or if the workcart already had a safe zone
- Returns `false` if another plugin blocked it with the `OnWorkcartSafeZoneCreate` hook

## Developer Hooks

#### OnWorkcartSafeZoneCreate

- Called when a safe zone is about to be created on a workcart
- Returning `false` will prevent the safe zone from being created
- Returning `null` will result in the default behavior

```csharp
bool? OnWorkcartSafeZoneCreate(TrainEngine workcart)
```

#### OnWorkcartSafeZoneCreated

- Called after a safe zone has been created on a workcart
- No return behavior

```csharp
void OnWorkcartSafeZoneCreated(TrainEngine workcart)
```
