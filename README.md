## Features

- Allows creating safe zones on workcarts (can be automatic)
- Allows spawning NPC auto turrets alongside the safe zone
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
  "AllowDamageToHostileOccupants": true,
  "Turrets": [
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
- `AllowDamageToHostileOccupants` (`true` or `false`) -- While `false`, all players mounted on the workcart will be invulnerable, even if hostile.
- `SafeZoneRadius` -- Radius of the safe zone around the workcart.
  - Set to `0` to only apply the safe zone to players standing on the workcart. Otherwise, at least `5` is recommended.
- `Turrets` -- List of turret positions relative to the workcart.
  - Each entry in this list will cause a separate auto turret to be spawned (on every workcart that has a safe zone).
  - Set to `[]` to not spawn any auto turrets.

## Localization

```json
{
  "Error.NoPermission": "You don't have permission to do that.",
  "Error.NoWorkcartFound": "Error: No workcart found.",
  "Error.AutoZonesEnabled": "Error: You cannot do that while automatic zones are enabled.",
  "Error.SafeZonePresent": "That workcart already has a safe zone.",
  "Error.NoSafeZone": "That workcart doesn't have a safe zone.",
  "Add.Success": "Successfully added safe zone to the workcart.",
  "Remove.Success": "Successfully removed safe zone from the workcart."
}
```

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
