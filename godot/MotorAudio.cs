using System;
using Godot;

// Procedural quad-motor audio (no sound files). Four slightly detuned oscillators
// emulate the 4 motors beating together; pitch and volume track the model's thrust
// proxy (throttle + stick activity), falling to SILENCE at zero throttle. The sound
// menu (S) selects a variant and shapes tone; variant 0 = off.
//
// The 10 variants are two families derived from the SAW and RICH favourites:
//   1-5  SAW*  : tonal buzz (saw waves), little/no noise
//   6-10 RICH* : saw + harmonic whine + prop/air noise, more texture
public partial class MotorAudio : AudioStreamPlayer
{
    private struct P
    {
        public string Name;
        public float FLo, FHi;      // motor tone range (Hz) idle..full
        public float Detune;        // spread between the 4 motors (beating)
        public float Saw;           // saw-wave weight
        public float WhineAmt;      // added sine "whine" weight
        public int WhineMult;       // whine harmonic (x fundamental)
        public float Noise;         // prop/air noise amount
        public P(string n, float lo, float hi, float det, float saw, float wa, int wm, float nz)
        { Name = n; FLo = lo; FHi = hi; Detune = det; Saw = saw; WhineAmt = wa; WhineMult = wm; Noise = nz; }
    }

    // index 0 unused (OFF); 1..10 are the variants
    private static readonly P[] Presets =
    {
        default,
        new P("SAW-soft",    65, 320, 0.006f, 1.00f, 0.00f, 3, 0.00f),
        new P("SAW-mid",     75, 360, 0.010f, 1.00f, 0.10f, 2, 0.00f),
        new P("SAW-hi",      95, 430, 0.008f, 1.00f, 0.15f, 2, 0.02f),
        new P("SAW-fat",     60, 300, 0.016f, 1.00f, 0.00f, 3, 0.02f),
        new P("SAW-aggr",    80, 380, 0.012f, 1.00f, 0.20f, 4, 0.05f),
        new P("RICH-soft",   70, 320, 0.010f, 0.70f, 0.25f, 3, 0.06f),
        new P("RICH-mid",    75, 350, 0.013f, 0.70f, 0.30f, 3, 0.12f),
        new P("RICH-hi",     95, 420, 0.011f, 0.65f, 0.35f, 4, 0.10f),
        new P("RICH-grit",   70, 340, 0.015f, 0.60f, 0.30f, 3, 0.22f),
        new P("RICH-turbine",85, 400, 0.012f, 0.50f, 0.45f, 2, 0.15f),
    };
    public const int VariantCount = 10;

    // chosen defaults (SAW-soft with light tone shaping) - also the menu's initial values
    public const int DefaultVariant = 1;
    public const float DefLowPassHz = 15150f;
    public const float DefHighPassHz = 125f;
    public const float DefDrive = 0.01f;
    public const float DefMasterDb = -8f;

    public int Variant { get; private set; } = DefaultVariant;
    public string CurrentName => Variant == 0 ? "OFF" : $"{Variant}/{VariantCount} {Presets[Variant].Name}";

    private const int Rate = 44100;
    private AudioStreamGeneratorPlayback _pb = null!;
    private readonly double[] _phase = new double[4];
    private static readonly float[] _spread = { 0f, 1f, -1f, 1.6f };   // per-motor detune offsets
    private float _effort;         // smoothed 0..1
    private float _effortTarget;
    private readonly Random _rng = new(1234);

    // shaping effects on a dedicated bus (driven by the sound menu)
    private AudioEffectLowPassFilter _lp = null!;
    private AudioEffectHighPassFilter _hp = null!;
    private AudioEffectDistortion _dist = null!;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;   // keep sounding while the game is paused (menu open)

        int bus = AudioServer.GetBusIndex("Motor");
        if (bus == -1)
        {
            bus = AudioServer.BusCount;
            AudioServer.AddBus(bus);
            AudioServer.SetBusName(bus, "Motor");
            AudioServer.SetBusSend(bus, "Master");
            _lp = new AudioEffectLowPassFilter { CutoffHz = DefLowPassHz };
            _hp = new AudioEffectHighPassFilter { CutoffHz = DefHighPassHz };
            _dist = new AudioEffectDistortion { Mode = AudioEffectDistortion.ModeEnum.Clip, Drive = DefDrive };
            AudioServer.AddBusEffect(bus, _lp);
            AudioServer.AddBusEffect(bus, _hp);
            AudioServer.AddBusEffect(bus, _dist);
        }
        Bus = "Motor";

        Stream = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.08f };
        VolumeDb = DefMasterDb;
        Play();
        _pb = (AudioStreamGeneratorPlayback)GetStreamPlayback();
    }

    public void Cycle() => Variant = (Variant + 1) % (VariantCount + 1);
    public void SetVariant(int v) => Variant = Mathf.Clamp(v, 0, VariantCount);
    public void SetEffort(float e) => _effortTarget = Mathf.Clamp(e, 0f, 1f);

    // sound-menu audio tools
    public void SetLowPassHz(float hz) => _lp.CutoffHz = hz;
    public void SetHighPassHz(float hz) => _hp.CutoffHz = hz;
    public void SetDrive(float d) => _dist.Drive = Mathf.Clamp(d, 0f, 1f);
    public void SetMasterDb(float db) => VolumeDb = db;

    public override void _Process(double delta)
    {
        if (_pb == null) return;
        int frames = _pb.GetFramesAvailable();
        if (frames <= 0) return;

        _effort = Mathf.Lerp(_effort, _effortTarget, 1f - Mathf.Exp(-8f * (float)delta));

        if (Variant == 0)
        {
            for (int i = 0; i < frames; i++) _pb.PushFrame(Vector2.Zero);
            return;
        }

        P p = Presets[Variant];
        float e = _effort;                              // silent at zero throttle (no idle floor)
        float baseFreq = p.FLo + e * (p.FHi - p.FLo);
        float amp = e * 0.34f;                          // amplitude -> 0 as throttle -> 0

        for (int i = 0; i < frames; i++)
        {
            float s = 0f;
            for (int m = 0; m < 4; m++)
            {
                double ph = _phase[m] + baseFreq * (1f + p.Detune * _spread[m]) / Rate;
                if (ph >= 1.0) ph -= 1.0;
                _phase[m] = ph;
                float saw = (float)(2.0 * (ph - Math.Floor(ph + 0.5)));
                float whine = p.WhineAmt * (float)Math.Sin(ph * 2.0 * Math.PI * p.WhineMult);
                s += saw * p.Saw + whine;
            }
            s *= 0.25f;
            if (p.Noise > 0f) s += (float)(_rng.NextDouble() * 2.0 - 1.0) * p.Noise * e;
            float v = s * amp;
            _pb.PushFrame(new Vector2(v, v));
        }
    }
}
