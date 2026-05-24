namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// Named weapon module definitions.
    /// Pattern mirrors EngineModuleDefinitions / PowerModuleDefinitions.
    ///
    /// A weapon's "feel" is almost entirely controlled by its config:
    ///   fast + short-lived       → pulse laser
    ///   slow + long-lived        → missile
    ///   moderate + medium-lived  → plasma bolt
    ///   fast + very short        → point-defense
    /// </summary>
    public static class WeaponModuleDefinitions
    {
        /// <summary>
        /// Sample laser cannon — the default weapon on a new player ship.
        /// Hits like light damage at medium range. High muzzle speed gives it
        /// an instant-hit feel at typical engagement distances.
        /// </summary>
        public static WeaponModuleConfig LaserCannon => new()
        {
            Name                      = "laser_cannon",
            DisplayName               = "Light Laser Cannon",
            Description               = "A standard-issue pulsed laser. Light, cheap, forgiving.",
            SlotSize                  = 2,
            Mass                      = 2f,
            Range                     = 300f,
            MuzzleSpeed               = 200f,
            ProjectileLifetimeSeconds = 1.5f,   // 200 × 1.5 = 300 unit effective reach
            ProjectileKind            = "missile",   // reuses renderer's red missile colour
            Damage                    = 5f,
            EnergyPerShot             = 2f,
        };

        /// <summary>
        /// Heavier, slower kinetic weapon. Stub for later tuning.
        /// </summary>
        public static WeaponModuleConfig RailGun => new()
        {
            Name                      = "rail_gun",
            DisplayName               = "Coilgun",
            Description               = "A high-mass kinetic launcher. Hits hard, fires slow.",
            SlotSize                  = 4,
            Mass                      = 8f,
            Range                     = 500f,
            MuzzleSpeed               = 350f,
            ProjectileLifetimeSeconds = 1.8f,
            ProjectileKind            = "missile",
            Damage                    = 25f,
            EnergyPerShot             = 12f,
        };
    }
}
