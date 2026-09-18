using System;
using System.Collections.Generic;
using System.Linq;
using NpcInt.Core;

namespace DynaraSim.Core
{
    public sealed class DirectorGoal
    {
        public string Id;
        public string Label;
        public float Priority;
        public string Reason;
    }

    public sealed class DirectorPlan
    {
        public DirectorGoal Goal;
        public readonly List<ActionIntent> Steps = new List<ActionIntent>();
        public readonly List<string> ReasoningTrace = new List<string>();
    }

    public sealed class DirectorSelfModel
    {
        public string Identity = "DYNARA";
        public string Role = "Simulation Director";
        public string Purpose = "administrar una simulación persistente, crear experiencias y adaptar el mundo sin romper sus reglas";
        public float PurposeConfidence = 1f;
        public float PerformanceEstimate = 0.5f;
        public int SuccessfulInterventions;
        public int FailedInterventions;
    }

    public sealed class DirectorMemoryItem
    {
        public double TimeMinutes;
        public string Kind;
        public string Text;
        public float Importance;
    }

    public sealed class DynaraDirector
    {
        private readonly NpcBrain _brain;
        private readonly MentalCycleEngine _mind;
        private readonly Dictionary<string, PlayerModel> _players =
            new Dictionary<string, PlayerModel>(StringComparer.OrdinalIgnoreCase);
        private readonly List<DirectorMemoryItem> _memory = new List<DirectorMemoryItem>();
        private int _stimulusSequence;

        public AgentProfile Profile { get; private set; }
        public BeliefState Beliefs { get; private set; } = new BeliefState();
        public DirectorSelfModel Self { get; private set; } = new DirectorSelfModel();
        public DirectorPlan LastPlan { get; private set; }
        public MentalCycleResult LastNpcIntCycle { get { return _mind.LastCycle; } }
        public IEnumerable<PlayerModel> PlayerModels { get { return _players.Values; } }
        public IEnumerable<DirectorMemoryItem> Memory { get { return _memory; } }

        public DynaraDirector(int seed = 1337)
        {
            Profile = BuildProfile();
            _brain = new NpcBrain(seed);
            _mind = new MentalCycleEngine(_brain);
        }

        public PlayerModel EnsurePlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("playerId required");
            PlayerModel model;
            if (!_players.TryGetValue(playerId, out model))
            {
                model = new PlayerModel { PlayerId = playerId };
                _players[playerId] = model;
            }
            return model;
        }

        public void Observe(WorldEvent worldEvent, WorldState world)
        {
            if (worldEvent == null || world == null) return;

            Beliefs.ObserveWorld(world);
            Beliefs.Upsert("event:last:type", worldEvent.Type, "WorldState", 1f, world.TimeMinutes);
            Beliefs.Upsert("event:last:description", worldEvent.Description, "WorldState", 1f, world.TimeMinutes);

            _brain.ProcessWorldEvent(worldEvent.Description ?? worldEvent.Type, worldEvent.Importance, worldEvent.Threat);
            _mind.ThinkWorld(worldEvent.Description ?? worldEvent.Type, worldEvent.Threat, worldEvent.Importance);
            Remember(world.TimeMinutes, "world", worldEvent.Description ?? worldEvent.Type, worldEvent.Importance);
        }

        public void ObservePlayerInput(string playerId, string text, WorldState world)
        {
            PlayerModel model = EnsurePlayer(playerId);
            model.ObserveInput(text);
            _brain.ProcessMessage(text ?? string.Empty, playerId);
            _mind.ThinkMessage(text ?? string.Empty);
            Remember(world == null ? 0 : world.TimeMinutes, "player", playerId + ": " + text, 0.65f);
        }

        public DirectorPlan BuildPlan(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            var plan = new DirectorPlan();
            List<EntityState> players = world.Players.ToList();

            if (players.Count == 0)
            {
                plan.Goal = Goal("wait_for_players", "esperar usuarios", 0.20f, "no hay jugadores activos");
                plan.Steps.Add(new ActionIntent { ActorId = Profile.Id, Type = DynaraActionType.Wait });
                plan.ReasoningTrace.Add("Sin jugadores no conviene fabricar contenido que nadie puede experimentar.");
                LastPlan = plan;
                return plan;
            }

            foreach (EntityState player in players) EnsurePlayer(player.Id);

            float engagement = Average(x => x.Engagement, 0.55f);
            float frustration = Average(x => x.Frustration, 0.15f);
            float stress = Average(x => x.Stress, 0.10f);
            AdventureState active = world.ActiveAdventure();

            plan.ReasoningTrace.Add("players=" + players.Count);
            plan.ReasoningTrace.Add("engagement=" + engagement.ToString("0.00"));
            plan.ReasoningTrace.Add("frustration=" + frustration.ToString("0.00"));
            plan.ReasoningTrace.Add("stress=" + stress.ToString("0.00"));
            plan.ReasoningTrace.Add("activeAdventure=" + (active == null ? "none" : active.Spec.Id));

            if (active == null)
            {
                float intensity = Clamp01(0.62f - frustration * 0.25f - stress * 0.35f + engagement * 0.18f);
                string theme = ChooseTheme();
                plan.Goal = Goal("create_experience", "crear una aventura para los jugadores", 0.86f,
                    "hay jugadores pero no existe una aventura activa");

                var action = new ActionIntent
                {
                    ActorId = Profile.Id,
                    Type = DynaraActionType.CreateAdventure,
                    Template = theme,
                    Text = theme
                };
                action.Parameters["intensity"] = intensity.ToString(System.Globalization.CultureInfo.InvariantCulture);
                plan.Steps.Add(action);
                plan.ReasoningTrace.Add("La intensidad se reduce si frustración/estrés suben; no se optimiza sólo por excitación.");
            }
            else if (frustration > 0.72f || stress > 0.72f)
            {
                plan.Goal = Goal("reduce_pressure", "reducir presión de la experiencia", 0.92f,
                    "el modelo de usuarios estima frustración o estrés altos");
                var action = new ActionIntent
                {
                    ActorId = Profile.Id,
                    Type = DynaraActionType.EmitEvent,
                    Text = "Dynara reduce temporalmente la presión del escenario y hace más legibles las salidas."
                };
                action.Parameters["eventType"] = "director_relief";
                action.Parameters["importance"] = "0.75";
                plan.Steps.Add(action);
            }
            else if (engagement < 0.42f)
            {
                plan.Goal = Goal("increase_engagement", "introducir una novedad legible", 0.82f,
                    "engagement agregado por debajo del umbral");

                var spawn = new ActionIntent
                {
                    ActorId = Profile.Id,
                    Type = DynaraActionType.SpawnEntity,
                    Template = "Anomalía",
                    LocationId = players[0].LocationId
                };
                spawn.Parameters["id"] = "stimulus_" + (++_stimulusSequence).ToString("0000");
                spawn.Parameters["name"] = "Anomalía dinámica";
                spawn.Parameters["kind"] = "Object";
                spawn.Parameters["tag:director_stimulus"] = "true";
                spawn.Parameters["prop:purpose"] = "crear curiosidad sin bloquear el progreso";
                plan.Steps.Add(spawn);
            }
            else
            {
                plan.Goal = Goal("maintain_coherence", "mantener coherencia y observar consecuencias", 0.55f,
                    "la experiencia está activa y no hay señal fuerte que justifique intervenir");
                plan.Steps.Add(new ActionIntent
                {
                    ActorId = Profile.Id,
                    Type = DynaraActionType.Observe,
                    TargetId = active.Spec.Id
                });
            }

            LastPlan = plan;
            return plan;
        }

        public void ObserveResult(ActionResult result, WorldState world)
        {
            if (result == null) return;
            if (result.Success) Self.SuccessfulInterventions++;
            else Self.FailedInterventions++;

            int total = Self.SuccessfulInterventions + Self.FailedInterventions;
            Self.PerformanceEstimate = total == 0 ? 0.5f :
                (float)Self.SuccessfulInterventions / total;

            Remember(world == null ? 0 : world.TimeMinutes,
                result.Success ? "action_success" : "action_failure",
                result.Code + ": " + result.Message,
                result.Success ? 0.55f : 0.80f);
        }

        private string ChooseTheme()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (PlayerModel player in _players.Values)
                foreach (string interest in player.Interests)
                    counts[interest] = counts.ContainsKey(interest) ? counts[interest] + 1 : 1;

            return counts.Count == 0
                ? "misterio"
                : counts.OrderByDescending(x => x.Value).ThenBy(x => x.Key).First().Key;
        }

        private float Average(Func<PlayerModel, float> selector, float fallback)
        {
            if (_players.Count == 0) return fallback;
            float sum = 0f;
            foreach (PlayerModel player in _players.Values) sum += selector(player);
            return sum / _players.Count;
        }

        private void Remember(double time, string kind, string text, float importance)
        {
            _memory.Add(new DirectorMemoryItem
            {
                TimeMinutes = time,
                Kind = kind,
                Text = text ?? string.Empty,
                Importance = Clamp01(importance)
            });
            if (_memory.Count > 200) _memory.RemoveAt(0);
        }

        private static DirectorGoal Goal(string id, string label, float priority, string reason)
        {
            return new DirectorGoal { Id = id, Label = label, Priority = priority, Reason = reason };
        }

        private static AgentProfile BuildProfile()
        {
            var profile = new AgentProfile
            {
                Id = "dynara",
                Name = "DYNARA",
                Role = "Simulation Director",
                Purpose = "administrar la simulación completa y crear experiencias adaptativas coherentes"
            };

            profile.Capabilities.Add(AgentCapability.Speak);
            profile.Capabilities.Add(AgentCapability.Move);
            profile.Capabilities.Add(AgentCapability.Interact);
            profile.Capabilities.Add(AgentCapability.ObserveGlobalWorld);
            profile.Capabilities.Add(AgentCapability.SpawnEntity);
            profile.Capabilities.Add(AgentCapability.DespawnEntity);
            profile.Capabilities.Add(AgentCapability.ModifyEnvironment);
            profile.Capabilities.Add(AgentCapability.TeleportEntity);
            profile.Capabilities.Add(AgentCapability.CreateAdventure);
            profile.Capabilities.Add(AgentCapability.ManageNpc);
            profile.Capabilities.Add(AgentCapability.EmitWorldEvent);
            return profile;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
