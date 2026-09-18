# Arquitectura de Dynara

Dynara es un **agente autónomo central que administra una simulación completa**. No se diseña como chatbot. El diálogo es una de sus posibles acciones.

```text
WorldState
   |
   v
WorldEvent
   |
   v
PerceptionSystem
   |
   +--> BeliefState
   +--> DirectorMemory
   +--> PlayerModel[]
   |
   v
DynaraDirector
   |
   v
Goal Selection
   |
   v
DirectorPlan
   |
   v
ActionIntent[]
   |
   v
ActionGateway
   |
   +--> capability validation
   +--> preconditions
   +--> execution
   |
   v
ActionResult
   |
   +--> WorldState
   +--> WorldEvent
   +--> memory / performance
   |
   +------------------------> next cycle
```

## Principios no negociables

### 1. La verdad del mundo no es una creencia

`WorldState` guarda lo que existe realmente. `BeliefState` guarda lo que un agente cree.

Dynara tiene `ObserveGlobalWorld`, por lo que gran parte de sus creencias puede tener confianza 1.0. Un NPC normal no debería recibir ese privilegio.

### 2. Decidir no equivale a ejecutar

```text
Decision -> ActionIntent -> validator/executor -> ActionResult
```

Las consecuencias sólo deben aplicarse después del resultado. En Unity esto será especialmente importante para movimiento, interacción, combate, animaciones y físicas.

### 3. Dynara es un agente con capacidades, no una excepción en el código

Los privilegios administrativos se expresan con `AgentCapability`:

- `ObserveGlobalWorld`
- `SpawnEntity`
- `DespawnEntity`
- `ModifyEnvironment`
- `TeleportEntity`
- `CreateAdventure`
- `ManageNpc`
- `EmitWorldEvent`

Un NPC común reutilizará el mismo runtime, pero tendrá una lista de capacidades mucho menor.

### 4. NpcInt.Core es cognición reutilizable

`src/NpcInt.Core` se importó desde `acewalt/npc-int` para conservar el cerebro desarrollado para NIA: memoria, estado mental, objetivos, razonamiento y modelos lingüísticos.

Dynara agrega una capa distinta:

```text
NpcInt.Core
   +
World simulation
   +
Director cognition
   +
Administrative actions
   +
Adventure generation
```

A medio plazo conviene convertir `NpcInt.Core` en paquete/versionado compartido para evitar mantener dos copias.

## Estado actual

La primera vertical funcional ya permite:

- mundo estructurado con entidades, ubicaciones, propiedades y reglas;
- log de eventos;
- percepción global para Dynara;
- creencias separadas de `WorldState`;
- modelos de jugador con confianza explícita;
- selección de objetivo director;
- generación procedural básica de aventuras;
- permisos administrativos;
- `ActionIntent -> ActionResult`;
- memoria de director;
- estimación de desempeño;
- terminal web de prueba;
- bridge inicial a Unity.

## Lo siguiente

La siguiente fase debería incorporar:

1. `AgentManager` para múltiples cerebros NpcInt dentro del mismo mundo.
2. Percepción local espacial para NPC: distancia, línea de visión, audio y obstáculos.
3. Planner GOAP/HTN para planes de varios pasos.
4. ejecutores Unity asíncronos: NavMesh, interacción, animación, spawn y escenas;
5. `NpcBlueprint -> CognitiveAgent` para que las aventuras generen NPC reales;
6. narrativa con beats que avancen por condiciones, no sólo por tiempo;
7. persistencia/event sourcing;
8. capa neuronal opcional para lenguaje y propuestas narrativas, siempre validada por estructuras.
