# Architecture

## Layers

```
 React shell (web/)                 Unity player (unity/SoccerVision)
 ───────────────────               ─────────────────────────────────────────────
 menus, squad, tactics UI   <──>   Runtime/  MatchController, views, HUD, audio
 scoreboard / commentary    JSON   Runtime/Bridge  WebBridge + jslib
                            events                  │
                                                    ▼
                                   Simulation/  MatchEngine  (pure C#, no UnityEngine)
                                                    ▲
 sim/ (.NET 8): tests, SimRunner CLI ───────────────┘  same source files, compiled headless
```

The **simulation is engine-agnostic** (`SoccerVision.Simulation.asmdef` has `noEngineReferences: true`). One set of
source files is used in three places:

1. Unity renders it.
2. `sim/` compiles it as `netstandard2.1` with C# 9 (Unity's constraints) for tests and calibration.
3. Later, the server that plays league matches authoritatively. Manager games simulate matches on the server, and the
   client replays them or streams them live.

It uses its own deterministic RNG (PCG32) and its own `Vec2`, so a seed gives the same match on every runtime and at
every frame rate. The test `ResultIsIndependentOfFrameRate` checks this.

## Time model

| quantity | default | meaning |
|---|---|---|
| `HalfRealSeconds` | 600 | real seconds per 45 clock minutes (added time on top) |
| `HalftimeRealSeconds` | 120 | real seconds of the break |
| `MotionSpeed` | 2.25 | football seconds simulated per real second (players move at real speeds, shown 2.25x) |
| clock speed | 4.5 | clock seconds per real second (= 45·60/600) |

`MatchEngine.Advance(realSeconds)` accumulates real time and runs fixed physics steps (0.02 s of football each). The
renderer interpolates between the last two steps (`InterpolationAlpha`), so motion stays smooth at any frame rate. The
half-time break is counted in real time with no physics.

**Added time** is announced at 45:00 and 90:00 and is always a whole number of minutes. It is measured from the time
actually lost in that half, in the spirit of the IFAB "accurate time" approach: goal celebrations, the time from a
card's whistle to the restart, penalties, plus an estimate for other stoppages until substitutions and injuries exist.
After it expires, the referee ends the half at the next neutral moment, or at most 30 clock seconds later (an attack
may finish). A pending penalty is always completed (Law 7).

## Match flow

`MatchPhase`: `Stoppage` (setting up a restart) → `Live` → `Stoppage` / `GoalCelebration` → … → `HalfTimeBreak` → … → `FullTime`.

A restart (`Restart`) moves every player to a legal position. The layouts are kick-off, throw-in, goal kick (short or
long build-up), corner (attackers in zones, defenders zonal plus man-marking), free kick (a wall of 1-5 at 9.15 m,
attackers on the line) and penalty. The taker walks to the ball, and the kick is taken after a setup time or after a
hard limit (`MaxSetupSeconds`), so the match can never stall. Just before the kick, the referee pushes anyone still
inside a forbidden zone back to a legal position. The test-suite checks every restart of several matches against the laws.

## Each physics step (Live)

1. Team shape every 0.2 s (`TeamAI`). The 4-4-2 block slides with the ball. The defensive line height depends on
   tactics and on whether the ball carrier has time to play forward. Midfielders screen goal-side. Strikers hold the
   offside line. Fullbacks overlap. Wingers hug the touchline or tuck in (inverted foot). One unique marker is assigned
   per attacker in our half.
2. Ball duties every 0.1 s: the fastest interceptor of each team chases a loose ball (pitch-control time-to-intercept),
   and the intended receiver always goes. A presser and at most one second presser close down the carrier (design doc:
   never more than two).
3. Thinking (`OnBall`, `Goalkeeper`). The ball carrier compares options:

   ```
   utility = P(success) · value(target) − (1 − P) · (opponent value at loss point + risk premium)
   ```

   - value = Expected Threat (xT, Karun Singh's 12×8 grid); shots use an xG logistic model.
   - P(pass) uses each opponent's time to reach the lane (reaction + acceleration + top speed), body blocks, receiver
     pressure and execution error.
   - dribbles account for the crowd and take-ons; crosses estimate the aerial duel.
   - The choice adds noise that grows with poor *Decisions* and *Composure* ("decision trees + weighted randomness", design doc).
4. Movement: kinematic steering with acceleration and top speed from the attributes, slowed by fatigue (Stamina) and
   by physical pressure.
5. Ball: gravity, quadratic drag, Magnus curl and dip, bounce with restitution, rolling resistance. Post and crossbar
   collisions. A net that absorbs the ball.
6. Contacts: control or heavy touch, interceptions (whoever is first along the ball's path has priority; a surprised
   player can only body-block), blocks, headers and aerial duels, saves (difficulty from shot speed, distance, the
   keeper's distance to the shot path and his skills) with catch / parry / tip over, and claims or punches of crosses.
7. Tackles and take-ons: duels are resolved from attributes. Fouls lead to free kicks or penalties. Advantage is played.
   Cards follow Law 12: reckless challenges, stopping a promising attack, persistent infringement, DOGSO (red, or yellow
   inside the area when the defender genuinely played for the ball), second yellow → red. A sent-off player cannot be
   replaced (design doc) and the team re-shapes.
8. Offside: positions are snapshotted at every touch by a team-mate (Law 11). There is no offside directly from a goal
   kick, throw-in or corner. A deliberate play by an opponent resets it; a save or deflection does not. The assistant
   raises his flag.
9. Ball out of play: throw-in, goal kick or corner by last touch. A goal is the whole ball over the line between the
   posts and under the bar.

## Unity presentation (`Runtime/`)

| design doc item | implementation |
|---|---|
| field PNG | `PitchRenderer` draws Law 1 markings and nets procedurally; `fieldOverride` takes the designer's PNG |
| player circle + number + two legs | `PlayerView` (legs animate with speed and show the facing) |
| jump: circle grows, shadow stays | `PlayerView.UpdateBodyScale` |
| ball size/shadow height illusion, trail | `BallView` |
| referee + 2 linesmen moving with play | `OfficialView`. The referee runs the diagonal system; assistants follow the second-last defender |
| corner flags flutter | `CornerFlagView` |
| scoreboard top-left | `MatchHud` (IMGUI; the React shell can hide it and draw its own) |
| live commentary, colour-coded | `MatchHud` + `Simulation/Events/Commentary.cs` |
| active player label "#10 – Name" | `PlayerView` label |
| tooltip with skills on hover | `MatchHud.DrawTooltip` |
| whistle patterns, ball/net sounds, crowd | `MatchAudio` (synthesised placeholders) |

## React ↔ Unity bridge

The contract lives in `Runtime/Bridge/BridgeProtocol.cs` and mirrors `web/src/bridge/protocol.ts`.

- Unity → React (`window.dispatchReactUnityEvent` via `SoccerVisionBridge.jslib`): `MatchReady`, `MatchState`
  (4 Hz), `MatchEvent`. The payload is a JSON string.
- React → Unity: `sendMessage("SoccerVisionBridge", "OnCommand", json)`. Commands: `pause`, `resume`, `togglePause`,
  `setSpeed`, `setHud`, `restart`, `requestState`, `setTactics` (already applied by the engine).

## Next steps that the design already accounts for

- **Substitutions**: `TeamAgent.Bench` already holds the bench players. Add an `IMatchCommand` that swaps a bench
  agent into a slot at the next stoppage (Law 3), count the time for added time, and expose it in the
  quick-substitution bar.
- **Tactics panel**: the `TeamTactics` sliders (mentality, defensive line, width, tempo, pressing) are already read by
  the AI. `ChangeTacticsCommand` works now. Formation changes mean swapping `TeamAgent.Formation`.
- **Individual instructions**: these would become per-slot modifiers in `ComputeShapeTarget` / `ChooseAction`.
- **Server**: run `MatchEngine` in an ASP.NET service and stream snapshots. The engine is already deterministic and headless.
