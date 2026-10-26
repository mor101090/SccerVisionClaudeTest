using SoccerVision.Simulation;
using UnityEngine;

namespace SoccerVision.Runtime
{
    /// <summary>
    /// Placeholder audio synthesised at start-up (no asset files needed) following the design document's audio
    /// conventions: single short whistle for restarts, quick blasts for fouls, long whistle for serious
    /// stoppages, two whistles at half time, three (short-short-long) at full time, ball thuds, net rustle and
    /// a crowd bed that swells on goals and boos fouls. Replace the clips with recorded assets later.
    /// </summary>
    public sealed class MatchAudio : MonoBehaviour
    {
        private const int Rate = 22050;
        private AudioSource _fx;
        private AudioSource _crowd;
        private AudioClip _whistleShort, _whistleFoul, _whistleLong, _whistleHalf, _whistleFull, _kick, _net, _cheer, _boo, _crowdLoop;
        private float _crowdBoost;
        private System.Random _random = new System.Random(7);

        public float masterVolume = 0.8f;

        public void Init()
        {
            _fx = gameObject.AddComponent<AudioSource>();
            _fx.playOnAwake = false;
            _crowd = gameObject.AddComponent<AudioSource>();
            _crowd.playOnAwake = false;
            _crowd.loop = true;

            _whistleShort = Whistle("whistle_short", new[] { 0.28f }, 0.08f);
            _whistleFoul = Whistle("whistle_foul", new[] { 0.12f, 0.12f, 0.2f }, 0.06f);
            _whistleLong = Whistle("whistle_long", new[] { 0.9f }, 0.1f);
            _whistleHalf = Whistle("whistle_half", new[] { 0.45f, 0.45f }, 0.18f);
            _whistleFull = Whistle("whistle_full", new[] { 0.3f, 0.3f, 1.1f }, 0.16f);
            _kick = Thud("kick", 0.09f, 90f);
            _net = Noise("net", 0.5f, 0.35f, true);
            _cheer = Noise("cheer", 4.5f, 0.9f, false);
            _boo = Boo("boo", 2.2f);
            _crowdLoop = CrowdBed("crowd", 6f);

            _crowd.clip = _crowdLoop;
            _crowd.volume = 0.18f * masterVolume;
            _crowd.Play();
        }

        private void Update()
        {
            if (_crowd == null) return;
            _crowdBoost = Mathf.MoveTowards(_crowdBoost, 0f, Time.deltaTime * 0.12f);
            _crowd.volume = (0.16f + _crowdBoost) * masterVolume;
        }

        public void OnEvent(MatchEvent e)
        {
            if (_fx == null) return;
            switch (e.Type)
            {
                case MatchEventType.Whistle:
                    switch (e.Whistle)
                    {
                        case WhistleKind.Short: _fx.PlayOneShot(_whistleShort, 0.35f * masterVolume); break;
                        case WhistleKind.Foul: _fx.PlayOneShot(_whistleFoul, 0.55f * masterVolume); break;
                        case WhistleKind.Long: _fx.PlayOneShot(_whistleLong, 0.55f * masterVolume); break;
                        case WhistleKind.HalfTime: _fx.PlayOneShot(_whistleHalf, 0.6f * masterVolume); break;
                        case WhistleKind.FullTime: _fx.PlayOneShot(_whistleFull, 0.65f * masterVolume); break;
                    }
                    break;
                case MatchEventType.Pass:
                case MatchEventType.LongBall:
                case MatchEventType.Clearance:
                case MatchEventType.Cross:
                    _fx.PlayOneShot(_kick, 0.25f * masterVolume);
                    break;
                case MatchEventType.Shot:
                    _fx.PlayOneShot(_kick, 0.6f * masterVolume);
                    _crowdBoost = Mathf.Max(_crowdBoost, 0.1f);
                    break;
                case MatchEventType.Goal:
                    _fx.PlayOneShot(_net, 0.5f * masterVolume);
                    _fx.PlayOneShot(_cheer, 0.75f * masterVolume);
                    _crowdBoost = 0.35f;
                    break;
                case MatchEventType.NetHit:
                    _fx.PlayOneShot(_net, 0.25f * masterVolume);
                    break;
                case MatchEventType.HitWoodwork:
                    _fx.PlayOneShot(_kick, 0.7f * masterVolume);
                    _crowdBoost = Mathf.Max(_crowdBoost, 0.2f);
                    break;
                case MatchEventType.Foul:
                case MatchEventType.RedCard:
                    _fx.PlayOneShot(_boo, 0.3f * masterVolume);
                    break;
                case MatchEventType.Save:
                    _crowdBoost = Mathf.Max(_crowdBoost, 0.15f);
                    break;
            }
        }

        // ---- Synthesis -------------------------------------------------------------------------------------

        /// <summary>Pea whistle: ~3 kHz tone with the pea's fast trill (frequency + amplitude modulation).</summary>
        private AudioClip Whistle(string name, float[] blasts, float gap)
        {
            float total = 0f;
            foreach (var b in blasts) total += b + gap;
            int n = Mathf.CeilToInt(total * Rate);
            var data = new float[n];
            int i = 0;
            foreach (var b in blasts)
            {
                int len = Mathf.CeilToInt(b * Rate);
                for (int k = 0; k < len && i < n; k++, i++)
                {
                    float t = k / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((b - t) / 0.04f);
                    float trill = Mathf.Sin(2f * Mathf.PI * 42f * t);
                    float f = 2950f + 180f * trill;
                    float phase = 2f * Mathf.PI * f * t;
                    float s = Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2f) * 0.12f;
                    data[i] = s * env * (0.75f + 0.25f * trill) * 0.6f;
                }
                i += Mathf.CeilToInt(gap * Rate);
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
                float noise = (float)(_random.NextDouble() * 2.0 - 1.0);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t * (1f - t * 3f)) * 0.8f + noise * 0.25f) * env;
            }
            return Make(name, data);
        }

        private AudioClip Noise(string name, float length, float amp, bool bright)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / (bright ? 0.01f : 0.4f)) * Mathf.Clamp01((length - t) / (length * 0.6f));
                float w = (float)(_random.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * (bright ? 0.6f : 0.08f);
                float swell = bright ? 1f : 0.7f + 0.3f * Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 1.7f);
                data[i] = lp * env * amp * swell * (bright ? 1f : 2.2f);
            }
            return Make(name, data);
        }

        private AudioClip Boo(string name, float length)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((length - t) / 0.8f);
                float w = (float)(_random.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.03f;
                float voice = Mathf.Sin(2f * Mathf.PI * (140f + 10f * Mathf.Sin(t * 3f)) * t);
                data[i] = (lp * 3f + voice * 0.15f) * env * 0.6f;
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
                float w = (float)(_random.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.12f;
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
