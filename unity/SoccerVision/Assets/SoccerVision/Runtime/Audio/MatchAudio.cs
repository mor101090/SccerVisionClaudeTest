using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Match audio synthesised at start-up (no asset files needed). Mirrors web/src/audio/matchAudio.ts.
    /// <list type="bullet">
    /// <item>Referee: a pea whistle (Acme Thunderer style, ~2.8 kHz with the pea's irregular warble). When it is
    /// blown follows the IFAB practical guidelines (none for clear throw-ins, goal kicks, corners or goals);
    /// the blast follows the offence: light tweet for offside, sharp blast for a foul, long strong blast for a
    /// penalty or sending-off, two blasts at half time and three (short-short-long) at full time.</item>
    /// <item>Crowd: the home end. It roars for home goals and falls silent for away ones (the visiting section
    /// cheers in the distance), "ooh"s home shots, groans at near misses, boos the visitors' fouls and cards
    /// against the home side, and applauds saves, blocks and corners.</item>
    /// </list>
    /// Replace the clips with recorded assets later.
    /// </summary>
    public sealed class MatchAudio : MonoBehaviour
    {
        private const int Rate = 22050;
        private AudioSource _fx;
        private AudioSource _crowd;
        private AudioClip _whistleShort, _whistleLight, _whistleFoul, _whistleLong, _whistleHalf, _whistleFull;
        private AudioClip _kick, _kickHard, _header, _post, _crossbar, _glove, _net, _crowdLoop;
        private AudioClip _roar, _roarShort, _awayEnd, _ooh, _groan, _murmur, _boo, _booLong, _applause, _applauseLong;
        private float _crowdBoost;
        private float _hush;
        private System.Random _random = new System.Random(7);

        public float masterVolume = 0.8f;

        public void Init()
        {
            _fx = gameObject.AddComponent<AudioSource>();
            _fx.playOnAwake = false;
            _crowd = gameObject.AddComponent<AudioSource>();
            _crowd.playOnAwake = false;
            _crowd.loop = true;

            _whistleShort = Whistle("whistle_short", new[] { 0.34f }, new[] { 0f }, 1f);
            _whistleLight = Whistle("whistle_light", new[] { 0.17f }, new[] { 0f }, 0.97f);
            _whistleFoul = Whistle("whistle_foul", new[] { 0.3f }, new[] { 0f }, 1.03f);
            _whistleLong = Whistle("whistle_long", new[] { 1.1f }, new[] { 0f }, 1.05f);
            _whistleHalf = Whistle("whistle_half", new[] { 0.4f, 1.0f }, new[] { 0.28f, 0f }, 1.03f);
            _whistleFull = Whistle("whistle_full", new[] { 0.3f, 0.3f, 1.4f }, new[] { 0.22f, 0.22f, 0f }, 1.04f);
            _kick = Thud("kick", 0.09f, 90f);
            _kickHard = Thud("kick_hard", 0.14f, 70f);
            _header = Thud("header", 0.07f, 140f);
            _glove = Thud("glove", 0.08f, 180f);
            _post = Woodwork("post", false);
            _crossbar = Woodwork("crossbar", true);
            _net = Net("net", 0.7f);
            _roar = Voices("roar", Ah, 270f, 340f, 5.5f, 3200f);
            _roarShort = Voices("roar_short", Ah, 260f, 320f, 3f, 3200f);
            _awayEnd = Voices("away_end", Ah, 280f, 330f, 4f, 900f);
            _ooh = Voices("ooh", Oo, 210f, 290f, 1.4f, 3200f);
            _groan = Voices("groan", Aw, 250f, 150f, 1.8f, 3200f);
            _murmur = Voices("murmur", Aw, 200f, 170f, 0.9f, 2000f);
            _boo = Voices("boo", Oo, 135f, 120f, 2f, 2400f);
            _booLong = Voices("boo_long", Oo, 135f, 118f, 3.5f, 2400f);
            _applause = Applause("applause", 2f);
            _applauseLong = Applause("applause_long", 6f);
            _crowdLoop = CrowdBed("crowd", 6f);

            _crowd.clip = _crowdLoop;
            _crowd.volume = 0.18f * masterVolume;
            _crowd.Play();
        }

        private void Update()
        {
            if (_crowd == null) return;
            _crowdBoost = Mathf.MoveTowards(_crowdBoost, 0f, Time.deltaTime * 0.12f);
            _hush = Mathf.MoveTowards(_hush, 0f, Time.deltaTime * 0.12f);
            _crowd.volume = (0.16f + _crowdBoost) * (1f - 0.65f * _hush) * masterVolume;
        }

        private void Fx(AudioClip clip, float volume) => _fx.PlayOneShot(clip, volume * masterVolume);

        /// <summary>Plays the sound of an event. Event teams: 0 home, 1 away (offender for fouls/cards, keeper for saves).</summary>
        public void OnEvent(MatchEvent e, int homeScore, int awayScore)
        {
            if (_fx == null) return;
            bool home = e.Team == 0;
            bool away = e.Team == 1;
            switch (e.Type)
            {
                case MatchEventType.Whistle:
                    switch (e.Whistle)
                    {
                        case WhistleKind.Short: Fx(_whistleShort, 0.5f); break;
                        case WhistleKind.Light: Fx(_whistleLight, 0.36f); break;
                        case WhistleKind.Foul: Fx(_whistleFoul, 0.72f); break;
                        case WhistleKind.Long: Fx(_whistleLong, 0.9f); break;
                        case WhistleKind.HalfTime: Fx(_whistleHalf, 0.8f); break;
                        case WhistleKind.FullTime: Fx(_whistleFull, 0.85f); break;
                    }
                    break;
                case MatchEventType.Pass:
                case MatchEventType.LongBall:
                case MatchEventType.Clearance:
                case MatchEventType.Cross:
                    Fx(_kick, 0.25f);
                    break;
                case MatchEventType.Header:
                    Fx(_header, 0.45f);
                    break;
                case MatchEventType.Shot:
                    // side-footed finishes and chips are struck softly; volleys and free kicks are hit hardest
                    Fx(_kickHard, e.Detail == 1 || e.Detail == 3 ? 0.8f : e.Detail == 6 || e.Detail == 7 || e.Detail == 9 ? 0.4f : 0.65f);
                    if (home) Fx(_ooh, 0.45f);
                    else _crowdBoost = Mathf.Max(_crowdBoost, 0.05f);
                    break;
                case MatchEventType.Save:
                    Fx(_glove, 0.5f);
                    if (home) Fx(_applause, 0.5f);
                    else Fx(_groan, 0.5f);
                    break;
                case MatchEventType.GoalkeeperClaim:
                    Fx(_glove, 0.4f);
                    break;
                case MatchEventType.ShotOffTarget:
                    if (home) Fx(_groan, 0.4f);
                    break;
                case MatchEventType.ShotBlocked:
                    if (home) Fx(_applause, 0.3f);
                    break;
                case MatchEventType.HitWoodwork:
                    Fx(e.Detail == 2 ? _crossbar : _post, Mathf.Clamp(0.3f + e.Value / 30f, 0.3f, 1f));
                    Fx(_ooh, home ? 0.7f : 0.45f);
                    if (home) Fx(_groan, 0.4f);
                    break;
                case MatchEventType.Goal:
                    // the net's own sound comes with the NetHit event right after (it knows how hard the ball hit it)
                    if (home)
                    {
                        Fx(_roar, 1f);
                        Fx(_applauseLong, 0.7f);
                        _crowdBoost = 0.35f;
                    }
                    else
                    {
                        Fx(_groan, 0.45f);
                        Fx(_awayEnd, 0.25f);
                        _hush = 1f;
                    }
                    break;
                case MatchEventType.NetHit:
                    Fx(_net, Mathf.Clamp(0.25f + e.Value / 28f, 0.25f, 1f));
                    break;
                case MatchEventType.Foul:
                    if (away) Fx(_boo, 0.6f);
                    else Fx(_murmur, 0.15f);
                    break;
                case MatchEventType.YellowCard:
                    if (away) Fx(_applause, 0.35f);
                    else Fx(_boo, 0.8f);
                    break;
                case MatchEventType.RedCard:
                    if (away) { Fx(_roarShort, 0.6f); Fx(_applause, 0.6f); }
                    else Fx(_booLong, 1f);
                    break;
                case MatchEventType.PenaltyAwarded:
                    if (home) { Fx(_roarShort, 0.85f); Fx(_applause, 0.5f); }
                    else Fx(_booLong, 1f);
                    break;
                case MatchEventType.PenaltyMissed:
                    if (home) Fx(_groan, 0.9f);
                    else { Fx(_roarShort, 0.8f); Fx(_applause, 0.6f); }
                    break;
                case MatchEventType.Offside:
                    if (home) Fx(_murmur, 0.25f);
                    else Fx(_applause, 0.25f);
                    break;
                case MatchEventType.Corner:
                    if (home) Fx(_applause, 0.3f);
                    break;
                case MatchEventType.FreeKick:
                    if (home && e.Detail == 1) Fx(_ooh, 0.2f);
                    break;
                case MatchEventType.HalfTime:
                    Fx(_applause, 0.4f);
                    break;
                case MatchEventType.FullTime:
                    if (homeScore >= awayScore) Fx(_applauseLong, homeScore > awayScore ? 0.9f : 0.5f);
                    if (homeScore > awayScore) Fx(_roarShort, 0.6f);
                    if (homeScore < awayScore) Fx(_booLong, 0.8f);
                    break;
            }
        }

        // ---- Synthesis -------------------------------------------------------------------------------------

        private float Rand() => (float)(_random.NextDouble() * 2.0 - 1.0);

        /// <summary>
        /// Pea whistle: a ~2.8 kHz tone with strong 2nd/3rd harmonics; the pea rattling round the chamber makes
        /// irregular frequency (~6%) and amplitude flutter at ~30-45 Hz; the pitch slides up as the breath builds
        /// and drops as it fades; breath noise surrounds the tone. The last blast of a sequence is the strongest.
        /// </summary>
        private AudioClip Whistle(string name, float[] blasts, float[] gaps, float pressure)
        {
            float total = 0.05f;
            for (int b = 0; b < blasts.Length; b++) total += blasts[b] + gaps[b];
            int n = Mathf.CeilToInt(total * Rate);
            var data = new float[n];
            int i = 0;
            for (int b = 0; b < blasts.Length; b++)
            {
                float len = blasts[b];
                bool last = b == blasts.Length - 1 && blasts.Length > 1;
                float f0 = 2800f * pressure * (last ? 1.02f : 1f);
                float gain = last ? 1f : 0.92f;
                int count = Mathf.CeilToInt(len * Rate);
                float phase = 0f, peaPhase = 0f, wander = 0f, peaLp = 0f, breathLp = 0f, breathBp = 0f;
                for (int k = 0; k < count && i < n; k++, i++)
                {
                    float t = k / (float)Rate;
                    float rate = 36f + 7f * Mathf.Sin(t * 2.3f) + 4f * Mathf.Sin(t * 5.9f);
                    peaPhase += 2f * Mathf.PI * rate / Rate;
                    wander += (Rand() - wander) * 0.02f;
                    float s = Mathf.Sin(peaPhase);
                    float peaRaw = 0.75f * Mathf.Sign(s) * Mathf.Sqrt(Mathf.Abs(s)) + wander * 2.2f;
                    peaLp += (peaRaw - peaLp) * 0.06f; // ~220 Hz smoothing
                    float slide = t < 0.035f ? Mathf.Lerp(0.82f, 1f, t / 0.035f) : (t > len - 0.05f ? Mathf.Lerp(1f, 0.88f, (t - (len - 0.05f)) / 0.05f) : 1f);
                    float f = f0 * slide + f0 * 0.055f * peaLp;
                    phase += 2f * Mathf.PI * f / Rate;
                    float tone = Mathf.Sin(phase) + 0.42f * Mathf.Sin(2f * phase) + 0.2f * Mathf.Sin(3f * phase); // 4th harmonic would alias at 22 kHz
                    // breath: crude band-pass of noise
                    breathLp += (Rand() - breathLp) * 0.55f;
                    breathBp += (breathLp - breathBp) * 0.5f;
                    float breath = (breathLp - breathBp) * 0.35f;
                    float flutter = 0.72f + 0.28f * Mathf.Clamp(peaLp, -1f, 1f);
                    float env = t < 0.012f ? t / 0.012f * 1.1f : (t < 0.05f ? Mathf.Lerp(1.1f, 1f, (t - 0.012f) / 0.038f) : 1f);
                    env *= Mathf.Clamp01((len - t) / 0.045f);
                    data[i] = (tone * 0.38f + breath) * flutter * env * gain;
                }
                i += Mathf.CeilToInt(gaps[b] * Rate);
            }
            return Make(name, data);
        }

        private static readonly float[] Oo = { 330f, 820f };
        private static readonly float[] Aw = { 570f, 900f };
        private static readonly float[] Ah = { 760f, 1220f };

        /// <summary>
        /// A crowd shouting a vowel: 22 detuned voices (sawtooths with their own vibrato and onsets) through two
        /// vowel formant band-passes, plus breath; pitch glides from f0 to f1 (rising "ooh", falling "aww", roar).
        /// </summary>
        private AudioClip Voices(string name, float[] formants, float f0, float f1, float len, float lowpass)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var src = new float[n];
            const int count = 22;
            for (int v = 0; v < count; v++)
            {
                float spread = 0.72f + (float)_random.NextDouble() * 0.62f;
                float start = (float)_random.NextDouble() * Mathf.Min(0.3f, len * 0.2f);
                float vibRate = 4f + (float)_random.NextDouble() * 3f;
                float end = 0.95f + (float)_random.NextDouble() * 0.1f;
                float phase = (float)_random.NextDouble();
                for (int i = (int)(start * Rate); i < n; i++)
                {
                    float t = i / (float)Rate;
                    float f = Mathf.Lerp(f0 * spread, f1 * spread * end, t / len);
                    f *= 1f + 0.02f * Mathf.Sin(2f * Mathf.PI * vibRate * t);
                    phase += f / Rate;
                    phase -= Mathf.Floor(phase);
                    src[i] += (phase * 2f - 1f) * 0.2f;
                }
            }
            for (int i = 0; i < n; i++) src[i] += Rand() * 0.5f;
            var a = BandPass(src, formants[0], 3f);
            var b = BandPass(src, formants[1], 4f);
            var data = new float[n];
            float attack = Mathf.Min(0.35f, len * 0.25f);
            float lpK = 1f - Mathf.Exp(-2f * Mathf.PI * lowpass / Rate);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = t < attack ? Mathf.Lerp(0f, 0.5f, t / attack) : (t < len * 0.6f ? Mathf.Lerp(0.5f, 0.42f, (t - attack) / (len * 0.6f - attack)) : Mathf.Lerp(0.42f, 0f, (t - len * 0.6f) / (len * 0.4f)));
                lp += (a[i] + 0.55f * b[i] - lp) * lpK;
                data[i] = lp * env;
            }
            return Make(name, data);
        }

        /// <summary>RBJ biquad band-pass (constant 0 dB peak gain).</summary>
        private static float[] BandPass(float[] x, float freq, float q)
        {
            float w0 = 2f * Mathf.PI * freq / Rate;
            float alpha = Mathf.Sin(w0) / (2f * q);
            float a0 = 1f + alpha;
            float b0 = alpha / a0, b2 = -alpha / a0, a1 = -2f * Mathf.Cos(w0) / a0, a2 = (1f - alpha) / a0;
            var y = new float[x.Length];
            float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float o = b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x[i]; y2 = y1; y1 = o;
                y[i] = o;
            }
            return y;
        }

        /// <summary>Many hands clapping: random short bursts of filtered noise, band-passed around 1.9 kHz.</summary>
        private AudioClip Applause(string name, float len)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var raw = new float[n];
            int claps = Mathf.FloorToInt(len * 420f);
            for (int c = 0; c < claps; c++)
            {
                int start = _random.Next(0, n - Rate / 50);
                int clapLen = (int)(Rate * (0.004f + (float)_random.NextDouble() * 0.008f));
                float amp = 0.25f + (float)_random.NextDouble() * 0.75f;
                float k = 0.3f + (float)_random.NextDouble() * 0.5f;
                float lp = 0f;
                for (int i = 0; i < clapLen; i++)
                {
                    lp += (Rand() - lp) * k;
                    raw[start + i] += lp * amp * Mathf.Exp(-i / (clapLen * 0.35f));
                }
            }
            var data = BandPass(raw, 1900f, 0.7f);
            float peak = 1e-6f;
            foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.25f) * (t < len * 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - len * 0.6f) / (len * 0.4f)));
                data[i] = data[i] / peak * env * 0.55f;
            }
            return Make(name, data);
        }

        private AudioClip Thud(string name, float length, float freq)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 45f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t * (1f - t * 3f)) * 0.8f + Rand() * 0.25f) * env;
            }
            return Make(name, data);
        }

        /// <summary>
        /// The ball hitting a goal post or the crossbar (a hollow steel/aluminium tube). Layers: the contact click and the ball's
        /// dull thud; the frame's bending modes (free-free beam ratios 1 : 2.76 : 5.40 : 8.93) for the deep "tonk" (the crossbar is
        /// lighter and rings longer than the ground-socketed post); the tube's shell "ping" at 2-3 kHz (squeeze-and-expand cylinder
        /// modes, like an aluminium bat's 2200 and 2800 Hz); and the rattle of net fixings right after.
        /// </summary>
        private AudioClip Woodwork(string name, bool crossbar)
        {
            int n = Mathf.CeilToInt(1.1f * Rate);
            var data = new float[n];
            float f1 = crossbar ? 74f : 58f, tail = crossbar ? 0.7f : 0.38f;
            float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
            // the lowest mode is felt more than heard on a normal speaker: the audible "tonk" comes from modes 2-4 (about 170-550 Hz)
            float[] weights = { 0.2f, 0.5f, 0.45f, 0.32f };
            float[] ping = crossbar ? new[] { 2200f, 2800f, 3650f } : new[] { 1900f, 2500f, 3300f };
            float pingDur = crossbar ? 0.34f : 0.2f;
            var rattleAt = new float[4];
            for (int k = 0; k < 4; k++) rattleAt[k] = 0.03f + k * 0.045f + (float)_random.NextDouble() * 0.03f;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float v = 0f;
                // contact: click (high-passed noise) + thud
                lp += (Rand() - lp) * 0.5f;
                float noise = Rand() - lp;
                v += noise * Mathf.Exp(-t * 600f) * 0.8f;
                v += Mathf.Sin(2f * Mathf.PI * 130f * t * (1f - t * 2f)) * Mathf.Exp(-t * 40f) * 0.45f;
                // frame modes
                for (int k = 0; k < 4; k++)
                {
                    float dur = tail / (1f + k * 0.5f);
                    v += Mathf.Sin(2f * Mathf.PI * f1 * ratios[k] * t) * Mathf.Exp(-t * 6.9f / dur) * weights[k];
                }
                // shell ping
                for (int k = 0; k < ping.Length; k++)
                {
                    float dur = pingDur / (1f + k * 0.3f);
                    v += Mathf.Sin(2f * Mathf.PI * ping[k] * t) * Mathf.Exp(-t * 6.9f / dur) * 0.42f / (1f + k * 0.4f);
                }
                // rattle of the fixings
                for (int k = 0; k < 4; k++)
                {
                    float dt = t - rattleAt[k];
                    if (dt > 0f && dt < 0.03f) v += noise * Mathf.Exp(-dt * 250f) * 0.18f * (1f - k * 0.2f);
                }
                data[i] = v * Mathf.Clamp01(t / 0.002f) * Mathf.Clamp01((1.1f - t) / 0.1f);
            }
            return Make(name, data);
        }

        /// <summary>
        /// The ball hitting the back of the net. Netting is slack nylon cord: no ring, no hiss. A soft low "whump" as the ball
        /// stretches the net, a short muffled swish of cords sliding on each other (about 150 Hz to 2 kHz, with a quick flutter)
        /// and a few tiny cord ticks as it settles.
        /// </summary>
        private AudioClip Net(string name, float length)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            float lo = 0f, hi = 0f;
            float flutterHz = 26f + (float)_random.NextDouble() * 8f;
            var tickAt = new float[4];
            for (int k = 0; k < 4; k++) tickAt[k] = 0.05f + k * 0.07f + (float)_random.NextDouble() * 0.05f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = Rand();
                lo += (w - lo) * 0.45f; // ~2 kHz low-pass
                hi += (lo - hi) * 0.04f; // ~150 Hz: subtract for a high-pass
                float band = lo - hi;
                float env = Mathf.Clamp01(t / 0.018f) * Mathf.Exp(-t * 6.9f / 0.5f);
                float swish = band * env * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * flutterHz * t)) * 3.6f;
                float whump = Mathf.Sin(2f * Mathf.PI * 85f * t * (1f - t * 1.5f)) * Mathf.Exp(-t * 28f) * 0.55f;
                float ticks = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float dt = t - tickAt[k];
                    if (dt > 0f && dt < 0.03f) ticks += (w - lo) * Mathf.Exp(-dt * 200f) * 0.4f;
                }
                data[i] = swish + whump + ticks;
            }
            return Make(name, data);
        }

        /// <summary>Seamless murmuring crowd loop (filtered noise with slow swells).</summary>
        private AudioClip CrowdBed(string name, float length)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (Rand() - lp) * 0.12f;
                lp2 += (lp - lp2) * 0.2f;
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / length) * Mathf.Sin(2f * Mathf.PI * 3f * t / length);
                data[i] = lp2 * swell * 2.5f;
            }
            // Cross-fade the tail into the head and drop the tail so the loop has no click.
            int fade = Rate / 5;
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = data[i] * a + data[n - fade + i] * (1f - a);
            }
            var loop = new float[n - fade];
            System.Array.Copy(data, loop, loop.Length);
            return Make(name, loop);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
