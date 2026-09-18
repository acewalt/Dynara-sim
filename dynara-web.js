"use strict";

(() => {
  const clamp = (v, min = 0, max = 1) => Math.max(min, Math.min(max, Number(v) || 0));
  const fold = value => (value || "").toLowerCase()
    .normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/\s+/g, " ").trim();

  class DynaraWebSim {
    constructor() {
      this.reset();
    }

    reset() {
      this.eventSeq = 0;
      this.entitySeq = 0;
      this.playerSeq = 0;
      this.adventureSeq = 0;
      this.stimulusSeq = 0;
      this.world = {
        timeMinutes: 0,
        entities: new Map(),
        adventures: new Map(),
        rules: new Map([
          ["decision_is_not_execution", "true"],
          ["world_truth_separate_from_beliefs", "true"],
          ["admin_actions_require_capabilities", "true"]
        ]),
        events: [],
        activeEvents: []
      };
      this.profile = {
        id: "dynara",
        name: "DYNARA",
        role: "Simulation Director",
        purpose: "administrar una simulación persistente, crear experiencias y adaptar el mundo sin romper sus reglas",
        capabilities: new Set([
          "speak", "move", "interact", "observe_global_world", "spawn_entity",
          "despawn_entity", "modify_environment", "teleport_entity",
          "create_adventure", "manage_npc", "emit_world_event"
        ])
      };
      this.self = {
        performance: 0.5,
        success: 0,
        failure: 0
      };
      this.beliefs = new Map();
      this.memory = [];
      this.playerModels = new Map();
      this.lastPlan = null;
      this.activePlayerId = null;

      this.world.entities.set("dynara", {
        id: "dynara", name: "DYNARA", kind: "Agent",
        locationId: "system", active: true, properties: {}, tags: ["director"]
      });

      this.addLocation("Lobby", "lobby", true);
      this.addLocation("Bosque de prueba", "forest", true);
      this.addEntity({
        id: "door_01", name: "Puerta de prueba", kind: "Door",
        locationId: "lobby", active: true,
        properties: { open: "false", locked: "true" }, tags: ["interactive"]
      }, true);
      this.addEntity({
        id: "key_01", name: "Llave de prueba", kind: "Item",
        locationId: "forest", active: true, properties: {}, tags: ["pickup"]
      }, true);

      this.remember("system", "Runtime reiniciado.", 0.5);
    }

    addLocation(name, id, silent = false) {
      const location = {
        id: id || `location_${String(++this.entitySeq).padStart(4, "0")}`,
        name: name || "Location",
        kind: "Location",
        locationId: id || null,
        active: true,
        properties: {},
        tags: []
      };
      location.locationId = location.id;
      this.world.entities.set(location.id, location);
      if (!silent) this.record("location_added", `Se registró la ubicación ${location.name}.`, {
        sourceId: "system", targetId: location.id, locationId: location.id, importance: 0.25
      });
      return location;
    }

    addEntity(entity, silent = false) {
      const copy = {
        id: entity.id || `entity_${String(++this.entitySeq).padStart(4, "0")}`,
        name: entity.name || entity.id || "Entity",
        kind: entity.kind || "Object",
        locationId: entity.locationId || null,
        active: entity.active !== false,
        properties: { ...(entity.properties || {}) },
        tags: [...(entity.tags || [])]
      };
      this.world.entities.set(copy.id, copy);
      if (!silent) this.record("entity_spawned", `Apareció ${copy.name} (${copy.kind}).`, {
        sourceId: "dynara", targetId: copy.id, locationId: copy.locationId, importance: 0.55
      });
      return copy;
    }

    addPlayer(name = "Jugador") {
      const id = `player_${String(++this.playerSeq).padStart(4, "0")}`;
      const player = {
        id, name, kind: "Player", locationId: "lobby",
        active: true, properties: {}, tags: ["human"]
      };
      this.world.entities.set(id, player);
      this.playerModels.set(id, this.newPlayerModel(id));
      this.activePlayerId = id;
      this.record("player_joined", `${name} entró en la simulación.`, {
        sourceId: id, targetId: id, locationId: "lobby", importance: 0.8
      });
      return player;
    }

    newPlayerModel(id) {
      return {
        playerId: id,
        engagement: 0.55,
        frustration: 0.15,
        confusion: 0.20,
        curiosity: 0.50,
        stress: 0.10,
        confidence: 0.35,
        interactionCount: 0,
        interests: new Set(),
        recentInputs: []
      };
    }

    record(type, description, meta = {}) {
      const event = {
        sequence: ++this.eventSeq,
        type,
        description,
        sourceId: meta.sourceId || null,
        targetId: meta.targetId || null,
        locationId: meta.locationId || null,
        importance: clamp(meta.importance ?? 0.5),
        threat: clamp(meta.threat ?? 0),
        timeMinutes: this.world.timeMinutes
      };
      this.world.events.push(event);
      this.world.activeEvents.push(event);
      if (this.world.events.length > 1000) this.world.events.shift();
      if (this.world.activeEvents.length > 100) this.world.activeEvents.shift();
      this.observe(event);
      return event;
    }

    observe(event) {
      this.beliefs.set("event:last:type", {
        value: event.type, source: "WorldState", confidence: 1, time: this.world.timeMinutes
      });
      this.beliefs.set("event:last:description", {
        value: event.description, source: "WorldState", confidence: 1, time: this.world.timeMinutes
      });
      for (const entity of this.world.entities.values()) {
        this.beliefs.set(`entity:${entity.id}:location`, {
          value: entity.locationId || "", source: "WorldState", confidence: 1, time: this.world.timeMinutes
        });
        this.beliefs.set(`entity:${entity.id}:active`, {
          value: String(entity.active), source: "WorldState", confidence: 1, time: this.world.timeMinutes
        });
      }
      this.remember("world", event.description, event.importance);
    }

    remember(kind, text, importance = 0.5) {
      this.memory.push({
        time: this.world.timeMinutes,
        kind,
        text: text || "",
        importance: clamp(importance)
      });
      if (this.memory.length > 200) this.memory.shift();
    }

    observePlayerInput(playerId, text) {
      const model = this.playerModels.get(playerId);
      if (!model) throw new Error("Jugador desconocido: " + playerId);
      const n = fold(text);
      const question = /[?¿]/.test(text);
      const bored = /\b(aburrid[oa]|aburre|aburrimiento)\b/.test(n);
      const frustrated = /(no puedo|imposible|molest|frustrad)/.test(n);
      const curious = question || /(quiero saber|que pasa|que hay|explorar|investigar)/.test(n);

      model.interactionCount++;
      model.recentInputs.push(text);
      if (model.recentInputs.length > 20) model.recentInputs.shift();

      if (bored) {
        model.engagement = clamp(model.engagement - 0.18);
        model.frustration = clamp(model.frustration + 0.08);
      } else {
        model.engagement = clamp(model.engagement + 0.025);
      }

      model.frustration = clamp(model.frustration + (frustrated ? 0.18 : -0.015));
      model.confusion = clamp(model.confusion + (question ? 0.04 : -0.01));
      if (curious) model.curiosity = clamp(model.curiosity + 0.12);
      model.stress = clamp(model.stress + (/(miedo|peligro|demasiado|basta)/.test(n) ? 0.12 : -0.01));
      model.confidence = clamp(0.25 + Math.min(model.interactionCount, 20) * 0.03);

      for (const interest of ["misterio","terror","aventura","fantasia","ciencia","espacio","combate","puzzle","acertijo","explorar"]) {
        if (n.includes(interest)) model.interests.add(interest);
      }

      this.remember("player", `${playerId}: ${text}`, 0.65);
    }

    playerInput(text, playerId = this.activePlayerId) {
      if (!playerId) playerId = this.addPlayer("Jugador").id;
      const player = this.world.entities.get(playerId);
      if (!player || player.kind !== "Player") throw new Error("Jugador no válido.");
      this.activePlayerId = playerId;

      this.record("player_input", `${player.name}: ${text}`, {
        sourceId: playerId, targetId: "dynara", locationId: player.locationId, importance: 0.65
      });
      this.observePlayerInput(playerId, text);
      return this.runDirector();
    }

    average(field, fallback) {
      const models = [...this.playerModels.values()];
      if (!models.length) return fallback;
      return models.reduce((sum, item) => sum + item[field], 0) / models.length;
    }

    chooseTheme() {
      const counts = new Map();
      for (const model of this.playerModels.values()) {
        for (const interest of model.interests) counts.set(interest, (counts.get(interest) || 0) + 1);
      }
      if (!counts.size) return "misterio";
      return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))[0][0];
    }

    activeAdventure() {
      return [...this.world.adventures.values()].find(x => x.status === "active") || null;
    }

    buildPlan() {
      const players = [...this.world.entities.values()].filter(x => x.kind === "Player" && x.active);
      const plan = { goal: null, steps: [], trace: [] };

      if (!players.length) {
        plan.goal = { id: "wait_for_players", label: "esperar usuarios", priority: 0.20, reason: "no hay jugadores activos" };
        plan.steps.push({ actorId: "dynara", type: "wait" });
        plan.trace.push("Sin jugadores no se genera contenido que nadie puede experimentar.");
        return this.lastPlan = plan;
      }

      const engagement = this.average("engagement", 0.55);
      const frustration = this.average("frustration", 0.15);
      const stress = this.average("stress", 0.10);
      const active = this.activeAdventure();

      plan.trace.push(`players=${players.length}`);
      plan.trace.push(`engagement=${engagement.toFixed(2)}`);
      plan.trace.push(`frustration=${frustration.toFixed(2)}`);
      plan.trace.push(`stress=${stress.toFixed(2)}`);
      plan.trace.push(`activeAdventure=${active ? active.spec.id : "none"}`);

      if (!active) {
        const intensity = clamp(0.62 - frustration * 0.25 - stress * 0.35 + engagement * 0.18);
        plan.goal = {
          id: "create_experience", label: "crear una aventura para los jugadores",
          priority: 0.86, reason: "hay jugadores pero no existe una aventura activa"
        };
        plan.steps.push({
          actorId: "dynara", type: "create_adventure",
          template: this.chooseTheme(), parameters: { intensity }
        });
        plan.trace.push("La intensidad baja cuando frustración o estrés suben.");
      } else if (frustration > 0.72 || stress > 0.72) {
        plan.goal = {
          id: "reduce_pressure", label: "reducir presión de la experiencia",
          priority: 0.92, reason: "el modelo de usuarios estima frustración o estrés altos"
        };
        plan.steps.push({
          actorId: "dynara", type: "emit_event",
          text: "Dynara reduce temporalmente la presión del escenario y hace más legibles las salidas.",
          parameters: { eventType: "director_relief", importance: 0.75 }
        });
      } else if (engagement < 0.42) {
        plan.goal = {
          id: "increase_engagement", label: "introducir una novedad legible",
          priority: 0.82, reason: "engagement agregado por debajo del umbral"
        };
        plan.steps.push({
          actorId: "dynara", type: "spawn_entity",
          template: "Anomalía",
          locationId: players[0].locationId,
          parameters: {
            id: `stimulus_${String(++this.stimulusSeq).padStart(4, "0")}`,
            name: "Anomalía dinámica",
            kind: "Object",
            purpose: "crear curiosidad sin bloquear el progreso"
          }
        });
      } else {
        plan.goal = {
          id: "maintain_coherence", label: "mantener coherencia y observar consecuencias",
          priority: 0.55, reason: "la experiencia está activa y no existe una señal fuerte que justifique intervenir"
        };
        plan.steps.push({ actorId: "dynara", type: "observe", targetId: active.spec.id });
      }

      return this.lastPlan = plan;
    }

    requiredCapability(action) {
      return ({
        speak: "speak",
        move: "move",
        teleport: "teleport_entity",
        spawn_entity: "spawn_entity",
        despawn_entity: "despawn_entity",
        set_property: "modify_environment",
        create_adventure: "create_adventure",
        emit_event: "emit_world_event",
        observe: "observe_global_world"
      })[action.type] || null;
    }

    execute(action) {
      const required = this.requiredCapability(action);
      if (required && !this.profile.capabilities.has(required)) {
        return this.result(false, "capability_denied", `DYNARA no tiene ${required}.`);
      }

      switch (action.type) {
        case "wait":
          return this.result(true, "wait", "Dynara decidió esperar.");

        case "observe": {
          const event = this.record("agent_observed", "Dynara observó el estado global sin intervenir.", {
            sourceId: "dynara", targetId: action.targetId || null, importance: 0.15
          });
          return this.result(true, "agent_observed", event.description, event);
        }

        case "emit_event": {
          const event = this.record(action.parameters?.eventType || "director_event",
            action.text || "Dynara produjo un evento.", {
              sourceId: "dynara",
              targetId: action.targetId || null,
              locationId: action.locationId || null,
              importance: clamp(action.parameters?.importance ?? 0.55),
              threat: clamp(action.parameters?.threat ?? 0)
            });
          return this.result(true, event.type, event.description, event);
        }

        case "spawn_entity": {
          const p = action.parameters || {};
          const id = p.id || `entity_${String(++this.entitySeq).padStart(4, "0")}`;
          if (this.world.entities.has(id)) return this.result(false, "duplicate_entity", "Ya existe " + id + ".");
          const entity = this.addEntity({
            id, name: p.name || action.template || id,
            kind: p.kind || "Object", locationId: action.locationId || null,
            properties: p.purpose ? { purpose: p.purpose } : {},
            tags: ["director_spawn"]
          }, true);
          const event = this.record("entity_spawned", `Apareció ${entity.name} (${entity.kind}).`, {
            sourceId: "dynara", targetId: id, locationId: entity.locationId, importance: 0.55
          });
          return this.result(true, "entity_spawned", event.description, event, id);
        }

        case "despawn_entity": {
          const entity = this.world.entities.get(action.targetId);
          if (!entity) return this.result(false, "target_missing", "La entidad no existe.");
          entity.active = false;
          const event = this.record("entity_despawned", `${entity.name} fue retirado de la simulación.`, {
            sourceId: "dynara", targetId: entity.id, locationId: entity.locationId, importance: 0.55
          });
          return this.result(true, event.type, event.description, event);
        }

        case "teleport":
        case "move": {
          const id = action.targetId || action.actorId;
          const entity = this.world.entities.get(id);
          if (!entity) return this.result(false, "target_missing", "La entidad no existe.");
          if (!action.locationId) return this.result(false, "location_missing", "Falta locationId.");
          const previous = entity.locationId;
          entity.locationId = action.locationId;
          const event = this.record(action.type === "teleport" ? "entity_teleported" : "entity_moved",
            `${entity.name}: ${previous || "none"} -> ${action.locationId}`, {
              sourceId: "dynara", targetId: id, locationId: action.locationId, importance: 0.45
            });
          return this.result(true, event.type, event.description, event);
        }

        case "set_property": {
          const entity = this.world.entities.get(action.targetId);
          if (!entity) return this.result(false, "target_missing", "La entidad no existe.");
          const key = action.parameters?.key;
          if (!key) return this.result(false, "property_missing", "Falta key.");
          entity.properties[key] = String(action.parameters?.value ?? "");
          const event = this.record("entity_property_changed",
            `${entity.name}.${key} = ${entity.properties[key]}`, {
              sourceId: "dynara", targetId: entity.id, locationId: entity.locationId, importance: 0.4
            });
          return this.result(true, event.type, event.description, event);
        }

        case "create_adventure":
          return this.createAdventure(action);

        default:
          return this.result(false, "unsupported_action", "Acción no implementada: " + action.type);
      }
    }

    result(success, code, message, event = null, createdId = null) {
      return { success, code, message, event, createdId };
    }

    createAdventure(action) {
      const theme = action.template || action.text || "misterio";
      const intensity = clamp(action.parameters?.intensity ?? 0.55);
      const id = `adv_${String(++this.adventureSeq).padStart(4, "0")}`;
      const names = ["Mira", "Kiro", "Vexa", "Orin"];
      const npcName = names[(this.eventSeq + this.adventureSeq) % names.length];
      const spec = {
        id, theme,
        seed: Math.floor(this.world.timeMinutes * 1000) + this.eventSeq + 31,
        participants: [...this.world.entities.values()].filter(x => x.kind === "Player" && x.active).map(x => x.id),
        objectives: [
          "descubrir qué está alterando el escenario",
          "resolver el conflicto principal sin romper las reglas del mundo"
        ],
        beats: [
          "entrada: presentar una anomalía clara",
          "exploración: ofrecer dos rutas con información distinta",
          "complicación: introducir una consecuencia de las decisiones del grupo",
          "decisión: forzar una elección con coste visible",
          "resolución: cerrar el conflicto y registrar consecuencias"
        ],
        rules: [
          "toda amenaza debe tener al menos una salida legible",
          "los NPC no conocen información que no hayan percibido o recibido",
          "una acción resuelta no puede reescribirse retroactivamente"
        ],
        npcs: [{
          name: npcName, role: "guía ambiguo",
          purpose: "orientar al grupo sin resolver el problema por ellos",
          traits: ["curioso", "evasivo"]
        }],
        difficulty: clamp(0.35 + intensity * 0.45),
        targetIntensity: intensity,
        estimatedMinutes: 8 + ((this.eventSeq + 3) % 8)
      };
      this.world.adventures.set(id, {
        spec, status: "active", currentBeat: 0, startedAtMinutes: this.world.timeMinutes
      });
      const event = this.record("adventure_created",
        `Dynara creó ${id} · tema=${theme} · intensidad=${intensity.toFixed(2)}`, {
          sourceId: "dynara", targetId: id, importance: 0.9
        });
      return this.result(true, "adventure_created", event.description, event, id);
    }

    runDirector() {
      const plan = this.buildPlan();
      const results = plan.steps.map(step => this.execute(step));
      for (const result of results) {
        if (result.success) this.self.success++;
        else this.self.failure++;
        const total = this.self.success + this.self.failure;
        this.self.performance = total ? this.self.success / total : 0.5;
        this.remember(result.success ? "action_success" : "action_failure",
          `${result.code}: ${result.message}`, result.success ? 0.55 : 0.8);
      }
      return { plan, results };
    }

    tick(minutes = 1) {
      minutes = Math.max(0, Number(minutes) || 0);
      this.world.timeMinutes += minutes;
      this.world.activeEvents = [];
      for (const model of this.playerModels.values()) {
        model.frustration = clamp(model.frustration - minutes * 0.002);
        model.stress = clamp(model.stress - minutes * 0.002);
      }
      this.record("time_advanced", `La simulación avanzó ${minutes.toFixed(2)} minutos.`, {
        sourceId: "system", targetId: "dynara", importance: 0.15
      });
      return this.runDirector();
    }

    externalEvent(description, threat = 0, importance = 0.6) {
      const player = this.activePlayerId ? this.world.entities.get(this.activePlayerId) : null;
      this.record("external_event", description, {
        sourceId: "unity", locationId: player?.locationId || null,
        importance: clamp(importance), threat: clamp(threat)
      });
      return this.runDirector();
    }

    setMetric(metric, value, playerId = this.activePlayerId) {
      const model = this.playerModels.get(playerId);
      if (!model) throw new Error("No hay jugador activo.");
      if (!["engagement","frustration","confusion","curiosity","stress","confidence"].includes(metric))
        throw new Error("Métrica desconocida.");
      model[metric] = clamp(value);
      return model;
    }
  }

  const terminal = document.getElementById("terminal");
  const form = document.getElementById("terminal-form");
  const input = document.getElementById("command");
  const clearBtn = document.getElementById("clear-btn");
  const sim = new DynaraWebSim();

  function line(text, type = "system", tag = "") {
    const row = document.createElement("div");
    row.className = "line " + type;
    if (tag) {
      const label = document.createElement("span");
      label.className = "tag";
      label.textContent = "[" + tag + "]";
      row.appendChild(label);
    }
    row.appendChild(document.createTextNode(text));
    terminal.appendChild(row);
    terminal.scrollTop = terminal.scrollHeight;
  }

  function printCycle(cycle) {
    if (!cycle?.plan) return;
    const goal = cycle.plan.goal;
    line(`${goal.id} · ${goal.label} · p=${goal.priority.toFixed(2)} · ${goal.reason}`, "goal", "GOAL");
    for (const trace of cycle.plan.trace) line(trace, "system", "TRACE");
    for (const result of cycle.results) {
      line(`${result.success ? "OK" : "FAIL"} ${result.code} · ${result.message}`,
        result.success ? "action" : "error", "ACTION");
    }
  }

  function refresh() {
    document.getElementById("status-time").textContent = sim.world.timeMinutes.toFixed(1);
    document.getElementById("status-players").textContent =
      [...sim.world.entities.values()].filter(x => x.kind === "Player" && x.active).length;
    document.getElementById("status-adventure").textContent = sim.activeAdventure()?.spec.id || "none";
    document.getElementById("director-goal").textContent = sim.lastPlan?.goal?.label || "esperar usuarios";
    document.getElementById("director-performance").textContent = Math.round(sim.self.performance * 100) + "%";

    const entities = [...sim.world.entities.values()].filter(x => x.active);
    document.getElementById("world-preview").textContent = [
      `time: ${sim.world.timeMinutes.toFixed(1)} min`,
      `entities: ${entities.length}`,
      `events: ${sim.world.events.length}`,
      `adventures: ${sim.world.adventures.size}`,
      `active: ${sim.activeAdventure()?.spec.id || "none"}`,
      "",
      ...entities.slice(0, 8).map(x => `${x.id} [${x.kind}] @ ${x.locationId || "—"}`)
    ].join("\n");

    const model = sim.activePlayerId ? sim.playerModels.get(sim.activePlayerId) : null;
    document.getElementById("player-preview").textContent = model ? [
      `id: ${model.playerId}`,
      `engagement: ${model.engagement.toFixed(2)}`,
      `frustration: ${model.frustration.toFixed(2)}`,
      `confusion: ${model.confusion.toFixed(2)}`,
      `curiosity: ${model.curiosity.toFixed(2)}`,
      `stress: ${model.stress.toFixed(2)}`,
      `model confidence: ${model.confidence.toFixed(2)}`,
      `interests: ${[...model.interests].join(", ") || "—"}`
    ].join("\n") : "sin jugador activo";
  }

  function printHelp() {
    line([
      "COMANDOS",
      "/player [nombre]        añade y selecciona un jugador",
      "/use <player_id>        cambia el jugador activo",
      "/world                  resumen completo del WorldState",
      "/entities               lista entidades estructuradas",
      "/players                muestra modelos de usuarios",
      "/events [n]             últimos eventos",
      "/memory [n]             memoria del director",
      "/beliefs [n]            creencias globales de Dynara",
      "/mind                   self-model + objetivo + traza",
      "/plan                   ejecuta un ciclo del director",
      "/tick [min]             avanza la simulación",
      "/event <texto>          inyecta un evento externo",
      "/threat <0..1> <texto>  inyecta una amenaza",
      "/spawn <tipo> <nombre>  spawn administrativo",
      "/move <id> <location>   teletransporta entidad",
      "/adventure [tema]       crea una aventura manual",
      "/set <métrica> <0..1>   fuerza el modelo del jugador activo",
      "/capabilities           permisos administrativos de Dynara",
      "/reset                  reinicia la demo",
      "",
      "Texto sin / se interpreta como entrada del jugador activo."
    ].join("\n"), "system", "HELP");
  }

  function worldDump() {
    const active = sim.activeAdventure();
    return {
      timeMinutes: sim.world.timeMinutes,
      rules: Object.fromEntries(sim.world.rules),
      entities: [...sim.world.entities.values()],
      adventures: [...sim.world.adventures.values()].map(x => ({
        id: x.spec.id, theme: x.spec.theme, status: x.status,
        currentBeat: x.currentBeat, beat: x.spec.beats[x.currentBeat]
      })),
      activeAdventure: active?.spec.id || null
    };
  }

  function handleCommand(raw) {
    const text = raw.trim();
    if (!text) return;
    line(text, "user", text.startsWith("/") ? "CMD" : "PLAYER");

    if (!text.startsWith("/")) {
      try {
        const cycle = sim.playerInput(text);
        printCycle(cycle);
      } catch (error) {
        line(error.message, "error", "ERROR");
      }
      refresh();
      return;
    }

    const parts = text.split(/\s+/);
    const cmd = parts.shift().toLowerCase();
    const rest = parts.join(" ");

    try {
      switch (cmd) {
        case "/help":
          printHelp();
          break;

        case "/reset":
          sim.reset();
          terminal.innerHTML = "";
          line("Runtime reiniciado.", "system", "BOOT");
          break;

        case "/player": {
          const player = sim.addPlayer(rest || "Jugador");
          line(`${player.id} · ${player.name} @ ${player.locationId}`, "action", "PLAYER");
          printCycle(sim.runDirector());
          break;
        }

        case "/use":
          if (!sim.playerModels.has(rest)) throw new Error("No existe ese jugador.");
          sim.activePlayerId = rest;
          line("Jugador activo = " + rest, "action", "PLAYER");
          break;

        case "/world":
          line(JSON.stringify(worldDump(), null, 2), "system", "WORLD");
          break;

        case "/entities":
          line([...sim.world.entities.values()].map(x =>
            `${x.id.padEnd(16)} ${String(x.kind).padEnd(9)} active=${x.active} @ ${x.locationId || "—"} · ${x.name}`
          ).join("\n"), "system", "ENTITIES");
          break;

        case "/players":
          line([...sim.playerModels.values()].map(x =>
            `${x.playerId}: engagement=${x.engagement.toFixed(2)} frustration=${x.frustration.toFixed(2)} stress=${x.stress.toFixed(2)} curiosity=${x.curiosity.toFixed(2)}`
          ).join("\n") || "sin jugadores", "system", "PLAYERS");
          break;

        case "/events": {
          const n = Math.max(1, Math.min(50, Number(parts[0]) || 12));
          line(sim.world.events.slice(-n).map(e =>
            `#${e.sequence} t=${e.timeMinutes.toFixed(1)} [${e.type}] ${e.description}`
          ).join("\n") || "sin eventos", "system", "EVENTS");
          break;
        }

        case "/memory": {
          const n = Math.max(1, Math.min(50, Number(parts[0]) || 12));
          line(sim.memory.slice(-n).map(m =>
            `t=${m.time.toFixed(1)} [${m.kind}] p=${m.importance.toFixed(2)} ${m.text}`
          ).join("\n"), "system", "MEMORY");
          break;
        }

        case "/beliefs": {
          const n = Math.max(1, Math.min(80, Number(parts[0]) || 25));
          line([...sim.beliefs.entries()].slice(-n).map(([key, b]) =>
            `${key} = ${b.value} · conf=${b.confidence.toFixed(2)} · ${b.source}`
          ).join("\n"), "system", "BELIEFS");
          break;
        }

        case "/mind": {
          const p = sim.lastPlan;
          line([
            `identity=DYNARA`,
            `role=${sim.profile.role}`,
            `purpose=${sim.profile.purpose}`,
            `performance=${sim.self.performance.toFixed(2)}`,
            `successfulInterventions=${sim.self.success}`,
            `failedInterventions=${sim.self.failure}`,
            `goal=${p?.goal?.id || "—"} · ${p?.goal?.label || "—"}`,
            "",
            ...(p?.trace || []).map(x => "trace: " + x)
          ].join("\n"), "system", "MIND");
          break;
        }

        case "/plan":
          printCycle(sim.runDirector());
          break;

        case "/tick":
          printCycle(sim.tick(Number(parts[0]) || 1));
          break;

        case "/event":
          if (!rest) throw new Error("Uso: /event <descripción>");
          printCycle(sim.externalEvent(rest, 0, 0.6));
          break;

        case "/threat": {
          const threat = clamp(Number(parts.shift()));
          const desc = parts.join(" ");
          if (!desc) throw new Error("Uso: /threat <0..1> <descripción>");
          printCycle(sim.externalEvent(desc, threat, Math.max(0.6, threat)));
          break;
        }

        case "/spawn": {
          const kind = parts.shift() || "Object";
          const name = parts.join(" ") || "Entidad";
          const player = sim.activePlayerId ? sim.world.entities.get(sim.activePlayerId) : null;
          const result = sim.execute({
            actorId: "dynara", type: "spawn_entity", template: name,
            locationId: player?.locationId || "lobby",
            parameters: { kind, name }
          });
          line(`${result.success ? "OK" : "FAIL"} ${result.message}`, result.success ? "action" : "error", "ACTION");
          break;
        }

        case "/move": {
          const id = parts.shift();
          const locationId = parts.shift();
          if (!id || !locationId) throw new Error("Uso: /move <entity_id> <location_id>");
          const result = sim.execute({ actorId: "dynara", type: "teleport", targetId: id, locationId });
          line(`${result.success ? "OK" : "FAIL"} ${result.message}`, result.success ? "action" : "error", "ACTION");
          break;
        }

        case "/adventure": {
          const result = sim.execute({
            actorId: "dynara", type: "create_adventure",
            template: rest || sim.chooseTheme(),
            parameters: { intensity: 0.55 }
          });
          line(`${result.success ? "OK" : "FAIL"} ${result.message}`, result.success ? "action" : "error", "ACTION");
          break;
        }

        case "/set": {
          const metric = parts.shift();
          const value = Number(parts.shift());
          if (!metric || Number.isNaN(value)) throw new Error("Uso: /set <métrica> <0..1>");
          const model = sim.setMetric(metric, value);
          line(`${metric}=${model[metric].toFixed(2)}`, "action", "MODEL");
          break;
        }

        case "/capabilities":
          line([...sim.profile.capabilities].sort().join("\n"), "system", "CAPABILITIES");
          break;

        default:
          throw new Error("Comando desconocido. Usa /help.");
      }
    } catch (error) {
      line(error.message || String(error), "error", "ERROR");
    }

    refresh();
  }

  form.addEventListener("submit", event => {
    event.preventDefault();
    const value = input.value;
    input.value = "";
    handleCommand(value);
    input.focus();
  });

  clearBtn.addEventListener("click", () => {
    terminal.innerHTML = "";
    input.focus();
  });

  line("DYNARA Simulation Director Runtime iniciado.", "system", "BOOT");
  line("Arquitectura: WorldState → Perception → UserModel/Beliefs → Goal → Plan → ActionIntent → ActionResult.", "system", "BOOT");
  line("No hay jugadores. Usa /player Andrés o escribe directamente para crear Jugador.", "system", "BOOT");
  line("Usa /help para ver comandos de prueba.", "system", "BOOT");
  refresh();
  input.focus();

  window.DynaraSim = sim;
})();
