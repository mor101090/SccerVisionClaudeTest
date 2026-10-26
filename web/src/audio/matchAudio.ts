// Procedural match sounds for the web viewers (no asset files): referee whistles, ball kicks, headers, saves,
// the clang of the woodwork, net rustle and a home crowd that reacts to what happens. Mirrors Unity's MatchAudio.
// Browsers only allow audio after a user gesture, so call enable() from a click handler.

export interface SoundEvent {
  type: string;
  whistle?: string | null;
  detail?: number;
  /** 0 = home, 1 = away, -1 = none (as in the engine's events). */
  team?: number;
  homeScore?: number;
  awayScore?: number;
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

export class MatchSound {
  private ctx: AudioContext | null = null;
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
    }
    void this.ctx.resume();
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
        this.thud(70, 0.16, e.detail === 1 || e.detail === 3 ? 0.75 : 0.6);
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
        else this.voices("aw", 250, 160, 1.8, 0.5); // "ohhh" - so close
        break;
      case "GoalkeeperClaim":
        this.thud(190, 0.08, 0.4);
        break;
      case "ShotOffTarget":
        if (home) this.voices("aw", 240, 150, 1.6, 0.4);
        break;
      case "ShotBlocked":
        if (home) this.applause(1.2, 0.3);
        break;
      case "HitWoodwork":
        this.clang();
        this.voices("oo", 240, 300, 1.0, home ? 0.7 : 0.45);
        if (home) this.voices("aw", 260, 160, 1.6, 0.5, 0.9);
        break;
      case "Goal":
        this.net(0.5);
        if (home) {
          this.voices("ah", 270, 340, 5.5, 1);
          this.voices("eh", 300, 360, 4, 0.5, 0.4);
          this.applause(6, 0.8, 1.5);
          this.boost = 0.3;
        } else {
          // Home fans fall silent; the away section celebrates in the distance.
          this.voices("aw", 220, 140, 1.5, 0.45);
          this.voices("ah", 280, 330, 4, 0.22, 0.2, 900);
          this.hush = 1;
        }
        break;
      case "NetHit":
        this.net(0.3);
        break;
      case "Foul":
        // e.team is the offender's side: the home crowd boos the visitors' fouls.
        if (away) this.boo(1.8, 0.65);
        else this.voices("aw", 200, 170, 0.9, 0.15);
        break;
      case "YellowCard":
        if (away) this.applause(1.5, 0.35);
        else this.boo(2.2, 0.8);
        break;
      case "RedCard":
        if (away) {
          this.voices("ah", 260, 320, 2.5, 0.6);
          this.applause(3, 0.6);
        } else this.boo(3.2, 1);
        break;
      case "PenaltyAwarded":
        if (home) {
          this.voices("ah", 270, 330, 3, 0.85);
          this.applause(3, 0.5);
        } else this.boo(3.5, 1);
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

  /** Metallic ring of the ball hitting a post or crossbar. */
  private clang() {
    const ctx = this.ctx!;
    const t = ctx.currentTime;
    [410, 880, 1370, 2110, 3300].forEach((f, i) => {
      const o = ctx.createOscillator();
      o.frequency.value = f;
      const g = ctx.createGain();
      g.gain.setValueAtTime(0.5 / (1 + i * 0.6), t);
      g.gain.exponentialRampToValueAtTime(0.001, t + 0.9 / (1 + i * 0.5));
      o.connect(g).connect(this.out());
      o.start(t);
      o.stop(t + 1);
    });
    this.thud(900, 0.03, 0.4);
  }

  /** Net rustle: bright noise with a quick decay. */
  private net(vol: number) {
    const ctx = this.ctx!;
    const t = ctx.currentTime;
    const n = ctx.createBufferSource();
    n.buffer = this.noiseBuf;
    const hp = ctx.createBiquadFilter();
    hp.type = "highpass";
    hp.frequency.value = 2500;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(vol, t + 0.02);
    g.gain.exponentialRampToValueAtTime(0.001, t + 0.7);
    n.connect(hp).connect(g).connect(this.out());
    n.start(t);
    n.stop(t + 0.8);
    this.thud(110, 0.12, 0.3);
  }
}
