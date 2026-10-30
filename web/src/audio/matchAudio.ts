// Match sounds for the web viewers: referee whistles, ball kicks, headers and saves and the steady murmur of the crowd are
// made here; the crowd's reactions (anger, celebration, booing, applause), the net and the woodwork are recordings
// (loadSamples) - without them those moments are silent.
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
/** How loud the recordings play against the ball, the whistles and the steady crowd (they were far too loud at 1). */
const SAMPLE_LEVEL = 0.42;

export const SAMPLE_NAMES = ["fan_angry", "after_goal", "booing", "clapping", "net", "crossbar", "goalpost"] as const;
export type SampleName = (typeof SAMPLE_NAMES)[number];

export class MatchSound {
  private ctx: AudioContext | null = null;
  private samples: Partial<Record<SampleName, AudioBuffer>> = {};
  private sampleBase: string | null = null;
  private sampleBusy: Partial<Record<SampleName, number>> = {};
  /** A home shot that has not been answered by the crowd yet (applause if it ends in a corner). */
  private homeShotAt = -100;
  private homeShotAnswered = true;
  private master: GainNode | null = null;
  private crowd: GainNode | null = null;
  private crowdLevel = 0.14;
  private boost = 0;
  private hush = 0;
  private noiseBuf: AudioBuffer | null = null;
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
  private sample(
    name: SampleName,
    gain: number,
    opts: { wide?: boolean; pan?: number; echo?: number; maxSeconds?: number; far?: boolean; offset?: number; fadeIn?: number; fadeOut?: number } = {},
  ): boolean {
    const buf = this.samples[name];
    const ctx = this.ctx;
    if (!buf || !ctx) return false;
    const now = ctx.currentTime;
    // the same reaction does not pile up on itself (a foul and its card a second later): one at a time
    const busyUntil = this.sampleBusy[name] ?? 0;
    if (opts.fadeIn !== undefined && opts.fadeIn > 0.05 && now < busyUntil) return true;
    gain *= SAMPLE_LEVEL;
    const offset = Math.min(opts.offset ?? 0, Math.max(0, buf.duration - 0.5));
    const len = Math.min(buf.duration - offset, opts.maxSeconds ?? buf.duration);
    const fadeIn = Math.min(opts.fadeIn ?? 0.4, len / 3);
    const fadeOut = Math.min(opts.fadeOut ?? 1.5, len / 2);
    this.sampleBusy[name] = now + len * 0.6;
    const pans = opts.wide ? [-0.85, 0, 0.85] : [opts.pan ?? 0];
    const each = opts.wide ? gain * 0.62 : gain;
    pans.forEach((p, k) => {
      const src = ctx.createBufferSource();
      src.buffer = buf;
      const g = ctx.createGain();
      const at = now + (opts.wide ? k * 0.035 : 0); // (a few ms apart: the sound arrives from all round)
      g.gain.setValueAtTime(0.0001, at);
      g.gain.linearRampToValueAtTime(each, at + Math.max(0.005, fadeIn));
      g.gain.setValueAtTime(each, at + Math.max(fadeIn, len - fadeOut));
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
      src.start(at, offset, len);
    });
    return true;
  }

  /** The home fans furious with the referee (or a goal against): from all round the ground. */
  private angry(gain = 0.9) {
    this.sample("fan_angry", gain, { wide: true, maxSeconds: 7, fadeIn: 0.5, fadeOut: 2.5 });
  }

  /** The home crowd booing (the recording is cut before its applause starts, at 14.5 s; its boos start 0.6 s in). */
  private booing(gain = 0.85) {
    this.sample("booing", gain, { wide: true, offset: 0.6, maxSeconds: 8, fadeIn: 0.6, fadeOut: 2.5 }); // (the boos start 0.6 s in)
  }

  /** Applause for a home shot that did not go in. */
  private clapping(gain = 0.6) {
    this.homeShotAnswered = true;
    this.sample("clapping", gain, { wide: true, fadeIn: 0.3, fadeOut: 1.5 });
  }

  disable(): void {
    void this.ctx?.suspend();
  }

  /**
   * Plays the sound that belongs to a match event (no-op while disabled). The ball, the whistles and the steady crowd are
   * made here; every reaction of the crowd (anger, celebration, booing, applause) and the net and the woodwork are the
   * recordings - nothing is played for them until those have loaded.
   */
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
        if (!home) this.addBoost(0.05);
        else if (this.ctx) {
          this.homeShotAt = this.ctx.currentTime;
          this.homeShotAnswered = false;
        }
        break;
      case "Header":
        this.thud(150, 0.07, 0.4);
        break;
      case "Save":
        this.thud(190, 0.08, 0.45);
        // e.team is the keeper's side: a home shot saved
        if (away) this.clapping(0.5);
        break;
      case "GoalkeeperClaim":
        this.thud(190, 0.08, 0.4);
        break;
      case "ShotOffTarget":
        if (home) this.clapping(0.45);
        break;
      case "ShotBlocked":
        // e.team is the blocker's side: a home shot blocked by the visitors
        if (away) this.clapping(0.4);
        break;
      case "HitWoodwork": {
        // how hard it hit decides how loud it rings and how long the stadium echoes it
        const hard = Math.max(0, Math.min(1, ((e.value ?? 12) - 6) / 24));
        this.sample(e.detail === 2 ? "crossbar" : "goalpost", 1.4 + 0.9 * hard, { echo: hard, fadeIn: 0.005, fadeOut: 0.6 });
        if (home) this.clapping(0.5);
        break;
      }
      case "Goal":
        // the net's own sound comes with the NetHit event right after (it knows how hard the ball hit it)
        if (home) {
          // the whole ground goes up (the recording's roar starts 2.1 s in: played from there, it comes with the goal)
          this.sample("after_goal", 1, { wide: true, offset: 2.05, maxSeconds: 8, fadeIn: 0.15, fadeOut: 3 });
          this.boost = 0.3;
        } else {
          // The away section celebrates in one corner, far and quieter; the home fans are furious all round.
          this.sample("after_goal", 0.3, { pan: 0.8, far: true, offset: 2.05, maxSeconds: 6, fadeIn: 0.3, fadeOut: 2.5 });
          this.angry(0.7);
          this.hush = 1;
        }
        break;
      case "NetHit": {
        const hard = Math.max(0, Math.min(1, ((e.value ?? 10) - 4) / 22));
        this.sample("net", 1.1 + 0.9 * hard, { echo: hard * 0.7, fadeIn: 0.005, fadeOut: 0.5 });
        break;
      }
      case "Corner":
        // a home shot turned round the post or deflected behind for a corner: applause for the effort
        if (home && this.ctx && !this.homeShotAnswered && this.ctx.currentTime - this.homeShotAt < 5) this.clapping(0.5);
        break;
      case "Foul":
        // e.team is the offender's side: a free kick against the home team angers the home end
        if (home) this.angry(0.75);
        break;
      case "YellowCard":
        if (home) this.angry(0.8);
        break;
      case "RedCard":
        if (home) {
          this.angry(0.9);
          this.booing(0.8);
        }
        break;
      case "PenaltyAwarded":
        // e.team is the side given the penalty: against the home team the ground erupts, and the away taker is booed
        if (away) {
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

  /**
   * The ball hitting the back of the net. Netting is slack nylon cord: there is no ring and no hiss. What you hear is a soft low
   * "whump" as the ball stretches the net, a short muffled swish of cords sliding against each other (broad, about 150 Hz to
   * 2 kHz, with a quick flutter) and a few tiny cord ticks as it settles. Louder for a harder strike.
   */
}
