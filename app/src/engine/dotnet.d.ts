/** Minimal typing of the exports the .NET WASM host (dotnet.js) gives the worker. */
export interface LiveEngineExports {
  Start(
    seed: number,
    mode: string,
    weather: string,
    maintenance: number,
    wind: number,
    windAngle: number,
    night: boolean,
    homePlan: string,
    competition: string,
    homeStrength: string,
    awayStrength: string,
  ): string;
  /** Next Start fields this squad at home (missing on engine builds older than 0d6734d7aeef). */
  UseHomeTeam?(team: string): void;
  Step(frames: number): number[] | Int32Array;
  Events(): string;
  Stats(): number[] | Int32Array;
  Finished(): boolean;
  FinalHeader(): string;
  SetPlanFor(team: number, plan: string): void;
  SubstituteFor(team: number, outNumber: number, inNumber: number): void;
  FrameLength(): number;
}

export interface DotnetModule {
  dotnet: {
    withDiagnosticTracing(enabled: boolean): {
      create(): Promise<{
        getAssemblyExports(name: string): Promise<{ LiveEngine: LiveEngineExports }>;
        getConfig(): { mainAssemblyName: string };
      }>;
    };
  };
}
