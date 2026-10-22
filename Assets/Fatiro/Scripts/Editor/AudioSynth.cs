using System;
using System.IO;
using UnityEngine;

namespace Fatiro.EditorTools
{
    /// <summary>
    /// Synthesises the app's sound cues as WAV files so the project has no third-party audio.
    /// Each cue is designed to match its event: a woody click for a snap, a low buzz for a
    /// rejection, a rising chime for a completed stage, filtered noise for rain, wind and quakes.
    /// </summary>
    public static class AudioSynth
    {
        private const int SampleRate = 22050;

        public static void WriteAll(string folder)
        {
            Directory.CreateDirectory(folder);
            Write(folder, "ui_tap", 0.06f, (t, d) => Tone(t, 1400f) * Envelope(t, d, 0.002f, 0.05f) * 0.35f);
            Write(folder, "snap", 0.18f, (t, d) => (Tone(t, 880f) * 0.5f + Tone(t, 1320f) * 0.3f + Noise() * 0.25f * Math.Exp(-t * 60)) * Envelope(t, d, 0.001f, 0.15f));
            Write(folder, "reject", 0.38f, (t, d) => Square(t, 140f) * 0.35f * Envelope(t, d, 0.005f, 0.1f) * (t < 0.16 || t > 0.22 ? 1 : 0));
            Write(folder, "site_placed", 0.45f, (t, d) => (Tone(t, 220f) + 0.5f * Tone(t, 330f)) * Envelope(t, d, 0.01f, 0.4f) * 0.6f + Noise() * 0.2f * Math.Exp(-t * 30));
            Write(folder, "stage_complete", 0.7f, (t, d) => Arpeggio(t, new[] { 523.25f, 659.25f, 783.99f }, 0.12f) * Envelope(t, d, 0.01f, 0.35f) * 0.5f);
            Write(folder, "build_complete", 1.2f, (t, d) => Arpeggio(t, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.14f) * Envelope(t, d, 0.01f, 0.6f) * 0.55f);
            Write(folder, "crack", 0.35f, (t, d) => Noise() * Math.Exp(-t * 18) * 0.9f + Tone(t, 90f) * Math.Exp(-t * 12) * 0.5f);
            Write(folder, "collapse", 0.9f, (t, d) => LowNoise(0.15f) * Math.Exp(-t * 4) * 0.9f + Tone(t, 55f) * Math.Exp(-t * 3) * 0.4f);
            Write(folder, "survived", 0.9f, (t, d) => Arpeggio(t, new[] { 392f, 523.25f, 659.25f }, 0.16f) * Envelope(t, d, 0.01f, 0.4f) * 0.45f);
            Write(folder, "warning", 0.6f, (t, d) => Tone(t, t < 0.3 ? 660f : 440f) * Envelope(t, d, 0.01f, 0.1f) * 0.4f);
            Write(folder, "session_pass", 1.6f, (t, d) => Arpeggio(t, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.13f) * Envelope(t, d, 0.01f, 0.8f) * 0.5f);
            Write(folder, "session_fail", 1.0f, (t, d) => Arpeggio(t, new[] { 392f, 349.2f, 311.1f }, 0.22f) * Envelope(t, d, 0.01f, 0.5f) * 0.45f);
            Write(folder, "loop_quake", 3.0f, (t, d) => LowNoise(0.04f) * (0.7f + 0.3f * (float)Math.Sin(t * 9)) * 0.9f + Tone(t, 38f) * 0.35f, loop: true);
            Write(folder, "loop_crowd", 3.0f, (t, d) => LowNoise(0.3f) * 0.4f * (0.6f + 0.4f * (float)Math.Sin(t * 3.1)) + Tone(t, 180f + 30f * (float)Math.Sin(t * 2)) * 0.05f, loop: true);
        }

        private static readonly System.Random random = new System.Random(7);
        private static float lowState;

        private static float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);

        /// <summary>One-pole low-pass filtered noise: smaller alpha means a deeper rumble.</summary>
        private static float LowNoise(float alpha)
        {
            lowState += alpha * (Noise() - lowState);
            return lowState * 2.5f;
        }

        private static float Tone(double t, float frequency) => (float)Math.Sin(2 * Math.PI * frequency * t);

        private static float Square(double t, float frequency) => Tone(t, frequency) >= 0 ? 1f : -1f;

        private static float Arpeggio(double t, float[] notes, float step)
        {
            var index = Math.Min(notes.Length - 1, (int)(t / step));
            var local = t - index * step;
            return Tone(t, notes[index]) * (float)Math.Exp(-local * 6) + 0.3f * Tone(t, notes[index] * 2f) * (float)Math.Exp(-local * 9);
        }

        private static float Envelope(double t, double duration, double attack, double release)
        {
            if (t < attack) return (float)(t / attack);
            if (t > duration - release) return (float)Math.Max(0, (duration - t) / release);
            return 1f;
        }

        private static void Write(string folder, string name, float seconds, Func<double, double, double> generator, bool loop = false)
        {
            var count = (int)(seconds * SampleRate);
            var samples = new float[count];
            lowState = 0f;
            for (var i = 0; i < count; i++)
            {
                var t = (double)i / SampleRate;
                samples[i] = Mathf.Clamp((float)generator(t, seconds), -1f, 1f);
            }

            if (loop)
            {
                // Cross-fade the ends so the loop has no click.
                var fade = SampleRate / 10;
                for (var i = 0; i < fade; i++)
                {
                    var weight = (float)i / fade;
                    samples[i] = samples[i] * weight + samples[count - fade + i] * (1f - weight);
                }
                Array.Resize(ref samples, count - fade);
            }

            using var stream = new FileStream(Path.Combine(folder, name + ".wav"), FileMode.Create);
            using var writer = new BinaryWriter(stream);
            var dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            foreach (var sample in samples) writer.Write((short)(sample * short.MaxValue * 0.9f));
        }
    }
}
