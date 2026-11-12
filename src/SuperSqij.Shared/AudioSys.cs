using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace SuperSqij;

/// <summary>
/// All audio is synthesised at startup: a moody 8-bar cave loop plus a set of 8-bit effects.
/// Kept deliberately dependency-free so every platform sounds identical.
/// </summary>
public enum MusicTrack { Game, GameOver, Scores }

public sealed class AudioSys : IDisposable
{
    private const int Rate = 22050;
    private readonly Dictionary<string, SoundEffect> _effects = new();
    private readonly Dictionary<MusicTrack, SoundEffect> _tracks = new();
    private readonly Dictionary<MusicTrack, SoundEffectInstance> _musicInstances = new();
    private MusicTrack _track = MusicTrack.Game;
    public bool Enabled = true;
    private bool _ok;

    public void Initialize()
    {
        try
        {
            AddTrack(MusicTrack.Game, MakeMusic(), 0.65f);
            AddTrack(MusicTrack.GameOver, MakeGameOverMusic(), 0.6f);
            AddTrack(MusicTrack.Scores, MakeScoresMusic(), 0.55f);

            _effects["shot"] = MakeEffect(0.13f, Shot);
            _effects["pickup"] = MakeEffect(0.20f, Pickup);
            _effects["fragment"] = MakeEffect(0.55f, Fragment);
            _effects["hurt"] = MakeEffect(0.30f, Hurt);
            _effects["door"] = MakeEffect(0.55f, DoorSfx);
            _effects["flap"] = MakeEffect(0.09f, Flap);
            _effects["feed"] = MakeEffect(0.50f, Feed);
            _effects["victory"] = MakeEffect(2.0f, Victory);
            _effects["death"] = MakeEffect(1.2f, Death);
            _effects["menu"] = MakeEffect(0.08f, MenuBlip);
            _ok = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("SQIJ: audio init failed: " + ex);
            _ok = false;
        }
    }

    private void AddTrack(MusicTrack track, SoundEffect effect, float volume)
    {
        var instance = effect.CreateInstance();
        instance.IsLooped = true;
        instance.Volume = volume;
        _tracks[track] = effect;
        _musicInstances[track] = instance;
    }

    public void StartMusic(MusicTrack track = MusicTrack.Game)
    {
        if (!_ok || !Enabled) { _track = track; return; }
        if (_track != track)
        {
            StopMusic();
            _track = track;
        }
        try
        {
            if (_musicInstances.TryGetValue(track, out var instance) && instance.State != SoundState.Playing)
                instance.Play();
        }
        catch { }
    }

    public void StopMusic()
    {
        foreach (var instance in _musicInstances.Values)
        {
            try { instance.Stop(); } catch { }
        }
    }

    public void Play(string name, float volume = 1f)
    {
        if (!_ok || !Enabled) return;
        if (_effects.TryGetValue(name, out var fx))
        {
            try { fx.Play(Math.Clamp(volume, 0f, 1f), 0f, 0f); } catch { }
        }
    }

    public void Toggle()
    {
        Enabled = !Enabled;
        if (Enabled) StartMusic(_track);
        else StopMusic();
    }

    // ------------------------------------------------------------ synthesis
    private static SoundEffect MakeEffect(float seconds, Func<float, float> fn)
    {
        int n = (int)(seconds * Rate);
        var buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = MathF.Min(1f, t * 60f) * MathF.Max(0f, 1f - t / seconds);
            buf[i] = fn(t) * env;
        }
        return FromSamples(buf, n, Peak);
    }

    // Headroom for the synth: every buffer is normalised so the effects are audible without clipping.
    private const float Peak = 0.85f;

    private static SoundEffect FromSamples(float[] buf, int count, float peak)
    {
        float max = 0f;
        for (int i = 0; i < count; i++) max = MathF.Max(max, MathF.Abs(buf[i]));
        float gain = max > 1e-4f ? peak / max : 1f;
        var data = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            short s = (short)(Math.Clamp(buf[i] * gain, -1f, 1f) * 32767f);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new SoundEffect(data, Rate, AudioChannels.Mono);
    }

    private static float Square(float phase, float duty = 0.5f) => (phase % 1f) < duty ? 1f : -1f;
    private static float Tri(float phase) => 1f - 4f * MathF.Abs((phase % 1f) - 0.5f);
    private static float Noise(ref uint state)
    {
        state ^= state << 13; state ^= state >> 17; state ^= state << 5;
        return (state & 0xffff) / 32768f - 1f;
    }

    private static float Note(float hz, float t) => MathF.Sin(t * hz * MathF.Tau);

    private static float Shot(float t)
    {
        float n = t * 900f;
        return (Square(n + t * 300f, 0.25f) * 0.5f + Noise(ref _noise) * 0.5f) * MathF.Exp(-t * 22f);
    }

    private static float Pickup(float t)
    {
        float[] notes = { 523.25f, 659.25f, 783.99f };
        int idx = Math.Min(2, (int)(t / 0.055f));
        return Square(t * notes[idx], 0.5f) * 0.45f;
    }

    private static float Fragment(float t)
    {
        float[] notes = { 392f, 523.25f, 659.25f, 783.99f };
        int idx = Math.Min(3, (int)(t / 0.10f));
        float tone = Square(t * notes[idx], 0.25f) * 0.35f + Tri(t * notes[idx] * 0.5f) * 0.2f;
        return tone * MathF.Exp(-t * 2.2f);
    }

    private static float Hurt(float t)
    {
        float f = 220f - t * 500f;
        return Square(t * MathF.Max(40f, f), 0.4f) * 0.5f + Noise(ref _noise) * 0.2f;
    }

    private static float DoorSfx(float t)
    {
        float sweep = 180f + t * 900f;
        return (Square(t * sweep, 0.3f) * 0.35f + Noise(ref _noise) * 0.15f) * MathF.Exp(-t * 2.5f);
    }

    private static float Flap(float t)
    {
        return Noise(ref _noise) * MathF.Exp(-t * 38f) * 0.35f + Square(t * (140f + t * 300f), 0.2f) * 0.1f;
    }

    private static float Feed(float t)
    {
        float wobble = MathF.Sin(t * 45f) * 30f;
        return (Square(t * (300f + wobble), 0.35f) * 0.3f + Tri(t * 150f) * 0.2f) * MathF.Exp(-t * 3f);
    }

    private static float MenuBlip(float t) => Square(t * 880f, 0.5f) * 0.3f;

    private static float Death(float t)
    {
        float f = 440f * MathF.Exp(-t * 1.6f);
        return (Square(t * f, 0.4f) * 0.35f + Noise(ref _noise) * 0.12f) * MathF.Exp(-t * 1.2f);
    }

    private static float Victory(float t)
    {
        float[] notes = { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f };
        int idx = Math.Min(notes.Length - 1, (int)(t / 0.22f));
        float tone = Square(t * notes[idx], 0.35f) * 0.35f + Tri(t * notes[idx] * 0.5f) * 0.2f;
        return tone * MathF.Exp(-t * 0.8f);
    }

    private static uint _noise = 0x12345678;

    /// <summary>Slow, low and minor: the run is over.</summary>
    private static SoundEffect MakeGameOverMusic()
    {
        const float bpm = 58f;
        const int bars = 8;
        float beat = 60f / bpm;
        float seconds = bars * 4 * beat;
        int n = (int)(seconds * Rate);
        var buf = new float[n];
        int[][] chords =
        {
            new[] { 9, 0, 4 },   // Am
            new[] { 5, 9, 0 },   // F
            new[] { 2, 5, 9 },   // Dm
            new[] { 4, 8, 11 },  // E
        };
        int[] melody = { 12, 10, 8, 7, 5, 3, 2, 0 };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            int bar = (int)(t / (beat * 4)) % bars;
            int chord = (bar / 2) % chords.Length;

            float pad = 0f;
            foreach (int semi in chords[chord]) pad += Tri(t * Midi(45 + semi)) * 0.16f;
            pad *= 0.65f + 0.35f * MathF.Sin(t * 0.7f);

            float melPhase = t % (beat * 4);
            float melFreq = Midi(57 + melody[bar % melody.Length]);
            float mel = (Tri(t * melFreq) * 0.22f + Square(t * melFreq * 0.5f, 0.4f) * 0.06f) *
                        MathF.Exp(-melPhase * 0.9f);

            float pulsePhase = t % (beat * 2);
            float pulse = MathF.Sin(pulsePhase * MathF.Tau * 55f) * MathF.Exp(-pulsePhase * 3f) * 0.18f;

            buf[i] = (pad + mel + pulse) * 0.8f;
        }
        int delay = (int)(beat * 1.5f * Rate);
        for (int i = delay; i < n; i++) buf[i] += buf[i - delay] * 0.22f;
        return FromSamples(buf, n, 0.85f);
    }

    /// <summary>Bright, bouncy and major: the attract music behind the high score table.</summary>
    private static SoundEffect MakeScoresMusic()
    {
        const float bpm = 132f;
        const int bars = 8;
        float beat = 60f / bpm;
        float seconds = bars * 4 * beat;
        int n = (int)(seconds * Rate);
        var buf = new float[n];
        int[][] chords =
        {
            new[] { 0, 4, 7 },   // C
            new[] { 9, 0, 4 },   // Am
            new[] { 5, 9, 0 },   // F
            new[] { 7, 11, 2 },  // G
        };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            int bar = (int)(t / (beat * 4)) % bars;
            int chord = (bar / 2) % chords.Length;
            int step = (int)(t / (beat / 2));
            float inStep = t % (beat / 2);
            int[] tones = chords[chord];

            float leadFreq = Midi(72 + tones[step % 3] + (step % 8 >= 4 ? 12 : 0));
            float lead = (Square(t * leadFreq, 0.25f) * 0.16f + Tri(t * leadFreq * 0.5f) * 0.10f) *
                         MathF.Exp(-inStep * 5f);

            float bassFreq = Midi(36 + tones[0]);
            float bass = (Square(t * bassFreq, 0.5f) * 0.18f + Tri(t * bassFreq) * 0.12f) *
                         MathF.Exp(-inStep * 4f);

            float hatPhase = t % (beat / 2);
            float hat = Noise(ref _noise) * MathF.Exp(-hatPhase * 60f) * 0.05f;
            float kickPhase = t % beat;
            float kick = MathF.Sin(kickPhase * MathF.Tau * (90f - kickPhase * 45f)) *
                         MathF.Exp(-kickPhase * 18f) * 0.22f;

            buf[i] = (lead + bass + hat + kick) * 0.8f;
        }
        int delay = (int)(beat * 0.5f * Rate);
        for (int i = delay; i < n; i++) buf[i] += buf[i - delay] * 0.12f;
        return FromSamples(buf, n, 0.9f);
    }

    private static SoundEffect MakeMusic()
    {
        const float bpm = 104f;
        const int bars = 8;
        float beat = 60f / bpm;
        float seconds = bars * 4 * beat;
        int n = (int)(seconds * Rate);
        var buf = new float[n];
        int[] chordRoots = { 55, 43, 65, 49 }; // A1, F1, C2, G1 (Hz-ish via semitone helper below)
        int[][] chords =
        {
            new[] { 9, 0, 4, 7 },   // Am
            new[] { 5, 9, 0, 4 },   // F
            new[] { 0, 4, 7, 11 },  // Cmaj7
            new[] { 7, 11, 2, 5 },  // G
        };
        // melodies as MIDI-ish semitone offsets from A2 (110 Hz)
        int[] melody =
        {
            12, 15, 19, 15, 12, 10, 12, 15,
            8, 12, 15, 12, 8, 7, 8, 12,
            3, 7, 12, 7, 3, 5, 7, 12,
            10, 14, 17, 19, 17, 14, 10, 7,
        };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            int bar = (int)(t / (beat * 4)) % bars;
            int chord = bar / 2;
            float local = t % (beat * 4);
            int step = (int)(t / (beat / 2));
            float inStep = t % (beat / 2);

            // bass: root on every beat with a little glide
            int root = 9 + (chord == 0 ? 0 : chord == 1 ? -4 : chord == 2 ? 3 : -2);
            float bassFreq = Midi(33 + root);
            float bassEnv = MathF.Exp(-inStep * 3.2f);
            float bass = (Tri(t * bassFreq) * 0.55f + Square(t * bassFreq * 0.5f, 0.5f) * 0.15f) * bassEnv;

            // pad/arpeggio
            int[] tones = chords[chord];
            int arpIndex = step % 4;
            float arpFreq = Midi(57 + tones[arpIndex]);
            float arpEnv = MathF.Exp(-inStep * 7f);
            float arp = Square(t * arpFreq, 0.125f) * 0.10f * arpEnv;

            // melody every 2 beats
            int melStep = (step / 4) % melody.Length;
            float melFreq = Midi(69 + melody[melStep]);
            float melEnv = MathF.Exp(-(t % (beat * 2)) * 1.3f);
            float mel = (Square(t * melFreq, 0.25f) * 0.13f + Tri(t * melFreq * 0.5f) * 0.09f) * melEnv;

            // percussion
            float kickPhase = t % beat;
            float kick = MathF.Sin(kickPhase * MathF.Tau * (70f - kickPhase * 35f)) * MathF.Exp(-kickPhase * 14f) * 0.35f;
            float hatPhase = t % (beat / 2);
            float hat = Noise(ref _noise) * MathF.Exp(-hatPhase * 55f) * 0.055f;

            buf[i] = (bass * 0.5f + arp + mel + kick + hat) * 0.7f;
        }
        // simple echo for cave atmosphere
        int delay = (int)(beat * 0.75f * Rate);
        for (int i = delay; i < n; i++) buf[i] += buf[i - delay] * 0.18f;

        return FromSamples(buf, n, 0.95f);
    }

    private static float Midi(int note) => 440f * MathF.Pow(2f, (note - 69) / 12f);

    public void Dispose()
    {
        foreach (var fx in _effects.Values) fx.Dispose();
        _effects.Clear();
        foreach (var instance in _musicInstances.Values) instance.Dispose();
        _musicInstances.Clear();
        foreach (var track in _tracks.Values) track.Dispose();
        _tracks.Clear();
    }
}
