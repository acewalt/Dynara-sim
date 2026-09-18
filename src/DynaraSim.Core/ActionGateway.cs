using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DynaraSim.Core
{
    public sealed class ActionGateway
    {
        private readonly WorldState _world;
        private readonly EventBus _events;
        private readonly AdventureDirector _adventures;
        private readonly Dictionary<string, AgentProfile> _agents =
            new Dictionary<string, AgentProfile>(StringComparer.OrdinalIgnoreCase);
        private int _entitySequence;

        public ActionGateway(WorldState world, EventBus events, AdventureDirector adventures)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _adventures = adventures ?? throw new ArgumentNullException(nameof(adventures));
        }

        public void RegisterAgent(AgentProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                throw new ArgumentException("Agent profile requires an id.", nameof(profile));
            _agents[profile.Id] = profile;
        }

        public AgentProfile FindAgent(string id)
        {
            AgentProfile profile;
            return !string.IsNullOrWhiteSpace(id) && _agents.TryGetValue(id, out profile) ? profile : null;
        }

        public ActionResult Execute(ActionIntent intent)
        {
            if (intent == null) return ActionResult.Fail(null, "invalid_intent", "La intención es nula.");

            AgentProfile actor = FindAgent(intent.ActorId);
            if (actor == null) return ActionResult.Fail(intent, "unknown_actor", "El actor no está registrado.");

            string capabilityError = ValidateCapability(actor, intent);
            if (capabilityError != null) return ActionResult.Fail(intent, "capability_denied", capabilityError);

            switch (intent.Type)
            {
                case DynaraActionType.Speak:
                    return Emit(intent, "agent_spoke", intent.Text ?? actor.Name + " habló.", intent.ActorId, intent.TargetId, intent.LocationId, 0.35f, 0f);

                case DynaraActionType.Move:
                    return Move(intent, actor, false);

                case DynaraActionType.Teleport:
                    return Move(intent, actor, true);

                case DynaraActionType.SpawnEntity:
                    return Spawn(intent);

                case DynaraActionType.DespawnEntity:
                    return Despawn(intent);

                case DynaraActionType.SetProperty:
                    return SetProperty(intent);

                case DynaraActionType.CreateAdventure:
                    return CreateAdventure(intent);

                case DynaraActionType.EmitEvent:
                    return Emit(intent, Read(intent, "eventType", "director_event"),
                        intent.Text ?? "Dynara produjo un evento.", intent.ActorId, intent.TargetId,
                        intent.LocationId, ReadFloat(intent, "importance", 0.55f), ReadFloat(intent, "threat", 0f));

                case DynaraActionType.Observe:
                    return Emit(intent, "agent_observed", actor.Name + " observó el mundo.", intent.ActorId, intent.TargetId, intent.LocationId, 0.15f, 0f);

                case DynaraActionType.Wait:
                    return ActionResult.Ok(intent, "wait", actor.Name + " decidió esperar.");

                default:
                    return ActionResult.Fail(intent, "unsupported_action", "Acción todavía no implementada: " + intent.Type);
            }
        }

        private ActionResult Move(ActionIntent intent, AgentProfile actor, bool teleport)
        {
            string entityId = string.IsNullOrWhiteSpace(intent.TargetId) ? intent.ActorId : intent.TargetId;
            EntityState entity = _world.FindEntity(entityId);
            if (entity == null) return ActionResult.Fail(intent, "target_missing", "No existe la entidad " + entityId + ".");
            if (string.IsNullOrWhiteSpace(intent.LocationId)) return ActionResult.Fail(intent, "location_missing", "La acción necesita LocationId.");

            string previous = entity.LocationId;
            entity.LocationId = intent.LocationId;
            string description = actor.Name + (teleport ? " teletransportó " : " movió ") + entity.Name +
                                 " de " + (previous ?? "ningún lugar") + " a " + intent.LocationId + ".";
            return Emit(intent, teleport ? "entity_teleported" : "entity_moved", description,
                intent.ActorId, entity.Id, intent.LocationId, 0.45f, 0f);
        }

        private ActionResult Spawn(ActionIntent intent)
        {
            string id = Read(intent, "id", "entity_" + (++_entitySequence).ToString("0000"));
            if (_world.Entities.ContainsKey(id)) return ActionResult.Fail(intent, "duplicate_entity", "Ya existe " + id + ".");

            EntityKind kind;
            string kindText = Read(intent, "kind", "Object");
            if (!Enum.TryParse(kindText, true, out kind)) kind = EntityKind.Object;

            var entity = new EntityState
            {
                Id = id,
                Name = Read(intent, "name", intent.Template ?? id),
                Kind = kind,
                LocationId = intent.LocationId,
                Active = true
            };
            foreach (KeyValuePair<string, string> pair in intent.Parameters)
            {
                if (pair.Key.StartsWith("prop:", StringComparison.OrdinalIgnoreCase))
                    entity.Properties[pair.Key.Substring(5)] = pair.Value;
                if (pair.Key.StartsWith("tag:", StringComparison.OrdinalIgnoreCase) && pair.Value == "true")
                    entity.Tags.Add(pair.Key.Substring(4));
            }

            _world.Entities[id] = entity;
            ActionResult result = Emit(intent, "entity_spawned", "Apareció " + entity.Name + " (" + entity.Kind + ").",
                intent.ActorId, id, entity.LocationId, 0.55f, 0f);
            result.CreatedId = id;
            return result;
        }

        private ActionResult Despawn(ActionIntent intent)
        {
            EntityState target = _world.FindEntity(intent.TargetId);
            if (target == null) return ActionResult.Fail(intent, "target_missing", "La entidad no existe.");
            target.Active = false;
            return Emit(intent, "entity_despawned", target.Name + " fue retirado de la simulación.",
                intent.ActorId, target.Id, target.LocationId, 0.55f, 0f);
        }

        private ActionResult SetProperty(ActionIntent intent)
        {
            EntityState target = _world.FindEntity(intent.TargetId);
            if (target == null) return ActionResult.Fail(intent, "target_missing", "La entidad no existe.");
            string key = Read(intent, "key", null);
            string value = Read(intent, "value", null);
            if (string.IsNullOrWhiteSpace(key)) return ActionResult.Fail(intent, "property_missing", "Falta Parameters[key].");
            target.Properties[key] = value ?? string.Empty;
            return Emit(intent, "entity_property_changed", target.Name + "." + key + " = " + (value ?? string.Empty),
                intent.ActorId, target.Id, target.LocationId, 0.40f, 0f);
        }

        private ActionResult CreateAdventure(ActionIntent intent)
        {
            string theme = !string.IsNullOrWhiteSpace(intent.Template) ? intent.Template :
                           !string.IsNullOrWhiteSpace(intent.Text) ? intent.Text : "misterio";
            float intensity = ReadFloat(intent, "intensity", 0.55f);
            int seed = (int)(_world.TimeMinutes * 1000.0) + _world.EventLog.Count + 31;
            IEnumerable<string> participants = _world.Players.Select(x => x.Id);

            AdventureSpec spec = _adventures.Generate(theme, participants, seed, intensity);
            _world.Adventures[spec.Id] = new AdventureState
            {
                Spec = spec,
                Status = "active",
                CurrentBeat = 0,
                StartedAtMinutes = _world.TimeMinutes
            };

            ActionResult result = Emit(intent, "adventure_created",
                "Dynara creó la aventura " + spec.Id + " · tema=" + spec.Theme + " · intensidad=" + spec.TargetIntensity.ToString("0.00"),
                intent.ActorId, spec.Id, intent.LocationId, 0.90f, 0f);
            result.CreatedId = spec.Id;
            return result;
        }

        private string ValidateCapability(AgentProfile actor, ActionIntent intent)
        {
            AgentCapability required;
            switch (intent.Type)
            {
                case DynaraActionType.Speak: required = AgentCapability.Speak; break;
                case DynaraActionType.Move:
                    required = !string.IsNullOrWhiteSpace(intent.TargetId) &&
                               !string.Equals(intent.TargetId, actor.Id, StringComparison.OrdinalIgnoreCase)
                        ? AgentCapability.TeleportEntity : AgentCapability.Move;
                    break;
                case DynaraActionType.Teleport: required = AgentCapability.TeleportEntity; break;
                case DynaraActionType.SpawnEntity: required = AgentCapability.SpawnEntity; break;
                case DynaraActionType.DespawnEntity: required = AgentCapability.DespawnEntity; break;
                case DynaraActionType.SetProperty: required = AgentCapability.ModifyEnvironment; break;
                case DynaraActionType.CreateAdventure: required = AgentCapability.CreateAdventure; break;
                case DynaraActionType.EmitEvent: required = AgentCapability.EmitWorldEvent; break;
                case DynaraActionType.Observe: required = AgentCapability.ObserveGlobalWorld; break;
                case DynaraActionType.Wait: return null;
                default: return "No existe un permiso definido para " + intent.Type + ".";
            }
            return actor.Can(required) ? null : actor.Name + " no tiene la capacidad " + required + ".";
        }

        private ActionResult Emit(ActionIntent intent, string type, string description,
            string sourceId, string targetId, string locationId, float importance, float threat)
        {
            WorldEvent worldEvent = _world.RecordEvent(new WorldEvent
            {
                Type = type,
                Description = description,
                SourceId = sourceId,
                TargetId = targetId,
                LocationId = locationId,
                Importance = Clamp01(importance),
                Threat = Clamp01(threat)
            });
            _events.Publish(worldEvent);

            ActionResult result = ActionResult.Ok(intent, type, description);
            result.Event = worldEvent;
            return result;
        }

        private static string Read(ActionIntent intent, string key, string fallback)
        {
            string value;
            return intent.Parameters.TryGetValue(key, out value) ? value : fallback;
        }

        private static float ReadFloat(ActionIntent intent, string key, float fallback)
        {
            string value;
            float parsed;
            return intent.Parameters.TryGetValue(key, out value) &&
                   float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed : fallback;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
