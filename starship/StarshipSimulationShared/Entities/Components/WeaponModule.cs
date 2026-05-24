namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// A weapon that snaps into a weapon hardpoint.
    ///
    /// PURE DATA COMPONENT — holds configuration (range, muzzle speed,
    /// projectile kind, lifetime) and one transient flag (WantsToFire).
    /// The firing behaviour itself lives in WeaponSystem, since projectile
    /// spawning requires a UniverseService reference which the Shared
    /// project cannot take on (Shared → Server is forbidden).
    ///
    /// Generic projectile launcher — same framework handles laser bolts,
    /// missiles, torpedoes, and fighter launches. What comes out is
    /// controlled entirely by config:
    ///   ProjectileKind            — display kind ("missile", "fighter", ...)
    ///   MuzzleSpeed               — velocity added along fire heading
    ///   ProjectileLifetimeSeconds — how long the shot lives
    ///   Range                     — max targeting range for lock-on aim
    ///
    /// Fire flow:
    ///   1. CommandHandler.HandleFire sets WantsToFire = true on every
    ///      installed weapon when SPACE is pressed.
    ///   2. WeaponSystem.Tick iterates and calls its own Fire() for
    ///      each weapon with WantsToFire = true.
    ///   3. WeaponSystem spawns the projectile entity and clears the flag.
    ///   4. MovementSystem drifts the projectile.
    ///   5. ProjectileSystem expires it.
    ///
    /// No cooldown yet. Easy to add as LastFiredAtUtc + CooldownSeconds later.
    /// </summary>
    public class WeaponModule : ModuleBase
    {
        // ------------------------------------------------------------
        // IModule — identity
        // ------------------------------------------------------------

        public override string Name        { get; }
        public override string DisplayName { get; }
        public override string Description { get; }
        public override string SlotType    => "weapon";
        public override int    SlotSize    { get; }
        public override float  Mass        { get; }

        // ------------------------------------------------------------
        // Weapon capabilities — immutable after construction
        // ------------------------------------------------------------

        /// <summary>Max engagement range in world units.</summary>
        public float Range { get; }

        /// <summary>Projectile velocity magnitude (added to ship velocity).</summary>
        public float MuzzleSpeed { get; }

        /// <summary>Projectile lifetime — ProjectileSystem reaps after this.</summary>
        public float ProjectileLifetimeSeconds { get; }

        /// <summary>Display kind for the spawned projectile entity.</summary>
        public string ProjectileKind { get; }

        /// <summary>Nominal damage value — not applied yet.</summary>
        public float Damage { get; }

        /// <summary>Energy drawn when fired — not enforced yet.</summary>
        public float EnergyPerShot { get; }

        // ------------------------------------------------------------
        // Transient input state — written by CommandHandler, consumed by WeaponSystem
        // ------------------------------------------------------------

        /// <summary>
        /// Set to true by CommandHandler when the pilot presses SPACE.
        /// WeaponSystem reads, fires, and resets to false in the same tick.
        /// </summary>
        public bool WantsToFire { get; set; }

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------

        public WeaponModule(WeaponModuleConfig config)
        {
            Name        = config.Name;
            DisplayName = config.DisplayName;
            Description = config.Description;
            SlotSize    = config.SlotSize;
            Mass        = config.Mass;

            Range                     = config.Range;
            MuzzleSpeed               = config.MuzzleSpeed;
            ProjectileLifetimeSeconds = config.ProjectileLifetimeSeconds;
            ProjectileKind            = config.ProjectileKind;
            Damage                    = config.Damage;
            EnergyPerShot             = config.EnergyPerShot;
        }
    }

    // ------------------------------------------------------------
    // Config record — used to define weapon module types
    // ------------------------------------------------------------

    public record WeaponModuleConfig
    {
        public string Name        { get; init; } = "weapon_module";
        public string DisplayName { get; init; } = "Weapon Module";
        public string Description { get; init; } = string.Empty;
        public int    SlotSize    { get; init; } = 2;
        public float  Mass        { get; init; } = 3f;

        public float  Range                     { get; init; } = 250f;
        public float  MuzzleSpeed               { get; init; } = 120f;
        public float  ProjectileLifetimeSeconds { get; init; } = 2f;
        public string ProjectileKind            { get; init; } = "missile";
        public float  Damage                    { get; init; } = 0f;
        public float  EnergyPerShot             { get; init; } = 0f;
    }
}
