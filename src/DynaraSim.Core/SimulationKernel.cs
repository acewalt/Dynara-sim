using System;
using System.Collections.Generic;
using System.Linq;

namespace DynaraSim.Core
{
    public sealed class DirectorCycleResult
    {
        public DirectorPlan Plan;
        public readonly List<ActionResult> Results = new List<ActionResult>();
    }

    public sealed class SimulationKernel
    {
        private int _playerSequence;
        private int _locationSequence;

        public WorldState World { get; private set; }
        public EventBus Events { get; private set; }
        public PerceptionSystem Perception { get; private set; }
        public AdventureDirector AdventureDirector { get; private set; }
        public ActionGateway Actions { get; private set; }
        public DynaraDirector Dynara { get; private set; }

        public SimulationKernel(int seed = 1337)
        {
            World = new WorldState();
            Events = new EventBus();
            Perception = new PerceptionSystem();
            AdventureDirector = new AdventureDirector();
            Actions = new ActionGateway(World, Events, AdventureDirector);
            Dynara = new DynaraDirector(seed);

            World.Rules["decision_is_not_execution"] = "true";
            World.Rules["world_truth_separate_from_beliefs"] = "true";
            World.Rules["admin_actions_require_capabilities"] = "true";

            World.Entities[Dynara.Profile.Id] = new EntityState
            {
                Id = Dynara.Profile.Id,
                Name = Dynara.Profile.Name,
                Kind = EntityKind.Agent,
                LocationId = "system",
                Active = true
            };

            Actions.RegisterAgent(Dynara.Profile);
            Events.Subscribe(e => Dynara.Observe(e, World));
        }

        public EntityState AddLocation(string name, string id = null)
        {
            string locationId = string.IsNullOrWhiteSpace(id)
                ? "location_" + (++_locationSequence).ToString("0000")
                : id;

            var entity = new EntityState
            {
                Id = locationId,
                Name = string.IsNullOrWhiteSpace(name) ? locationId : name,
                Kind = EntityKind.Location,
                LocationId = locationId,
                Active = true
            };
            World.Entities[entity.Id] = entity;
            Publish(new WorldEvent
            {
                Type = "location_added",
                Description = "Se registró la ubicación " + entity.Name + ".",
                SourceId = "system",
                TargetId = entity.Id,
                LocationId = entity.Id,
                Importance = 0.25f
            });
            return entity;
        }

        public EntityState AddPlayer(string name, string locationId = null, string id = null)
        {
            string playerId = string.IsNullOrWhiteSpace(id)
                ? "player_" + (++_playerSequence).ToString("0000")
                : id;

            var player = new EntityState
            {
                Id = playerId,
                Name = string.IsNullOrWhiteSpace(name) ? playerId : name,
                Kind = EntityKind.Player,
                LocationId = locationId,
                Active = true
            };
            World.Entities[player.Id] = player;
            Dynara.EnsurePlayer(player.Id);

            Publish(new WorldEvent
            {
                Type = "player_joined",
                Description = player.Name + " entró en la simulación.",
                SourceId = player.Id,
                TargetId = player.Id,
                LocationId = player.LocationId,
                Importance = 0.80f
            });
            return player;
        }

        public DirectorCycleResult ProcessPlayerInput(string playerId, string text, bool runDirector = true)
        {
            EntityState player = World.FindEntity(playerId);
            if (player == null || player.Kind != EntityKind.Player)
                throw new InvalidOperationException("Unknown player: " + playerId);

            WorldEvent input = Publish(new WorldEvent
            {
                Type = "player_input",
                Description = player.Name + ": " + (text ?? string.Empty),
                SourceId = player.Id,
                TargetId = Dynara.Profile.Id,
                LocationId = player.LocationId,
                Importance = 0.65f
            });

            Dynara.ObservePlayerInput(player.Id, text ?? string.Empty, World);
            return runDirector ? RunDirectorCycle() : new DirectorCycleResult();
        }

        public DirectorCycleResult Tick(double minutes = 1.0)
        {
            if (minutes < 0) minutes = 0;
            World.TimeMinutes += minutes;
            World.ActiveEvents.Clear();

            Publish(new WorldEvent
            {
                Type = "time_advanced",
                Description = "La simulación avanzó " + minutes.ToString("0.##") + " minutos.",
                SourceId = "system",
                TargetId = Dynara.Profile.Id,
                Importance = 0.15f
            });

            DecayPlayerModels((float)minutes);
            return RunDirectorCycle();
        }

        public DirectorCycleResult RunDirectorCycle()
        {
            DirectorPlan plan = Dynara.BuildPlan(World);
            var cycle = new DirectorCycleResult { Plan = plan };

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ActionResult result = Actions.Execute(plan.Steps[i]);
                cycle.Results.Add(result);
                Dynara.ObserveResult(result, World);
            }

            return cycle;
        }

        public WorldEvent EmitExternalEvent(string description, string locationId = null,
            float importance = 0.6f, float threat = 0f, string sourceId = "unity")
        {
            return Publish(new WorldEvent
            {
                Type = "external_event",
                Description = description ?? string.Empty,
                SourceId = sourceId,
                LocationId = locationId,
                Importance = Clamp01(importance),
                Threat = Clamp01(threat)
            });
        }

        public void SeedDemoWorld()
        {
            if (World.FindEntity("lobby") == null) AddLocation("Lobby", "lobby");
            if (World.FindEntity("forest") == null) AddLocation("Bosque de prueba", "forest");

            if (World.FindEntity("door_01") == null)
            {
                var door = new EntityState
                {
                    Id = "door_01",
                    Name = "Puerta de prueba",
                    Kind = EntityKind.Door,
                    LocationId = "lobby",
                    Active = true
                };
                door.Properties["open"] = "false";
                door.Properties["locked"] = "true";
                World.Entities[door.Id] = door;
            }

            if (World.FindEntity("key_01") == null)
            {
                var key = new EntityState
                {
                    Id = "key_01",
                    Name = "Llave de prueba",
                    Kind = EntityKind.Item,
                    LocationId = "forest",
                    Active = true
                };
                World.Entities[key.Id] = key;
            }
        }

        private WorldEvent Publish(WorldEvent worldEvent)
        {
            World.RecordEvent(worldEvent);
            Events.Publish(worldEvent);
            return worldEvent;
        }

        private void DecayPlayerModels(float minutes)
        {
            foreach (PlayerModel player in Dynara.PlayerModels)
            {
                if (minutes <= 0f) continue;
                player.Frustration = Clamp01(player.Frustration - minutes * 0.002f);
                player.Stress = Clamp01(player.Stress - minutes * 0.002f);
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
