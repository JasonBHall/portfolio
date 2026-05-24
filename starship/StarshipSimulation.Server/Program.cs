using System.Numerics;
using StarshipSimulation.Server.Networking;
using StarshipSimulation.Server.Simulation;
using StarshipSimulation.Shared.Entities;
using StarshipSimulation.Shared.Entities.Components;
using StarshipSimulation.Shared.Entities.Orders;
using StarshipSimulation.Shared.Players;

// ------------------------------------------------------------
// Builder
// ------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Simulation engine — singleton, owns all entity state
builder.Services.AddSingleton<UniverseService>();

// Tick loop — drives micro and macro ticks
builder.Services.AddHostedService<UniverseTicker>();

// Narrative event broadcast — systems call Emit() on this
builder.Services.AddSingleton<NarrativeEventStream>();

// Player session management
builder.Services.AddSingleton<PlayerSessionStore>();

// CORS — allow Unity, React, and Renderer clients on any origin
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

// ------------------------------------------------------------
// App
// ------------------------------------------------------------

var app = builder.Build();

app.UseCors();
app.UseWebSockets();

// ------------------------------------------------------------
// /ws/universe  — tick stream out to clients
// ------------------------------------------------------------

app.Map("/ws/universe", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket   = await context.WebSockets.AcceptWebSocketAsync();
    var universe = context.RequestServices.GetRequiredService<UniverseService>();
    var stream   = new UniverseStream(universe);

    await stream.HandleAsync(socket, context.RequestAborted);
});

// ------------------------------------------------------------
// /ws/events  — narrative events out to clients
// ------------------------------------------------------------

app.Map("/ws/events", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket      = await context.WebSockets.AcceptWebSocketAsync();
    var eventStream = context.RequestServices.GetRequiredService<NarrativeEventStream>();

    await eventStream.HandleAsync(socket, context.RequestAborted);
});

// ------------------------------------------------------------
// /ws/commands  — player input in from clients
// ------------------------------------------------------------

app.Map("/ws/commands", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket   = await context.WebSockets.AcceptWebSocketAsync();
    var universe = context.RequestServices.GetRequiredService<UniverseService>();
    var sessions = context.RequestServices.GetRequiredService<PlayerSessionStore>();
    var handler  = new CommandHandler(universe, sessions);

    await handler.HandleAsync(socket, context.RequestAborted);
});

// ------------------------------------------------------------
// Debug seed — temporary, remove once spawn UI is working
// ------------------------------------------------------------

var sim = app.Services.GetRequiredService<UniverseService>();

// Helper — fit a ship with engine + power + stats


// ── Universe layout ───────────────────────────────────────────────
//
// NEAR REGION  (origin cluster)
//   Logistics entities start here
//   Iron Mine 1      (-800, -400)
//   Copper Mine 1    ( 600, -500)   [moved closer — copper feeds warheads]
//   Iron Smelter 1   (-500, -700)
//   Copper Smelter 1 ( 400, -700)
//   Fabricator 1     (  -100, -600)
//   Warhead Factory  ( 300, -600)   [NEW — copper → photonicWarhead]
//   Missile Ctr 1    ( 100, -800)   [needs ironTube + photonicWarhead]
//
// JUMP GATE PAIR
//   Gate Alpha       ( 200, 200)    → Gate Beta (far region)
//   Gate Beta        (2200, 200)    → Gate Alpha (near region)
//
// FAR REGION  (accessed via jump gate)
//   Iron Mine Far    (2500, -400)   [higher output, far from processing]
//   Copper Mine Far  (1800, -600)   [feeds far copper smelter or ships back]
//   Torpedo Ctr 1    (2200, -700)   [aspirational — needs 4 inputs]
//
// COURIERS
//   Warp Shuttle     ( 50, 150)     [1-stack, fast warp — courier routes]
//   Jump Courier     (-50, 150)     [5-stack, jump capable — gate runner]

// ── Helpers ────────────────────────────────────────────────────────

static void AddEngine(Entity e, EngineModuleConfig cfg) =>
    e.Components.Add(new EngineModule(cfg) { IsInstalled = true });

static void AddPower(Entity e, PowerModuleConfig cfg) =>
    e.Components.Add(new PowerModule(cfg) { IsInstalled = true });

static void AddCargo(Entity e, CargoModuleConfig cfg) =>
    e.Components.Add(new CargoModule(cfg) { IsInstalled = true });

static void AddLogistics(Entity e) =>
    e.Components.Add(new LogisticsComponent { AcceptsTradeContracts = true });

static void FitShip(Entity e, EngineModuleConfig eng, PowerModuleConfig pwr)
{
    AddEngine(e, eng);
    AddPower(e, pwr);
    e.Components.Add(new ShipStatsComponent());
}

static void FitStation(Entity e, ProductionComponentConfig prod, PowerModuleConfig pwr)
{
    e.Components.Add(new ProductionComponent(prod) { IsInstalled = true });
    AddPower(e, pwr);
}

// ── Heavy haulers ──────────────────────────────────────────────────

var hauler1 = sim.CreateEntity("Hauler-1", "ship", new Vector2(-100, 50));
FitShip(hauler1, EngineModuleDefinitions.DothrakiHorseDrive, PowerModuleDefinitions.CivilianFusionCore);
AddCargo(hauler1, CargoModuleDefinitions.StandardCargoContainer);
AddLogistics(hauler1);

var hauler2 = sim.CreateEntity("Hauler-2", "ship", new Vector2(100, -50));
FitShip(hauler2, EngineModuleDefinitions.GeckoSublightDrive, PowerModuleDefinitions.MiniFusionPlant);
AddCargo(hauler2, CargoModuleDefinitions.SmallCargoPod);
AddLogistics(hauler2);

var hauler3 = sim.CreateEntity("Hauler-3", "ship", new Vector2(0, 100));
FitShip(hauler3, EngineModuleDefinitions.GeckoSublightDrive, PowerModuleDefinitions.CivilianFusionCore);
AddCargo(hauler3, CargoModuleDefinitions.StandardCargoContainer);
AddLogistics(hauler3);

// ── Warp shuttle — 1 stack, fast courier ──────────────────────────
var warpShuttle = sim.CreateEntity("Warp Shuttle", "ship", new Vector2(50, 150));
FitShip(warpShuttle, EngineModuleDefinitions.GeckoSublightDrive, PowerModuleDefinitions.MiniFusionPlant);
AddEngine(warpShuttle, EngineModuleDefinitions.MicroWarpPod);
AddCargo(warpShuttle, CargoModuleDefinitions.SmallCargoPod);   // 10 slots
AddLogistics(warpShuttle);

// ── Jump courier — 5 stacks, gate runner ──────────────────────────
var jumpCourier = sim.CreateEntity("Jump Courier", "ship", new Vector2(-50, 150));
FitShip(jumpCourier, EngineModuleDefinitions.GeckoSublightDrive, PowerModuleDefinitions.CivilianFusionCore);
AddEngine(jumpCourier, EngineModuleDefinitions.SpringJumpUnit);
AddCargo(jumpCourier, CargoModuleDefinitions.StandardCargoContainer);  // 50 slots
AddLogistics(jumpCourier);

// ── Non-logistics ──────────────────────────────────────────────────
var fighter1 = sim.CreateEntity("Fighter-1", "ship", new Vector2(-200, 100));
FitShip(fighter1, EngineModuleDefinitions.GeckoSublightDrive, PowerModuleDefinitions.MiniFusionPlant);
// No LogisticsComponent — invisible to TradeSystem

// ── NEAR REGION — production stations ─────────────────────────────

// Tier 0 — Near extraction
var ironMine = sim.CreateEntity("Iron Mine 1", "station", new Vector2(-8000, -4000));
FitStation(ironMine, ProductionComponentDefinitions.IronMiningDrill, PowerModuleDefinitions.CivilianFusionCore);

var copperMine = sim.CreateEntity("Copper Mine 1", "station", new Vector2(6000, -5000));
FitStation(copperMine, ProductionComponentDefinitions.CopperMiningDrill, PowerModuleDefinitions.CivilianFusionCore);

// Tier 1 — Smelting
var ironSmelter = sim.CreateEntity("Iron Smelter 1", "station", new Vector2(-5000, -7000));
FitStation(ironSmelter, ProductionComponentDefinitions.IronSmelter, PowerModuleDefinitions.CivilianFusionCore);

var copperSmelter = sim.CreateEntity("Copper Smelter 1", "station", new Vector2(4000, -7000));
FitStation(copperSmelter, ProductionComponentDefinitions.CopperSmelter, PowerModuleDefinitions.CivilianFusionCore);

// Tier 2 — Fabrication + Warheads
var fabricator = sim.CreateEntity("Fabricator 1", "station", new Vector2(-1000, -6000));
FitStation(fabricator, ProductionComponentDefinitions.IronTubeFabricator, PowerModuleDefinitions.CivilianFusionCore);

var warheadFactory = sim.CreateEntity("Warhead Factory 1", "station", new Vector2(3000, -6000));
FitStation(warheadFactory, ProductionComponentDefinitions.WarheadFactory, PowerModuleDefinitions.MilitaryFusionCore);

// Tier 3 — End product (needs ironTube + photonicWarhead)
var missileFactory = sim.CreateEntity("Missile Ctr 1", "station", new Vector2(1000, -8000));
FitStation(missileFactory, ProductionComponentDefinitions.MissileConstructor, PowerModuleDefinitions.MilitaryFusionCore);

// ── JUMP GATE PAIR ─────────────────────────────────────────────────
// Gate Alpha (near) ↔ Gate Beta (far)

var gateAlpha = sim.CreateEntity("Gate Alpha", "gate", new Vector2(2000, 2000));
var gateBeta  = sim.CreateEntity("Gate Beta",  "gate", new Vector2(22000, 200));

// Link the gates bidirectionally
var gateAlphaComp = new StarshipSimulation.Shared.Entities.Components.JumpGateComponent
    { IsOperational = true };
var gateBetaComp  = new StarshipSimulation.Shared.Entities.Components.JumpGateComponent
    { IsOperational = true };

gateAlphaComp.LinkedGateEntityId = gateBeta.Id;
gateBetaComp.LinkedGateEntityId  = gateAlpha.Id;
gateAlpha.Components.Add(gateAlphaComp);
gateBeta.Components.Add(gateBetaComp);

// Invalidate gate cache so NavigationSystem rebuilds
sim.NavigationSystem.InvalidateGateCache();

// ── FAR REGION — accessed via jump gate ───────────────────────────

// Far extraction (high-output mines worth the transit cost)
var ironMineFar = sim.CreateEntity("Iron Mine Far", "station", new Vector2(25000, -4000));
FitStation(ironMineFar, ProductionComponentDefinitions.IronMiningDrill, PowerModuleDefinitions.CivilianFusionCore);

var copperMineFar = sim.CreateEntity("Copper Mine Far", "station", new Vector2(18000, -6000));
FitStation(copperMineFar, ProductionComponentDefinitions.CopperMiningDrill, PowerModuleDefinitions.CivilianFusionCore);

// Aspirational — starved until chain matures
var torpedoFactory = sim.CreateEntity("Torpedo Ctr 1", "station", new Vector2(22000, -7000));
FitStation(torpedoFactory, ProductionComponentDefinitions.StealthTorpedoConstructor, PowerModuleDefinitions.MilitaryFusionCore);

// ── Swarm ──────────────────────────────────────────────────────────
var swarm = sim.CreateEntity("Missile Swarm", "swarm", new Vector2(-3000, 1000));
swarm.Velocity = new Vector2(3f, 1.5f);

// ------------------------------------------------------------
// Run
// ------------------------------------------------------------

Console.WriteLine("[Server] StarshipSimulation starting...");
Console.WriteLine("[Server] Endpoints:");
Console.WriteLine("[Server]   ws://localhost:5000/ws/universe");
Console.WriteLine("[Server]   ws://localhost:5000/ws/events");
Console.WriteLine("[Server]   ws://localhost:5000/ws/commands");

app.Run();
