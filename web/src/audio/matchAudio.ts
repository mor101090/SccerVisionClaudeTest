// Procedural match sounds for the web viewers (no asset files): referee whistles, ball kicks, headers, saves,
// the clang of the woodwork, net rustle and the crowd. Mirrors the Unity MatchAudio placeholders.
// Browsers only allow audio after a user gesture, so call enable() from a click handler.

export interface SoundEvent {
  type: string;
  whistle?: string | null;
  detail?: number;
}

export class MatchSound {
  private ctx: AudioContext | null = null;
  private master: GainNode | null = null;
  private crowd: GainNode | null = null;
  private boost = 0;
  private noiseBuf: AudioBuffer | null = null;
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
      this.master.connect(this.ctx.destination);
      this.noiseBuf = this.makeNoise(2);
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
        this.thud(70, 0.16, e.detail === 1 ? 0.75 : 0.6);
        this.addBoost(0.08);
        break;
      case "Header":
        this.thud(150, 0.07, 0.4);
        break;
      case "Save":
      case "GoalkeeperClaim":
        this.thud(190, 0.08, 0.45);
        this.addBoost(0.12);
        break;
      case "HitWoodwork":
        this.clang();
        this.addBoost(0.15);
        break;
      case "Goal":
        this.net(0.5);
        this.cheer(4.5);
        this.boost = 0.3;
        break;
      case "NetHit":
        this.net(0.3);
        break;
      case "Foul":
      case "RedCard":
        this.boo();
        break;
    }
  }

  /** Call every frame with the elapsed seconds so the crowd swell decays. */
  tick(dt: number): void {
    if (!this.crowd || !this.ctx) return;
    this.boost = Math.max(0, this.boost - dt * 0.1);
    this.crowd.gain.setTargetAtTime(0.14 + this.boost, this.ctx.currentTime, 0.2);
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
    this.crowd.gain.value = 0.14;
    src.connect(lp).connect(this.crowd).connect(this.out());
    src.start();
  }

  /** Pea whistle: ~3 kHz tone with the pea's fast trill. */
  private whistle(kind: string) {
    const patterns: Record<string, [number[], number]> = {
      Short: [[0.28], 0.08],
      Foul: [[0.12, 0.12, 0.2], 0.06],
      Long: [[0.9], 0.1],
      HalfTime: [[0.45, 0.45], 0.18],
      FullTime: [[0.3, 0.3, 1.1], 0.16],
    };
    const [blasts, gap] = patterns[kind] ?? patterns.Short;
    const gain = kind === "Short" ? 0.3 : 0.5;
    let at = this.ctx!.currentTime + 0.01;
    for (const len of blasts) {
      this.blast(at, len, gain);
      at += len + gap;
    }
  }

  private blast(at: number, len: number, gain: number) {
    const ctx = this.ctx!;
    const osc = ctx.createOscillator();
    osc.type = "sine";
    osc.frequency.value = 2950;
    const trill = ctx.createOscillator();
    trill.frequency.value = 42;
    const trillDepth = ctx.createGain();
    trillDepth.gain.value = 180;
    trill.connect(trillDepth).connect(osc.frequency);
    const amp = ctx.createGain();
    amp.gain.setValueAtTime(0, at);
    amp.gain.linearRampToValueAtTime(gain, at + 0.015);
    amp.gain.setValueAtTime(gain, at + Math.max(0.02, len - 0.04));
    amp.gain.linearRampToValueAtTime(0, at + len);
    const harm = ctx.createOscillator();
    harm.type = "sine";
    harm.frequency.value = 5900;
    const harmGain = ctx.createGain();
    harmGain.gain.value = 0.12;
    harm.connect(harmGain).connect(amp);
    osc.connect(amp).connect(this.out());
    for (const o of [osc, trill, harm]) {
      o.start(at);
      o.stop(at + len + 0.05);
    }
  }

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

  private cheer(len: number) {
    const ctx = this.ctx!;
    const t = ctx.currentTime;
    const n = ctx.createBufferSource();
    n.buffer = this.noiseBuf;
    n.loop = true;
    const bp = ctx.createBiquadFilter();
    bp.type = "bandpass";
    bp.frequency.value = 900;
    bp.Q.value = 0.6;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(0.7, t + 0.4);
    g.gain.linearRampToValueAtTime(0.5, t + len * 0.5);
    g.gain.linearRampToValueAtTime(0.0001, t + len);
    n.connect(bp).connect(g).connect(this.out());
    n.start(t);
    n.stop(t + len + 0.1);
  }

  private boo() {
    const ctx = this.ctx!;
    const t = ctx.currentTime;
    const o = ctx.createOscillator();
    o.type = "sawtooth";
    o.frequency.value = 140;
    const lp = ctx.createBiquadFilter();
    lp.type = "lowpass";
    lp.frequency.value = 400;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(0.12, t + 0.3);
    g.gain.linearRampToValueAtTime(0.0001, t + 1.6);
    o.connect(lp).connect(g).connect(this.out());
    o.start(t);
    o.stop(t + 1.7);
  }
}
