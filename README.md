# SoccerVision

A football management game in the style of Football Manager, Sokker, Hattrick and Goleada, with fictional players.
The **match itself (gameplay) runs in Unity**; the **rest of the game is React**.

This first milestone is the gameplay foundation: a full **demo match, red vs blue, both in 4-4-2**, which you watch
from kick-off to the final whistle. Following the design document (`SoccerVisionDocument.pdf`) and the IFAB Laws of the
Game, it reaches 90 minutes plus added time with:

- **10 real minutes per half** (45 clock minutes, so the clock runs at 4.5x) **plus added time**,
- a **2 minute half-time break**,
- kick-offs (the first touch goes backwards, as the design document requires), throw-ins, goal kicks, corners,
  free kicks with walls, penalties, offside, fouls, advantage, yellow and red cards (second yellow, DOGSO), and the
  goalkeeper's 8-second rule (2025/26),
- restarts at a realistic pace: players reorganise (back into shape, into the box, into the wall) before the ball is
  played again,
- a referee (marked R) and two assistants (marked L) whose whistle follows the IFAB guidance (no whistle for a clear
  throw-in, goal kick, corner or goal; a light tweet for offside, a sharp blast for a foul, a long blast for a penalty,
  two blasts at half time, three at full time), synthesised as a pea whistle,
- a home crowd that roars for home goals, falls silent for away goals, boos the visitors' fouls, and "ooh"s and groans at chances,
- six goal celebrations (corner run, run to the fans, knee slide, hug with the assister, solo lap, and the rarer team
  huddle). Only the scorer and a few team-mates take part; the others jog back,
- goalkeeper sorties, headers with physically timed leaps, volleys, cut-inside shots from the wing, free kicks over the
  wall, a scoreboard, live commentary, a skills tooltip on hover, and the ball's height illusion and shadow.

## Repository layout

```
unity/SoccerVision/                 Unity 6.3 LTS project (open this folder in Unity Hub)
  Assets/SoccerVision/Simulation/   pure C# match engine (no UnityEngine references; asmdef noEngineReferences)
  Assets/SoccerVision/Runtime/      Unity presentation: pitch, players, ball, officials, HUD, audio, React bridge
  Assets/SoccerVision/Editor/       menu: Create Match Scene, Build WebGL for React
  Assets/Plugins/WebGL/             jslib that forwards events to React
sim/                                .NET 8 solution compiling the same engine sources headless
  SoccerVision.Simulation/          netstandard2.1 + C# 9 (Unity's constraints) build of the engine
  SoccerVision.Simulation.Tests/    xUnit: full matches, laws at every restart, physics, determinism
  SoccerVision.SimRunner/           CLI: calibration statistics, timelines, replay export
web/                                React 19 + Vite + TypeScript shell (react-unity-webgl 10)
docs/                               ARCHITECTURE.md, RESEARCH.md
```

## Running it

### Watch the match in Unity
1. Install **Unity 6.3 LTS** (6000.3.x) with the WebGL module.
2. In Unity Hub, choose **Add > Add project from disk** and select `unity/SoccerVision`.
3. Press **Play** in any scene. A match controller spawns automatically. To get a saved scene, use **SoccerVision > Create Match Scene**.
4. Keys: `Space` pause, `1`/`2`/`3`/`4` speed x1/x2/x4/x8 (for testing; x1 gives the real 10-minute halves), `H` toggles the HUD, `R` starts a new match.

### Inside React (WebGL)
1. In Unity, run **SoccerVision > Build WebGL for React**. It writes to `web/public/unity/SoccerVision`.
2. `cd web && npm install && npm run dev`, then open http://localhost:5173.

### Without Unity
- `cd web && npm run replay` records a full match with the C# engine (needs the .NET 8 SDK). The **Engine replay (dev)**
  tab plays it back in real time, or faster.
- `cd sim && dotnet test` runs the test-suite.
- `cd sim && dotnet run -c Release --project SoccerVision.SimRunner -- --matches 100` prints the calibration table against real-world numbers.
- `... -- --seed 7 --timeline` prints a minute-by-minute match report.

## Status of calibration (100 matches, seeds 9000-9099)

| per match | engine | top leagues |
|---|---|---|
| goals | 2.75 | 2.7-3.0 |
| fouls / yellow / red | 20.7 / 3.5 / 0.12 | 20-24 / 3.5-4.3 / 0.10-0.15 |
| penalties (converted) | 0.38 (76%) | 0.25-0.35 (75-80%) |
| direct free-kick shots | 0.63 | 0.6-1 |
| pass completion | 83% | 80-85% |
| shots blocked | 6.7 | 6-7 |
| added time 1st / 2nd half | 2.3 / 3.4 min | 1-3 / 3-7 min |
| real duration 1st half / break / 2nd half | 630 s / 120 s / 646 s | 600 s + added / 120 s / 600 s + added |

There are known gaps to tune next: shots (33 vs 25), corners (6.3 vs ~10), throw-ins (20 vs ~40) and offsides (3.1 vs ~4).
See `docs/RESEARCH.md`.
