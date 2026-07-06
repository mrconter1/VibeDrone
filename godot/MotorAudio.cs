using System;
using Godot;

// Procedural quad-motor audio: no sound files, synthesized in real time and driven
// by "engine effort" (the model's thrust proxy = throttle + stick activity, i.e. how
// hard the motors are working). Four slightly detuned oscillators emulate the 4 motors
// beating against each other. Press S to cycle OFF -> variants -> OFF; the current
// variant shows on the HUD so we can pick the most realistic by ear.
public partial class MotorAudio : AudioStreamPlayer
{
    public static readonly string[] Names = { "OFF", "SINE", "SAW", "PULSE", "RICH" };
    public int Variant { get; private set; }          // 0 = off
    public string CurrentName => Names[Variant];

    private const int Rate = 44100;
    private AudioStreamGeneratorPlayback _pb = null!;
    private readonly double[] _phase = new double[4];
    private readonly float[] _detune = { 1.000f, 1.006f, 0.994f, 1.013f };  // 4 motors, slightly apart
    private float _effort;         // smoothed 0..1
    private float _effortTarget;
    private readonly Random _rng = new(1234);

    public override void _Ready()
    {
        Stream = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.08f };
        VolumeDb = -7f;
        Play();
        _pb = (AudioStreamGeneratorPlayback)GetStreamPlayback();
    }

    public void Cycle() => Variant = (Variant + 1) % Names.Length;
    public void SetEffort(float e) => _effortTarget = Mathf.Clamp(e, 0f, 1f);

    public override void _Process(double delta)
    {
        if (_pb == null) return;
        int frames = _pb.GetFramesAvailable();
        if (frames <= 0) return;

        // smooth effort toward target once per block (avoids zipper noise)
        _effort = Mathf.Lerp(_effort, _effortTarget, 1f - Mathf.Exp(-8f * (float)delta));

        if (Variant == 0)
        {
            for (int i = 0; i < frames; i++) _pb.PushFrame(Vector2.Zero);
            return;
        }

        float e = _effort;
        float baseFreq = 70f + e * 300f;       // motor tone rises with effort
        float amp = 0.06f + e * 0.30f;         // and gets louder

        for (int i = 0; i < frames; i++)
        {
            float s = 0f;
            for (int m = 0; m < 4; m++)
            {
                double p = _phase[m] + baseFreq * _detune[m] / Rate;
                if (p >= 1.0) p -= 1.0;
                _phase[m] = p;
                switch (Variant)
                {
                    case 1: s += (float)Math.Sin(p * 2.0 * Math.PI); break;               // SINE hum
                    case 2: s += (float)(2.0 * (p - Math.Floor(p + 0.5))); break;         // SAW buzz
                    case 3: s += p < 0.5 ? 1f : -1f; break;                                // PULSE
                    case 4:                                                                 // RICH: saw + whine
                        float saw = (float)(2.0 * (p - Math.Floor(p + 0.5)));
                        float whine = 0.3f * (float)Math.Sin(p * 2.0 * Math.PI * 3.0);
                        s += saw * 0.7f + whine;
                        break;
                }
            }
            s *= 0.25f;                                                    // average the 4 motors
            if (Variant == 4) s += (float)(_rng.NextDouble() * 2.0 - 1.0) * 0.12f * e;  // prop/air noise
            float v = s * amp;
            _pb.PushFrame(new Vector2(v, v));
        }
    }
}
