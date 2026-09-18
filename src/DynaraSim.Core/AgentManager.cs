using System;
using System.Collections.Generic;
using NpcInt.Core;

namespace DynaraSim.Core
{
    public sealed class CognitiveAgentRuntime
    {
        private readonly NpcBrain _brain;
        private readonly MentalCycleEngine _mind;
        private readonly List<Percept> _recentPercepts = new List<Percept>();

        public AgentProfile Profile { get; private set; }
        public NpcBrain Brain { get { return _brain; } }
        public MentalCycleEngine Mind { get { return _mind; } }
        public BeliefState Beliefs { get; private set; } = new BeliefState();
        public IEnumerable<Percept> RecentPercepts { get { return _recentPercepts; } }

        public CognitiveAgentRuntime(AgentProfile profile, int seed)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _brain = new NpcBrain(seed);
            _mind = new MentalCycleEngine(_brain);
        }

        public void Observe(Percept percept, WorldState world)
        {
            if (percept == null || world == null) return;

            _recentPercepts.Add(percept);
            if (_recentPercepts.Count > 40) _recentPercepts.RemoveAt(0);

            string key = "event:" + percept.SourceEventSequence;
            Beliefs.Upsert(key, percept.Description, "perception", percept.Confidence, world.TimeMinutes);
            if (!string.IsNullOrWhiteSpace(percept.SubjectId))
                Beliefs.Upsert("entity:" + percept.SubjectId + ":last_seen_location",
                    percept.LocationId ?? string.Empty, "perception", percept.Confidence, world.TimeMinutes);

            _brain.ProcessWorldEvent(percept.Description ?? percept.Type, percept.Salience, percept.Threat);
            _mind.ThinkWorld(percept.Description ?? percept.Type, percept.Threat, percept.Salience);
        }

        public MentalCycleResult Hear(string text, string speaker)
        {
            _brain.ProcessMessage(text ?? string.Empty, speaker ?? "unknown");
            return _mind.ThinkMessage(text ?? string.Empty);
        }
    }

    public sealed class AgentManager
    {
        private readonly WorldState _world;
        private readonly EventBus _events;
        private readonly PerceptionSystem _perception;
        private readonly ActionGateway _actions;
        private readonly Dictionary<string, CognitiveAgentRuntime> _agents =
            new Dictionary<string, CognitiveAgentRuntime>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<CognitiveAgentRuntime> Items { get { return _agents.Values; } }

        public AgentManager(WorldState world, EventBus events, PerceptionSystem perception, ActionGateway actions)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _perception = perception ?? throw new ArgumentNullException(nameof(perception));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _events.Subscribe(HandleEvent);
        }

        public CognitiveAgentRuntime Register(AgentProfile profile, string locationId, EntityKind kind = EntityKind.Npc, int seed = 1)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                throw new ArgumentException("Agent profile requires id.", nameof(profile));

            CognitiveAgentRuntime existing;
            if (_agents.TryGetValue(profile.Id, out existing)) return existing;

            if (_world.FindEntity(profile.Id) == null)
            {
                _world.Entities[profile.Id] = new EntityState
                {
                    Id = profile.Id,
                    Name = profile.Name ?? profile.Id,
                    Kind = kind,
                    LocationId = locationId,
                    Active = true
                };
            }

            _actions.RegisterAgent(profile);
            var runtime = new CognitiveAgentRuntime(profile, seed);
            _agents[profile.Id] = runtime;
            return runtime;
        }

        public CognitiveAgentRuntime CreateNpc(NpcBlueprint blueprint, string adventureId, int index, string locationId)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            string id = adventureId + "_npc_" + index.ToString("00");

            var profile = new AgentProfile
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(blueprint.Name) ? id : blueprint.Name,
                Role = blueprint.Role ?? "npc",
                Purpose = blueprint.Purpose ?? "cumplir su rol dentro de la simulación"
            };
            profile.Capabilities.Add(AgentCapability.Speak);
            profile.Capabilities.Add(AgentCapability.Move);
            profile.Capabilities.Add(AgentCapability.Interact);

            for (int i = 0; i < blueprint.Capabilities.Count; i++)
            {
                string cap = (blueprint.Capabilities[i] ?? string.Empty).Trim().ToLowerInvariant();
                if (cap == "speak") profile.Capabilities.Add(AgentCapability.Speak);
                else if (cap == "move") profile.Capabilities.Add(AgentCapability.Move);
                else if (cap == "interact") profile.Capabilities.Add(AgentCapability.Interact);
            }

            CognitiveAgentRuntime runtime = Register(profile, locationId, EntityKind.Npc, StableSeed(id));
            EntityState entity = _world.FindEntity(id);
            if (entity != null)
            {
                entity.Properties["adventure"] = adventureId;
                entity.Properties["role"] = profile.Role ?? string.Empty;
                entity.Properties["purpose"] = profile.Purpose ?? string.Empty;
                for (int i = 0; i < blueprint.Traits.Count; i++) entity.Tags.Add(blueprint.Traits[i]);
            }

            WorldEvent spawned = _world.RecordEvent(new WorldEvent
            {
                Type = "cognitive_npc_spawned",
                Description = profile.Name + " apareció como NPC cognitivo de " + adventureId + ".",
                SourceId = "dynara",
                TargetId = id,
                LocationId = locationId,
                Importance = 0.65f
            });
            _events.Publish(spawned);
            return runtime;
        }

        public CognitiveAgentRuntime Find(string id)
        {
            CognitiveAgentRuntime runtime;
            return !string.IsNullOrWhiteSpace(id) && _agents.TryGetValue(id, out runtime) ? runtime : null;
        }

        private void HandleEvent(WorldEvent worldEvent)
        {
            CognitiveAgentRuntime[] snapshot = new List<CognitiveAgentRuntime>(_agents.Values).ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                CognitiveAgentRuntime runtime = snapshot[i];
                Percept percept = _perception.Perceive(worldEvent, runtime.Profile, _world);
                if (percept != null) runtime.Observe(percept, _world);
            }
        }

        private static int StableSeed(string value)
        {
            unchecked
            {
                int hash = 17;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++) hash = hash * 31 + text[i];
                return hash;
            }
        }
    }
}
