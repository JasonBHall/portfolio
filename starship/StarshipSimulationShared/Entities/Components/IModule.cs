namespace StarshipSimulation.Shared.Entities.Components
{
    /// <summary>
    /// A physical, tangible item that snaps into an entity's hardpoint.
    /// 
    /// Modules are the player-facing layer of the component system.
    /// They can be seen, moved, bought, sold, lost, repaired, and upgraded.
    /// Examples: engine modules, weapon modules, cargo pods, reactors,
    ///           sensor arrays, repair bays, veteran crew quarters.
    ///
    /// All modules are components — but not all components are modules.
    /// Internal simulation state (physics, attrition, player control) uses
    /// plain IComponent. Anything a player physically installs uses IModule.
    ///
    /// Hardpoint fitting rules:
    ///   SlotType must match the hardpoint's accepted type.
    ///   SlotSize must fit within the hardpoint's available space.
    ///   Mass contributes to the entity's total mass (affects physics).
    /// </summary>
    public interface IModule : IComponent
    {
        // ------------------------------------------------------------
        // Identity — player-facing
        // ------------------------------------------------------------

        /// <summary>
        /// In-game display name shown to players.
        /// e.g. "Dothraki Horse Drive Mk.II", "Helios Fusion Reactor"
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Lore/flavour description shown in the inventory UI.
        /// </summary>
        string Description { get; }

        // ------------------------------------------------------------
        // Physical inventory properties
        // ------------------------------------------------------------

        /// <summary>
        /// Hardpoint slot type this module requires.
        /// e.g. "engine", "weapon", "reactor", "cargo", "utility", "sensor"
        /// The hardpoint's accepted type must match for installation.
        /// </summary>
        string SlotType { get; }

        /// <summary>
        /// How many hardpoint grid squares this module occupies.
        /// Larger ships have larger hardpoints with more squares.
        /// e.g. 1 = tiny utility, 4 = standard engine, 9 = capital drive
        /// </summary>
        int SlotSize { get; }

        /// <summary>
        /// Mass in simulation units. Contributes to the entity's total mass.
        /// Heavier modules reduce acceleration. This is the cost of capability.
        /// </summary>
        float Mass { get; }

        // ------------------------------------------------------------
        // Installation state
        // ------------------------------------------------------------

        /// <summary>
        /// True when snapped into a hardpoint and active in the simulation.
        /// False when in cargo, in a station inventory, or uninstalled.
        /// </summary>
        bool IsInstalled { get; set; }

        /// <summary>
        /// The hardpoint Id this module is currently installed in.
        /// Null if not installed.
        /// </summary>
        string? InstalledInHardpoint { get; set; }

        // ------------------------------------------------------------
        // Condition
        // ------------------------------------------------------------

        /// <summary>
        /// 0.0 (destroyed) to 1.0 (pristine). Affects module effectiveness.
        /// Damaged modules produce reduced output. At 0 they stop working.
        /// Repairable by repair modules, tenders, or at a station.
        /// </summary>
        float Condition { get; set; }

        /// <summary>
        /// Whether this module is powered on.
        /// An offline module draws no power but provides no capability.
        /// Players can take modules offline to manage power budgets.
        /// </summary>
        bool IsOnline { get; set; }
    }
}
