# Dynara-sim

Dynara-sim es un experimento de director autónomo para una simulación persistente.

La meta no es crear un chatbot con poderes de administrador. La meta es construir un agente autónomo central que observe el mundo completo, mantenga memoria, modele a los usuarios, seleccione objetivos, planifique intervenciones, genere contenido y modifique la simulación mediante acciones administrativas validadas.

## Demo en GitHub Pages

Si Pages está configurado para main / root:

https://acewalt.github.io/Dynara-sim/

Comandos iniciales:

    /help
    /player Andrés
    /mind
    /world

También puedes escribir texto normal. Se trata como entrada del jugador activo y Dynara ejecuta un ciclo de director.

## Arquitectura

    PROPÓSITO
    administrar simulación + crear experiencias
                |
                v
           WORLD STATE
                |
                v
           WORLD EVENTS
                |
                v
        PERCEPTION SYSTEM
                |
       +--------+--------+
       |        |        |
       v        v        v
    BELIEFS   MEMORY   PLAYER MODELS
       \        |        /
        +------- v ------+
          DYNARA DIRECTOR
                |
                v
          GOAL SELECTION
                |
                v
             PLANNER
                |
                v
          ACTION INTENT
                |
                v
          ACTION GATEWAY
                |
                v
          ACTION RESULT
                |
          WORLD + MEMORY
                |
                +----> siguiente ciclo

## Principio central

DECIDIR != EJECUTAR

El ciclo correcto es:

    Decision
      -> ActionIntent
      -> ActionGateway
      -> validación
      -> ejecución
      -> ActionResult
      -> consecuencias

En Unity, una acción física no debería cambiar memoria o estado hasta que el motor confirme éxito o fracaso.

## Reutilización de NIA / npc-int

Este repo importa actualmente el snapshot completo de src/NpcInt.Core desde acewalt/npc-int.

Se reutilizan las piezas cognitivas desarrolladas para NIA: memoria, estado mental, objetivos, razonamiento, formación de ideas, modelos sociales y contratos neuronales.

La separación buscada es:

    NpcInt.Core
       |
       +------> NIA
       |
       +------> Dynara-sim
                    |
                    +--> Dynara Director
                    +--> NPC agents
                    +--> Simulation

Dynara no debe convertirse en una bifurcación permanente de NIA. A medio plazo NpcInt.Core debería pasar a ser una dependencia versionada compartida.

## Estructura actual

    Dynara-sim/
    |
    |-- index.html
    |-- styles.css
    |-- dynara-web.js
    |
    |-- src/
    |   |-- NpcInt.Core/
    |   |-- DynaraSim.Core/
    |       |-- WorldModels.cs
    |       |-- EventBus.cs
    |       |-- PerceptionSystem.cs
    |       |-- BeliefState.cs
    |       |-- UserModel.cs
    |       |-- AdventureDirector.cs
    |       |-- ActionGateway.cs
    |       |-- DynaraDirector.cs
    |       |-- SimulationKernel.cs
    |
    |-- unity/
    |   |-- DynaraSimulationBehaviour.cs
    |   |-- README.md
    |
    |-- docs/
    |   |-- ARCHITECTURE.md
    |   |-- NPC_INT_INTEGRATION.md
    |
    |-- .github/workflows/
        |-- validate.yml

## WorldState

El mundo no se almacena como frases.

Ejemplo:

    door_01
    kind = Door
    location = lobby
    properties:
      open = false
      locked = true

La frase "hay una puerta cerrada" es una interpretación, no la verdad primaria.

## Creencias

WorldState y BeliefState están separados.

    WorldState:
    Jax tiene key_03

    NPC:
    "creo que Jax tiene una llave"
    confidence = 0.62

    Dynara:
    entity:jax:inventory:key_03 = true
    confidence = 1.00
    source = WorldState

Dynara posee percepción global; los NPC normales no deberían tenerla.

## PlayerModel

Dynara mantiene modelos estimados por jugador:

- engagement
- frustration
- confusion
- curiosity
- stress
- confidence
- interests
- recent inputs

Son estimaciones del sistema, no verdades sobre la persona. confidence indica cuánto soporte tiene el modelo.

## Generación procedural

AdventureDirector ya produce una estructura formal con tema, participantes, objetivos, beats, reglas, NPC blueprints, dificultad, intensidad y duración estimada.

La generación actual es simbólica/determinista. Más adelante un modelo neuronal puede proponer temas, NPC, diálogos o beats, pero el resultado deberá convertirse primero a una representación estructurada y validarse antes de entrar al mundo.

## Permisos administrativos

Dynara obtiene capacidades explícitas:

- Speak
- Move
- Interact
- ObserveGlobalWorld
- SpawnEntity
- DespawnEntity
- ModifyEnvironment
- TeleportEntity
- CreateAdventure
- ManageNpc
- EmitWorldEvent

No se recomienda llenar el proyecto de comprobaciones por nombre del agente. Un agente es poderoso porque sus Capabilities lo permiten.

## Comandos de la terminal

    /help
    /player [nombre]
    /use <player_id>
    /world
    /entities
    /players
    /events [n]
    /memory [n]
    /beliefs [n]
    /mind
    /plan
    /tick [min]
    /event <texto>
    /threat <0..1> <texto>
    /spawn <tipo> <nombre>
    /move <entity_id> <location_id>
    /adventure [tema]
    /set <métrica> <0..1>
    /capabilities
    /reset

Prueba:

    /player Andrés
    /set engagement 0.2
    /plan

Con una aventura activa:

    /set frustration 0.9
    /plan

Dynara debería priorizar reducir presión en vez de introducir más dificultad.

## Unity

unity/DynaraSimulationBehaviour.cs inicializa el kernel sin meter la lógica de Dynara dentro de un MonoBehaviour.

El juego final deberá conectar ActionIntent a ejecutores reales: NavMesh, animación, interacción, escenas, spawn, físicas y audio.

## Validación

Cada push a main ejecuta:

    node --check dynara-web.js
    dotnet build src/NpcInt.Core/NpcInt.Core.csproj
    dotnet build src/DynaraSim.Core/DynaraSim.Core.csproj

## Próxima fase

1. AgentManager con múltiples instancias cognitivas.
2. Percepción espacial real para NPC.
3. Planner GOAP/HTN de varios pasos.
4. NpcBlueprint -> CognitiveAgent.
5. Beats narrativos condicionados por eventos.
6. Ejecución asíncrona real en Unity.
7. Persistencia/event sourcing.
8. Capa neuronal opcional para lenguaje y propuestas narrativas.

Más detalle en docs/ARCHITECTURE.md.
