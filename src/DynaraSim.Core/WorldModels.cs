using System;
using System.Collections.Generic;
using System.Linq;

namespace DynaraSim.Core
{
    public enum EntityKind
    {
        Unknown,
        Player,
        Agent,
        Npc,
        Object,
        Item,
        Door,
        Location
    }

    public enum AgentCapability
    {
        Speak,
        Move,
        Interact,
        ObserveGlobalWorld,
        SpawnEntity,
        DespawnEntity,
        ModifyEnvironment,
        TeleportEntity,
        CreateAdventure,
        ManageNpc,
        EmitWorldEvent
    }

    public enum DynaraActionType
    {
        None,
        Speak,
        Move,
        Observe,
        SpawnEntity,
        DespawnEntity,
        SetProperty,
        Teleport,
        CreateAdventure,
        EmitEvent,
        Wait
    }

    public sealed class SimulationVector3
    {
        public float X;
        public float Y;
        public float Z;

        public SimulationVector3() { }

        public SimulationVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public SimulationVector3 Clone()
        {
            return new SimulationVector3(X, Y, Z);
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.##") + ", " + Y.ToString("0.##") + ", " + Z.ToString("0.##") + ")";
        }
    }

    public sealed class EntityState
    {
        public string Id;
        public string Name;
        public EntityKind Kind;
        public string LocationId;
        public SimulationVector3 Position = new SimulationVector3();
        public bool Active = true;
        public readonly HashSet<string> Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public EntityState Clone()
        {
            var clone = new EntityState
            {
                Id = Id,
                Name = Name,
                Kind = Kind,
                LocationId = LocationId,
                Position = Position == null ? new SimulationVector3() : Position.Clone(),
                Active = Active
            };
            foreach (string tag in Tags) clone.Tags.Add(tag);
            foreach (KeyValuePair<string, string> pair in Properties) clone.Properties[pair.Key] = pair.Value;
            return clone;
        }
    }

    public sealed class AgentProfile
    {
        public string Id;
        public string Name;
        public string Role;
        public string Purpose;
        public readonly HashSet<AgentCapability> Capabilities = new HashSet<AgentCapability>();

        public bool Can(AgentCapability capability)
        {
            return Capabilities.Contains(capability);
        }
    }

    public sealed class WorldEvent
    {
        public long Sequence;
        public string Type;
        public string Description;
        public string SourceId;
        public string TargetId;
        public string LocationId;
        public float Importance;
        public float Threat;
        public double TimeMinutes;
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ActionIntent
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string ActorId;
        public DynaraActionType Type;
        public string TargetId;
        public string LocationId;
        public string Template;
        public string Text;
        public readonly Dictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ActionResult
    {
        public string IntentId;
        public bool Success;
        public string Code;
        public string Message;
        public string CreatedId;
        public WorldEvent Event;

        public static ActionResult Fail(ActionIntent intent, string code, string message)
        {
            return new ActionResult
            {
                IntentId = intent == null ? null : intent.Id,
                Success = false,
                Code = code,
                Message = message
            };
        }

        public static ActionResult Ok(ActionIntent intent, string code, string message)
        {
            return new ActionResult
            {
                IntentId = intent == null ? null : intent.Id,
                Success = true,
                Code = code,
                Message = message
            };
        }
    }

    public sealed class AdventureState
    {
        public AdventureSpec Spec;
        public string Status = "active";
        public int CurrentBeat;
        public double StartedAtMinutes;

        public string CurrentBeatLabel
        {
            get
            {
                if (Spec == null || Spec.Beats.Count == 0) return null;
                int index = Math.Max(0, Math.Min(CurrentBeat, Spec.Beats.Count - 1));
                return Spec.Beats[index];
            }
        }
    }

    public sealed class WorldState
    {
        private long _eventSequence;

        public double TimeMinutes;
        public readonly Dictionary<string, EntityState> Entities = new Dictionary<string, EntityState>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, AdventureState> Adventures = new Dictionary<string, AdventureState>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<WorldEvent> EventLog = new List<WorldEvent>();
        public readonly List<WorldEvent> ActiveEvents = new List<WorldEvent>();

        public IEnumerable<EntityState> Players
        {
            get { return Entities.Values.Where(x => x.Kind == EntityKind.Player && x.Active); }
        }

        public IEnumerable<EntityState> Npcs
        {
            get { return Entities.Values.Where(x => (x.Kind == EntityKind.Npc || x.Kind == EntityKind.Agent) && x.Active); }
        }

        public WorldEvent RecordEvent(WorldEvent worldEvent)
        {
            if (worldEvent == null) throw new ArgumentNullException(nameof(worldEvent));
            worldEvent.Sequence = ++_eventSequence;
            worldEvent.TimeMinutes = TimeMinutes;
            EventLog.Add(worldEvent);
            ActiveEvents.Add(worldEvent);
            if (EventLog.Count > 1000) EventLog.RemoveAt(0);
            if (ActiveEvents.Count > 100) ActiveEvents.RemoveAt(0);
            return worldEvent;
        }

        public EntityState FindEntity(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            EntityState entity;
            return Entities.TryGetValue(id, out entity) ? entity : null;
        }

        public AdventureState ActiveAdventure()
        {
            return Adventures.Values.FirstOrDefault(x => string.Equals(x.Status, "active", StringComparison.OrdinalIgnoreCase));
        }
    }
}
