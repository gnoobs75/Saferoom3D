namespace SafeRoom3D.Core;

/// <summary>
/// Game-wide constants for 3D version. Values adapted from 2D for 3D scale.
/// 2D → 3D conversion: 1 pixel ≈ 0.0625 units (16 pixels = 1 unit)
/// Tile size: 1 unit = 1 meter
/// </summary>
public static class Constants
{
    // ===================
    // SCALE CONVERSION
    // ===================
    /// <summary>3D units per 2D pixel (16 pixels = 1 meter)</summary>
    public const float PixelToUnit = 0.0625f;
    /// <summary>Tile size in 3D units (1 meter)</summary>
    public const float TileSize = 1f;
    /// <summary>Wall height in units</summary>
    public const float WallHeight = 3f;
    /// <summary>Player eye height</summary>
    public const float PlayerEyeHeight = 1.7f;

    // ===================
    // PLAYER MOVEMENT (FPS)
    // ===================
    public const float PlayerMoveSpeed = 5f;        // 5 m/s walking
    public const float PlayerSprintSpeed = 8f;      // 8 m/s sprinting
    public const float PlayerAcceleration = 20f;
    public const float PlayerFriction = 15f;
    public const float PlayerJumpVelocity = 6f;     // Jump height ~1.8m
    public const float Gravity = 20f;               // Slightly higher than Earth for snappier feel
    public const float MouseSensitivity = 0.002f;   // Radians per pixel
    public const float MaxLookAngle = 85f;          // Degrees up/down

    // ===================
    // MELEE COMBAT
    // ===================
    public const float AttackCooldown = 0.4f;
    public const float AttackRange = 2.5f;          // 2.5 meters (arm's reach + weapon)
    public const float AttackDamage = 25f;
    public const float AttackWindup = 0.1f;
    public const float AttackArc = 90f;             // Degrees horizontal swing arc

    // ===================
    // RANGED COMBAT
    // ===================
    public const float RangedCooldown = 0.6f;
    public const float RangedDamage = 15f;
    public const float ProjectileSpeed = 25f;       // 25 m/s
    public const float ProjectileLifetime = 2.0f;

    // ===================
    // HIT FEEDBACK
    // ===================
    public const float ScreenShakeDuration = 0.1f;
    public const float ScreenShakeIntensity = 0.05f; // Radians of camera shake
    public const float HitStopDuration = 0.05f;
    public const float KnockbackForce = 8f;          // 8 m/s impulse
    public const float DefaultScreenShakeDuration = 0.15f;
    public const float DefaultScreenShakeIntensity = 0.08f;

    // ===================
    // CAMERA
    // ===================
    public const float CameraSmoothSpeed = 8f;
    public const float CameraFov = 75f;              // Field of view in degrees
    public const float CameraNear = 0.1f;
    public const float CameraFar = 100f;

    // ===================
    // PLAYER STATS
    // ===================
    public const int StartingHealth = 1000;
    public const int StartingMana = 100;
    public const int StartingExperience = 0;
    public const int ExperienceToLevel = 1000;

    // ===================
    // FLOOR TIMER (POC)
    // ===================
    public const float FloorTimerSeconds = 300f;

    // ===================
    // ENEMY STATS
    // ===================
    public const float EnemyMoveSpeed = 3f;          // 3 m/s
    public const int EnemyHealth = 75;
    public const float EnemyDamage = 10f;
    public const float EnemyAttackCooldown = 1.0f;
    public const float EnemyMeleeRange = 2f;         // 2 meters

    // ===================
    // ENEMY AI
    // ===================
    public const float EnemyAggroRange = 15f;        // 15 meters
    public const float EnemyDeaggroRange = 25f;      // 25 meters
    public const float EnemyPatrolRadius = 8f;       // 8 meters
    public const float EnemyPatrolSpeed = 2f;        // 2 m/s patrol
    public const float EnemyPatrolWaitTime = 1.5f;
    public const float EnemyTrackingSpeed = 1.5f;    // Slow tracking when not aggro'd

    // ===================
    // UI
    // ===================
    public const float DamageNumberDuration = 1.0f;
    public const float DamageNumberRiseSpeed = 2f;   // 2 m/s upward

    // ===================
    // DUNGEON GENERATION
    // ===================
    public const int MinRoomSize = 5;                // 5x5 meters minimum
    public const int MaxRoomSize = 15;               // 15x15 meters maximum
    public const int MinRooms = 5;
    public const int MaxRooms = 12;
    public const int CorridorWidth = 3;              // 3 meters wide
    public const int DungeonWidth = 50;              // 50x50 meter dungeon
    public const int DungeonHeight = 50;

    // ===================
    // LIGHTING
    // ===================
    public const float TorchRange = 8f;              // 8 meter light radius
    public const float TorchEnergy = 2f;
    public const float AmbientLight = 0.05f;         // Very dark ambient
    public const float TorchFlickerSpeed = 8f;
    public const float TorchFlickerAmount = 0.15f;

    // ===================
    // COSMETICS (3D Props)
    // ===================
    public const float BarrelRadius = 0.4f;          // 40cm radius
    public const float BarrelHeight = 0.9f;          // 90cm tall
    public const float CrateSize = 0.8f;             // 80cm cube
    public const float PotRadius = 0.25f;            // 25cm radius
    public const float PotHeight = 0.4f;             // 40cm tall
    public const int LowPolySegments = 12;           // Cylinder segments for low-poly look

    // ===================
    // LOD SYSTEM
    // ===================
    /// <summary>LOD0 (Full detail): 0-15 meters</summary>
    public const float LOD0Distance = 15f;
    /// <summary>LOD1 (Medium detail): 15-30 meters</summary>
    public const float LOD1Distance = 30f;
    /// <summary>LOD2 (Low detail): 30-50 meters</summary>
    public const float LOD2Distance = 50f;
    /// <summary>LOD3 (Minimal/Culled): 50+ meters</summary>
    public const float LOD3Distance = 50f;
    /// <summary>Maximum render distance (culling distance)</summary>
    public const float MaxRenderDistance = 80f;
    /// <summary>Update LOD levels every 0.5 seconds</summary>
    public const float LODUpdateInterval = 0.5f;
    /// <summary>Player must move this far to trigger LOD updates</summary>
    public const float LODUpdateThreshold = 2f;
    /// <summary>Grid cell size for spatial partitioning</summary>
    public const float LODGridCellSize = 20f;

    // ===================
    // VISIBILITY CULLING (Draw Distance)
    // ===================
    /// <summary>Distance at which props become visible (fade in start)</summary>
    public const float PropVisibilityBegin = 0f;
    /// <summary>Distance at which props are culled (fade out end)</summary>
    public const float PropVisibilityEnd = 60f;
    /// <summary>Distance at which lights become visible</summary>
    public const float LightVisibilityBegin = 0f;
    /// <summary>Distance at which lights are culled</summary>
    public const float LightVisibilityEnd = 50f;
    /// <summary>Distance at which torches become visible</summary>
    public const float TorchVisibilityBegin = 0f;
    /// <summary>Distance at which torches are culled</summary>
    public const float TorchVisibilityEnd = 40f;
    /// <summary>Distance at which dungeon geometry becomes visible</summary>
    public const float GeometryVisibilityBegin = 0f;
    /// <summary>Distance at which dungeon geometry is culled</summary>
    public const float GeometryVisibilityEnd = 100f;
    /// <summary>Margin to add to visibility begin for fade effect</summary>
    public const float VisibilityFadeMargin = 5f;
}
