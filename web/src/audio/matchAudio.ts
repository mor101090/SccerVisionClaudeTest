// Match sounds for the web viewers: referee whistles, ball kicks, headers, saves, the woodwork, the net and a home crowd
// that reacts to what happens. Procedural by default; recorded sounds (loadSamples) replace the crowd's anger, the
// celebration, the booing, the applause, the net and the woodwork when they are there. Mirrors Unity's MatchAudio.
// Browsers only allow audio after a user gesture, so call enable() from a click handler.

export interface SoundEvent {
  type: string;
  whistle?: string | null;
  detail?: number;
  /** Event value: ball speed (m/s) for NetHit and HitWoodwork. */
  value?: number;
  /** 0 = home, 1 = away, -1 = none (as in the engine's events). */
  team?: number;
  homeScore?: number;
  awayScore?: number;
  /** Match minute (for the crowd's sense of time-wasting). */
  minute?: number;
}

/** Vowel formants (Hz) for the crowd's voices. */
const VOWELS = {
  oo: [330, 820],
  aw: [570, 900],
  ah: [760, 1220],
  eh: [560, 1750],
} as const;
type Vowel = keyof typeof VOWELS;

/**
 * Referee whistle signals: [blast seconds, gap after], loudness and air pressure (pitch). Which events get a whistle
 * follows the IFAB practical guidelines; the lengths follow refereeing practice (stronger offence, stronger blast).
 */
const WHISTLES: Record<string, { blasts: [number, number][]; gain: number; pressure: number }> = {
  Short: { blasts: [[0.34, 0]], gain: 0.5, pressure: 1 }, // kick-off / restart
  Light: { blasts: [[0.17, 0]], gain: 0.36, pressure: 0.97 }, // offside, technical offence
  Foul: { blasts: [[0.3, 0]], gain: 0.72, pressure: 1.03 }, // sharp, firm
  Long: { blasts: [[1.1, 0]], gain: 0.9, pressure: 1.05 }, // penalty, sending-off offence
  HalfTime: { blasts: [[0.4, 0.28], [1.0, 0]], gain: 0.8, pressure: 1.03 },
  FullTime: { blasts: [[0.3, 0.22], [0.3, 0.22], [1.4, 0]], gain: 0.85, pressure: 1.04 },
};

/** The recorded sounds the viewer can use (file names under the sample folder, without ".mp3"). */
export const SAMPLE_NAMES = ["fan_angry", "after_goal", "booing", "clapping", "net", "crossbar", "goalpost"] as const;
export type SampleName = (typeof SAMPLE_NAMES)[number];

export class MatchSound {
  private ctx: AudioContext | null = null;
  private samples: Partial<Record<SampleName, AudioBuffer>> = {};
  private sampleBase: string | null = null;
  private master: GainNode | null = null;
  private crowd: GainNode | null = null;
  private crowdLevel = 0.14;
  private boost = 0;
  private hush = 0;
  private noiseBuf: AudioBuffer | null = null;
  private clapBuf: AudioBuffer | null = null;
  private peaBuf: AudioBuffer | null = null;
  volume = 0.8;

  get enabled(): boolean {
    return this.ctx !== null && this.ctx.state === "running";
  }

  enable(): void {
    if (!this.ctx) {
      const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      if (!Ctor) return;
      this.ctx = new Ctor();
      this.master = this.ctx.createGain();
      this.master.gain.value = this.volume;
      const comp = this.ctx.createDynamicsCompressor();
      this.master.connect(comp).connect(this.ctx.destination);
      this.noiseBuf = this.makeNoise(2);
      this.clapBuf = this.makeApplause(3);
      this.peaBuf = this.makePeaNoise(2);
      this.startCrowd();
      if (this.sampleBase) void this.fetchSamples(this.sampleBase);
    }
    void this.ctx.resume();
  }

  /** Uses the recorded sounds in <base> (e.g. "sounds/"); a missing file keeps the procedural sound. */
  loadSamples(base: string): void {
    this.sampleBase = base;
    if (this.ctx) void this.fetchSamples(base);
  }

  private async fetchSamples(base: string) {
    const ctx = this.ctx;
    if (!ctx) return;
    await Promise.all(
      SAMPLE_NAMES.map(async (n) => {
        if (this.samples[n]) return;
        try {
          const r = await fetch(base + n + ".mp3");
          if (!r.ok) return;
          this.samples[n] = await ctx.decodeAudioData(await r.arrayBuffer());
        } catch {
          // (no such file: the procedural sound stays)
        }
      }),
    );
  }

  /**
   * Plays a recorded sound. wide: from every side of the stadium (copies spread left, centre and right); otherwise from
   * one place (pan -1..1) - a far-away section is quieter and duller. echo 0..1: the ring of a stadium (a harder hit
   * echoes more). maxSeconds cuts it short (with a fade).
   */
  private sample(name: SampleName, gain: number, opts: { wide?: boolean; pan?: number; echo?: number; maxSeconds?: number; far?: boolean } = {}): boolean {
    const buf = this.samples[name];
    const ctx = this.ctx;
    if (!buf || !ctx) return false;
    const now = ctx.currentTime;
    const len = Math.min(buf.duration, opts.maxSeconds ?? buf.duration);
    const pans = opts.wide ? [-0.85, 0, 0.85] : [opts.pan ?? 0];
    const each = opts.wide ? gain * 0.62 : gain;
    pans.forEach((p, k) => {
      const src = ctx.createBufferSource();
      src.buffer = buf;
      const g = ctx.createGain();
      const at = now + (opts.wide ? k * 0.035 : 0); // (a few ms apart: the sound arrives from all round)
      g.gain.setValueAtTime(each, at);
      g.gain.setValueAtTime(each, at + Math.max(0, len - 0.4));
      g.gain.linearRampToValueAtTime(0.0001, at + len);
      let node: AudioNode = src;
      if (opts.far) {
        const lp = ctx.createBiquadFilter();
        lp.type = "lowpass";
        lp.frequency.value = 1400;
        node.connect(lp);
        node = lp;
      }
      node.connect(g);
      const pan = ctx.createStereoPanner();
      pan.pan.value = p;
      g.connect(pan).connect(this.out());
      if (opts.echo && opts.echo > 0.02) {
        // a short feedback delay: the stadium answers the hit
        const d = ctx.createDelay(1);
        d.delayTime.value = 0.11 + 0.1 * opts.echo;
        const fb = ctx.createGain();
        fb.gain.value = 0.2 + 0.35 * opts.echo;
        const wet = ctx.createGain();
        wet.gain.value = 0.25 + 0.45 * opts.echo;
        g.connect(d);
        d.connect(fb).connect(d);
        d.connect(wet).connect(pan);
      }
      src.start(at, 0, len);
    });
    return true;
  }

  /** The home fans furious with the referee (or a goal against): from all round the ground. */
  private angry(gain = 0.9) {
    if (!this.sample("fan_angry", gain, { wide: true })) this.boo(3.5, 1);
  }

  /** The home crowd booing (the recording is cut before its applause starts, at 14.5 s). */
  private booing(gain = 0.85) {
    if (!this.sample("booing", gain, { wide: true, maxSeconds: 14.5 })) this.boo(3.2, 1);
  }

  /** Applause for a home shot that did not go in. */
  private clapping(gain = 0.6) {
    if (!this.sample("clapping", gain, { wide: true, maxSeconds: 4 })) this.applause(2.2, 0.45);
  }

  disable(): void {
    void this.ctx?.suspend();
  }

  /** Plays the sound that belongs to a match event (no-op while disabled). */
  play(e: SoundEvent): void {
    if (!this.enabled) return;
    const home = e.team === 0;
    const away = e.team === 1;
    switch (e.type) {
      case "Whistle":
        this.whistle(e.whistle ?? "Short");
        break;
      case "Pass":
      case "LongBall":
      case "Clearance":
      case "Cross":
        this.thud(95, 0.1, 0.22);
        break;
      case "Shot":
        // side-footed finishes and chips are struck softly; volleys and free kicks are hit hardest
        this.thud(70, 0.16, e.detail === 1 || e.detail === 3 || e.detail === 15 ? 0.75 : e.detail === 6 || e.detail === 7 || e.detail === 9 || e.detail === 16 ? 0.38 : 0.6);
        if (home) this.voices("oo", 210, 290, 1.4, 0.45); // the home end rises with the shot
        else this.addBoost(0.05);
        break;
      case "Header":
        this.thud(150, 0.07, 0.4);
        break;
      case "Save":
        this.thud(190, 0.08, 0.45);
        // e.team is the keeper's side
        if (home) this.applause(2.2, 0.5);
        else {
          this.voices("aw", 250, 160, 1.8, 0.5); // "ohhh" - so close
          this.clapping(0.5);
        }
        break;
      case "GoalkeeperClaim":
        this.thud(190, 0.08, 0.4);
        break;
      case "ShotOffTarget":
        if (home) {
          this.voices("aw", 240, 150, 1.6, 0.4);
          this.clapping(0.45);
        }
        break;
      case "ShotBlocked":
        // e.team is the blocker's side: a home shot blocked by the visitors
        if (away) this.clapping(0.4);
        break;
      case "HitWoodwork": {
        // how hard it hit decides how loud it rings and how long the stadium echoes it
        const v = e.value ?? 12;
        const hard = Math.max(0, Math.min(1, (v - 6) / 24));
        if (!this.sample(e.detail === 2 ? "crossbar" : "goalpost", 0.35 + 0.65 * hard, { echo: hard })) this.woodwork(e.detail === 2, v);
        if (home) this.clapping(0.5);
        this.voices("oo", 240, 300, 1.0, home ? 0.7 : 0.45);
        if (home) this.voices("aw", 260, 160, 1.6, 0.5, 0.9);
        break;
      }
      case "Goal":
        // the net's own sound comes with the NetHit event right after (it knows how hard the ball hit it)
        if (home) {
          // the whole ground goes up
          if (!this.sample("after_goal", 1, { wide: true })) {
            this.voices("ah", 270, 340, 5.5, 1);
            this.voices("eh", 300, 360, 4, 0.5, 0.4);
            this.applause(6, 0.8, 1.5);
          }
          this.boost = 0.3;
        } else {
          // The away section celebrates in one corner, far and quieter; the home fans are furious all round.
          if (!this.sample("after_goal", 0.32, { pan: 0.8, far: true })) this.voices("ah", 280, 330, 4, 0.22, 0.2, 900);
          if (this.samples.fan_angry) this.angry(0.7);
          else this.voices("aw", 220, 140, 1.5, 0.45);
          this.hush = 1;
        }
        break;
      case "NetHit": {
        const v = e.value ?? 10;
        const hard = Math.max(0, Math.min(1, (v - 4) / 22));
        if (!this.sample("net", 0.3 + 0.7 * hard, { echo: hard * 0.7 })) this.net(v);
        break;
      }
      case "Foul":
        // e.team is the offender's side: a free kick against the home team angers the home end; the visitors' fouls are booed
        if (away) this.boo(1.8, 0.65);
        else if (this.samples.fan_angry) this.angry(0.75);
        else this.voices("aw", 200, 170, 0.9, 0.15);
        break;
      case "YellowCard":
        if (away) this.applause(1.5, 0.35);
        else this.angry(0.8);
        break;
      case "RedCard":
        if (away) {
          this.voices("ah", 260, 320, 2.5, 0.6);
          this.applause(3, 0.6);
        } else {
          this.angry(0.9);
          this.booing(0.8);
        }
        break;
      case "PenaltyAwarded":
        // e.team is the side given the penalty: against the home team the ground erupts in anger, and the away taker is booed
        if (home) {
          this.voices("ah", 270, 330, 3, 0.85);
          this.applause(3, 0.5);
        } else {
          this.angry(1);
          this.booing(0.7);
        }
        break;
      case "Substitution":
        // a late change by the visitors when they are ahead: wasting time - booed
        if (away && (e.awayScore ?? 0) > (e.homeScore ?? 0) && (e.minute ?? 0) >= 70) this.booing(0.6);
        break;
      case "GoalkeeperHoldViolation":
        if (away) this.booing(0.6);
        break;
      case "PenaltyMissed":
        if (home) this.voices("aw", 260, 140, 2.2, 0.9);
        else {
          this.voices("ah", 260, 330, 3, 0.8);
          this.applause(3, 0.6);
        }
        break;
      case "Offside":
        if (home) this.voices("aw", 210, 170, 0.9, 0.2);
        else this.applause(1, 0.25);
        break;
      case "Corner":
        if (home) this.applause(1.6, 0.3);
        break;
      case "FreeKick":
        if (home && e.detail === 1) this.voices("oo", 180, 230, 2, 0.2);
        break;
      case "FullTime": {
        const h = e.homeScore ?? 0;
        const a = e.awayScore ?? 0;
        if (h >= a) this.applause(6, h > a ? 0.9 : 0.5, 1);
        if (h > a) this.voices("ah", 260, 320, 4, 0.6);
        if (h < a) this.boo(4, 0.8);
        break;
      }
      case "HalfTime":
        this.applause(3, 0.4);
        break;
    }
  }

  /** Call every frame with the elapsed seconds so the crowd swell (and the hush after a goal conceded) decays. */
  tick(dt: number): void {
    if (!this.crowd || !this.ctx) return;
    this.boost = Math.max(0, this.boost - dt * 0.1);
    this.hush = Math.max(0, this.hush - dt * 0.12);
    const level = (this.crowdLevel + this.boost) * (1 - 0.65 * this.hush);
    this.crowd.gain.setTargetAtTime(level, this.ctx.currentTime, 0.25);
  }

  private addBoost(v: number) {
    this.boost = Math.max(this.boost, v);
  }

  private makeNoise(seconds: number): AudioBuffer {
    const ctx = this.ctx!;
    const buf = ctx.createBuffer(1, Math.floor(ctx.sampleRate * seconds), ctx.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    return buf;
  }

  /**
   * Irregular, band-limited jitter that drives the whistle's frequency: the cork pea rattling round the chamber
   * interrupts the air jet a few dozen times a second, but not periodically.
   */
  private makePeaNoise(seconds: number): AudioBuffer {
    const ctx = this.ctx!;
    const buf = ctx.createBuffer(1, Math.floor(ctx.sampleRate * seconds), ctx.sampleRate);
    const d = buf.getChannelData(0);
    let phase = 0;
    let lp = 0;
    for (let i = 0; i < d.length; i++) {
      const t = i / ctx.sampleRate;
      // pea rotation ~ 30-45 Hz wandering, with random kicks
      const rate = 36 + 7 * Math.sin(t * 2.3) + 4 * Math.sin(t * 5.9);
      phase += (2 * Math.PI * rate) / ctx.sampleRate;
      lp += (Math.random() * 2 - 1 - lp) * 0.02;
      d[i] = 0.75 * Math.sign(Math.sin(phase)) * Math.abs(Math.sin(phase)) ** 0.5 + lp * 2.2;
    }
    return buf;
  }

  /** Many hands clapping: random short bursts of band-passed noise. */
  private makeApplause(seconds: number): AudioBuffer {
    const ctx = this.ctx!;
    const sr = ctx.sampleRate;
    const buf = ctx.createBuffer(1, Math.floor(sr * seconds), sr);
    const d = buf.getChannelData(0);
    const claps = Math.floor(seconds * 420);
    for (let c = 0; c < claps; c++) {
      const start = Math.floor(Math.random() * (d.length - sr * 0.02));
      const len = Math.floor(sr * (0.004 + Math.random() * 0.008));
      const amp = 0.25 + Math.random() * 0.75;
      let lp = 0;
      const k = 0.3 + Math.random() * 0.5;
      for (let i = 0; i < len; i++) {
        lp += (Math.random() * 2 - 1 - lp) * k;
        d[start + i] += lp * amp * Math.exp(-i / (len * 0.35));
      }
    }
    let peak = 0;
    for (let i = 0; i < d.length; i++) peak = Math.max(peak, Math.abs(d[i]));
    for (let i = 0; i < d.length; i++) d[i] /= peak || 1;
    return buf;
  }

  private out(): AudioNode {
    return this.master!;
  }

  private startCrowd() {
    const ctx = this.ctx!;
    const src = ctx.createBufferSource();
    src.buffer = this.noiseBuf;
    src.loop = true;
    const lp = ctx.createBiquadFilter();
    lp.type = "lowpass";
    lp.frequency.value = 650;
    this.crowd = ctx.createGain();
    this.crowd.gain.value = this.crowdLevel;
    src.connect(lp).connect(this.crowd).connect(this.out());
    src.start();
  }

  // ---- Referee whistle ------------------------------------------------------------------------------------

  private whistle(kind: string) {
    const w = WHISTLES[kind] ?? WHISTLES.Short;
    let at = this.ctx!.currentTime + 0.01;
    w.blasts.forEach(([len, gap], i) => {
      // the last blast of a sequence is blown hardest
      const last = i === w.blasts.length - 1 && w.blasts.length > 1;
      this.blast(at, len, w.gain * (last ? 1.08 : 1), w.pressure * (last ? 1.02 : 1));
      at += len + gap;
    });
  }

  /**
   * One blast of a pea whistle (Acme Thunderer style): a bright ~2.8 kHz tone with strong 2nd/3rd harmonics,
   * deep irregular frequency and amplitude modulation from the pea, a pitch that slides up as the breath
   * builds and drops as it fades, and breath noise around the tone.
   */
  private blast(at: number, len: number, gain: number, pressure: number) {
    const ctx = this.ctx!;
    const f0 = 2800 * pressure;
    const osc = ctx.createOscillator();
    osc.setPeriodicWave(ctx.createPeriodicWave(new Float32Array([0, 0, 0, 0, 0]), new Float32Array([0, 1, 0.42, 0.2, 0.06])));
    // pitch envelope: slide in, hold, slide out
    osc.frequency.setValueAtTime(f0 * 0.82, at);
    osc.frequency.exponentialRampToValueAtTime(f0, at + 0.035);
    osc.frequency.setValueAtTime(f0, at + Math.max(0.04, len - 0.05));
    osc.frequency.exponentialRampToValueAtTime(f0 * 0.88, at + len);
    // the pea: irregular FM (about +/- 6%) ...
    const pea = ctx.createBufferSource();
    pea.buffer = this.peaBuf;
    pea.loop = true;
    const peaLp = ctx.createBiquadFilter();
    peaLp.type = "lowpass";
    peaLp.frequency.value = 220;
    const fmDepth = ctx.createGain();
    fmDepth.gain.value = f0 * 0.055;
    pea.connect(peaLp).connect(fmDepth).connect(osc.frequency);
    // ... and the matching flutter in loudness
    const amp = ctx.createGain();
    amp.gain.setValueAtTime(0, at);
    amp.gain.linearRampToValueAtTime(gain * 1.1, at + 0.012);
    amp.gain.linearRampToValueAtTime(gain, at + 0.05);
    amp.gain.setValueAtTime(gain, at + Math.max(0.05, len - 0.045));
    amp.gain.linearRampToValueAtTime(0, at + len);
    const flutter = ctx.createGain();
    flutter.gain.value = 0.72;
    const amDepth = ctx.createGain();
    amDepth.gain.value = 0.28;
    peaLp.connect(amDepth).connect(flutter.gain);
    // breath noise around the tone
    const breath = ctx.createBufferSource();
    breath.buffer = this.noiseBuf;
    const bp = ctx.createBiquadFilter();
    bp.type = "bandpass";
    bp.frequency.value = f0;
    bp.Q.value = 4;
    const breathGain = ctx.createGain();
    breathGain.gain.value = 0.16;
    breath.connect(bp).connect(breathGain).connect(flutter);
    const hp = ctx.createBiquadFilter();
    hp.type = "highpass";
    hp.frequency.value = 900;
    osc.connect(flutter).connect(hp).connect(amp).connect(this.out());
    const end = at + len + 0.05;
    osc.start(at);
    osc.stop(end);
    pea.start(at, Math.random());
    pea.stop(end);
    breath.start(at, Math.random());
    breath.stop(end);
  }

  // ---- Crowd -----------------------------------------------------------------------------------------------

  /**
   * A crowd shouting a vowel: many detuned voices (sawtooths with their own vibrato and onsets) through vowel
   * formants, plus breath. Pitch glides from f0 to f1 (rising "ooh", falling "aww", sustained roar).
   */
  private voices(vowel: Vowel, f0: number, f1: number, len: number, vol: number, delay = 0, lowpass = 3200) {
    const ctx = this.ctx!;
    const t = ctx.currentTime + delay;
    const [F1, F2] = VOWELS[vowel];
    const bus = ctx.createGain();
    const env = ctx.createGain();
    const attack = Math.min(0.35, len * 0.25);
    env.gain.setValueAtTime(0.0001, t);
    env.gain.linearRampToValueAtTime(vol * 0.5, t + attack);
    env.gain.linearRampToValueAtTime(vol * 0.42, t + len * 0.6);
    env.gain.linearRampToValueAtTime(0.0001, t + len);
    const form1 = ctx.createBiquadFilter();
    form1.type = "bandpass";
    form1.frequency.value = F1;
    form1.Q.value = 3;
    const form2 = ctx.createBiquadFilter();
    form2.type = "bandpass";
    form2.frequency.value = F2;
    form2.Q.value = 4;
    const g2 = ctx.createGain();
    g2.gain.value = 0.55;
    const lp = ctx.createBiquadFilter();
    lp.type = "lowpass";
    lp.frequency.value = lowpass;
    bus.connect(form1).connect(env);
    bus.connect(form2).connect(g2).connect(env);
    env.connect(lp).connect(this.out());
    const n = 22;
    for (let i = 0; i < n; i++) {
      const spread = 0.72 + Math.random() * 0.62; // men and women, low and high
      const o = ctx.createOscillator();
      o.type = "sawtooth";
      const start = t + Math.random() * Math.min(0.3, len * 0.2);
      o.frequency.setValueAtTime(f0 * spread, start);
      o.frequency.linearRampToValueAtTime(f1 * spread * (0.95 + Math.random() * 0.1), t + len);
      const vib = ctx.createOscillator();
      vib.frequency.value = 4 + Math.random() * 3;
      const vibDepth = ctx.createGain();
      vibDepth.gain.value = f0 * spread * 0.02;
      vib.connect(vibDepth).connect(o.frequency);
      const vg = ctx.createGain();
      vg.gain.value = 0.2;
      o.connect(vg).connect(bus);
      o.start(start);
      vib.start(start);
      o.stop(t + len + 0.05);
      vib.stop(t + len + 0.05);
    }
    const breath = ctx.createBufferSource();
    breath.buffer = this.noiseBuf;
    breath.loop = true;
    const bg = ctx.createGain();
    bg.gain.value = 0.5;
    breath.connect(bg).connect(bus);
    breath.start(t, Math.random());
    breath.stop(t + len + 0.05);
  }

  private boo(len: number, vol: number) {
    this.voices("oo", 135, 120, len, vol * 0.9);
  }

  private applause(len: number, vol: number, delay = 0) {
    const ctx = this.ctx!;
    const t = ctx.currentTime + delay;
    const src = ctx.createBufferSource();
    src.buffer = this.clapBuf;
    src.loop = true;
    const bp = ctx.createBiquadFilter();
    bp.type = "bandpass";
    bp.frequency.value = 1900;
    bp.Q.value = 0.7;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(vol * 0.55, t + 0.25);
    g.gain.linearRampToValueAtTime(vol * 0.4, t + len * 0.6);
    g.gain.linearRampToValueAtTime(0.0001, t + len);
    src.connect(bp).connect(g).connect(this.out());
    src.start(t, Math.random() * 2);
    src.stop(t + len + 0.05);
  }

  // ---- Ball ------------------------------------------------------------------------------------------------

  /** Ball struck by a foot or head: a low pitched thump with a short noise click. */
  private thud(freq: number, len: number, vol: number) {
    const ctx = this.ctx!;
    const t = ctx.currentTime;
    const osc = ctx.createOscillator();
    osc.frequency.setValueAtTime(freq * 1.8, t);
    osc.frequency.exponentialRampToValueAtTime(freq * 0.6, t + len);
    const g = ctx.createGain();
    g.gain.setValueAtTime(vol * this.volume, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + len);
    osc.connect(g).connect(this.out());
    osc.start(t);
    osc.stop(t + len + 0.02);
    const n = ctx.createBufferSource();
    n.buffer = this.noiseBuf;
    const lp = ctx.createBiquadFilter();
    lp.type = "lowpass";
    lp.frequency.value = 1800;
    const ng = ctx.createGain();
    ng.gain.setValueAtTime(vol * 0.5, t);
    ng.gain.exponentialRampToValueAtTime(0.001, t + 0.04);
    n.connect(lp).connect(ng).connect(this.out());
    n.start(t);
    n.stop(t + 0.06);
  }

  /** A short band-passed noise burst: contact clicks and rattles. */
  private burst(at: number, len: number, freq: number, q: number, gain: number) {
    const ctx = this.ctx!;
    const n = ctx.createBufferSource();
    n.buffer = this.noiseBuf;
    const bp = ctx.createBiquadFilter();
    bp.type = "bandpass";
    bp.frequency.value = freq;
    bp.Q.value = q;
    const g = ctx.createGain();
    g.gain.setValueAtTime(gain, at);
    g.gain.exponentialRampToValueAtTime(0.0005, at + len);
    n.connect(bp).connect(g).connect(this.out());
    n.start(at, Math.random());
    n.stop(at + len + 0.02);
  }

  /** One decaying sine partial of a struck object (decays to -60 dB over `dur` seconds). */
  private partial(at: number, freq: number, gain: number, dur: number) {
    const ctx = this.ctx!;
    const o = ctx.createOscillator();
    o.frequency.value = freq;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, at);
    g.gain.linearRampToValueAtTime(gain, at + 0.002);
    g.gain.exponentialRampToValueAtTime(0.0005, at + dur);
    o.connect(g).connect(this.out());
    o.start(at);
    o.stop(at + dur + 0.02);
  }

  /**
   * The ball hitting the post or the crossbar of a goal: a hollow steel/aluminium tube. Layers:
   * 1. the contact: a hard click and the ball's dull thud (a ball "breathes" at about 150 Hz when kicked);
   * 2. the frame as a whole: bending modes of a long free-free beam, frequency ratios 1 : 2.76 : 5.40 : 8.93, which gives the
   *    deep "tonk" (the crossbar is lighter and rings longer than the ground-socketed post);
   * 3. the hollow tube's shell "ping": the squeeze-and-expand cylinder modes ring at 2-3 kHz (like an aluminium bat, 2200 and
   *    2800 Hz), decaying in a fraction of a second;
   * 4. the rattle of net fixings and the frame settling, a few small clicks right after.
   * Louder for a harder strike.
   */
  private woodwork(crossbar: boolean, speed: number) {
    const ctx = this.ctx!;
    const t = ctx.currentTime;
    const vol = Math.min(1, Math.max(0.3, 0.25 + speed / 30));
    this.burst(t, 0.008, 3200, 1.1, 0.8 * vol);
    this.thud(130, 0.09, 0.5 * vol);
    const f1 = crossbar ? 74 : 58;
    const tail = crossbar ? 0.7 : 0.38;
    // the lowest mode is felt more than heard on a normal speaker: the audible "tonk" comes from modes 2-4 (about 170-550 Hz)
    [0.2, 0.5, 0.45, 0.32].forEach((w, i) => this.partial(t, f1 * [1, 2.76, 5.4, 8.93][i] * (1 + (Math.random() - 0.5) * 0.01), w * vol, tail / (1 + i * 0.5)));
    const ping = crossbar ? [2200, 2800, 3650] : [1900, 2500, 3300];
    ping.forEach((f, i) => this.partial(t, f * (1 + (Math.random() - 0.5) * 0.015), (0.42 * vol) / (1 + i * 0.4), (crossbar ? 0.34 : 0.2) / (1 + i * 0.3)));
    for (let k = 0; k < 4; k++) this.burst(t + 0.03 + k * 0.045 + Math.random() * 0.03, 0.012, 1600 + Math.random() * 2200, 1.4, 0.18 * vol * (1 - k * 0.2));
  }

  /**
   * The ball hitting the back of the net. Netting is slack nylon cord: there is no ring and no hiss. What you hear is a soft low
   * "whump" as the ball stretches the net, a short muffled swish of cords sliding against each other (broad, about 150 Hz to
   * 2 kHz, with a quick flutter) and a few tiny cord ticks as it settles. Louder for a harder strike.
   */
  private net(speed: number) {
    const ctx = this.ctx!;
    const t = ctx.currentTime;
    const vol = Math.min(1, Math.max(0.25, 0.2 + speed / 28));
    this.thud(85, 0.14, 0.4 * vol);
    const n = ctx.createBufferSource();
    n.buffer = this.noiseBuf;
    const hp = ctx.createBiquadFilter();
    hp.type = "highpass";
    hp.frequency.value = 150;
    const lp = ctx.createBiquadFilter();
    lp.type = "lowpass";
    lp.frequency.value = 2000;
    const flutter = ctx.createGain();
    flutter.gain.value = 0.75;
    const lfo = ctx.createOscillator();
    lfo.frequency.value = 26 + Math.random() * 8;
    const lfoDepth = ctx.createGain();
    lfoDepth.gain.value = 0.25;
    lfo.connect(lfoDepth).connect(flutter.gain);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(1.3 * vol, t + 0.018);
    g.gain.exponentialRampToValueAtTime(0.0005, t + 0.5);
    n.connect(hp).connect(lp).connect(flutter).connect(g).connect(this.out());
    n.start(t, Math.random());
    lfo.start(t);
    n.stop(t + 0.55);
    lfo.stop(t + 0.55);
    for (let k = 0; k < 4; k++) this.burst(t + 0.05 + k * 0.07 + Math.random() * 0.05, 0.015, 900 + Math.random() * 1400, 1.0, 0.16 * vol);
  }
}
