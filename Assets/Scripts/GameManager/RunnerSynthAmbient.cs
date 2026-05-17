using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// RunnerSynthAmbient v2 — Música animada y alegre para gato endless runner.
//
// CAMBIO FUNDAMENTAL vs v1:
// v1 era la arquitectura de Nodulus (meditativa, 40 BPM, espaciada).
// v2 es música de acción alegre: 120-130 BPM, progresión mayor, ritmo marcado.
//
// TRES MODOS:
//   Menu     → 100 BPM, C-Am-F-G, arpegio cada beat, hihat suave
//   Game     → 125 BPM, C-G-Am-F, arpegio cada 0.5 beats, kick+hihat marcado
//              La velocidad del arpegio sube con speedRatio (hasta 0.25 beats)
//   GameOver → Am descendente solo, sin ritmo, fade out lento
//
// CAPAS:
//   1. Drones: 4 osciladores internos con phase accumulator (sin clicks)
//   2. Melodía: secuencia de 8 notas compuesta, alegre, rebotada
//   3. Bajo: PolifoniaV2 en octava baja marcando el ritmo
//   4. DrumMachine: kick + hihat cuantizado
//   5. LFO de amplitud sobre la mezcla total
// ═══════════════════════════════════════════════════════════════════════════════

public enum RunnerMusicMode { Menu, Game, GameOver }

[RequireComponent(typeof(AudioSource))]
public class RunnerSynthAmbient : MonoBehaviour
{
    [Header("Arpegio / Melodía (pool de SynthSFX, 4 instancias)")]
    public SynthSFX[] arpPool;

    [Header("Percusión")]
    public DrumMachine drums;

    [Header("Tempo")]
    [Range(80f, 140f)] public float bpmMenu     = 100f;
    [Range(100f, 160f)] public float bpmGame    = 125f;

    [Header("Volumen")]
    [Range(0f, 1f)] public float masterVolume = 0.45f;
    [Range(0f, 1f)] public float droneVolume  = 0.12f;
    [Range(0f, 1f)] public float arpVolume    = 0.28f;
    [Range(0f, 1f)] public float bassVolume   = 0.20f;

    [Header("Glide entre acordes")]
    [Range(0.2f, 2f)] public float glideTime = 0.8f;

    [Header("LFO")]
    [Range(0.5f, 8f)]  public float lfoFreq  = 4f;
    [Range(0f, 0.3f)]  public float lfoDepth = 0.12f;

    // =========================================================================
    // PROGRESIONES
    // =========================================================================

    private struct ChordDef
    {
        public float[] droneFreqs;   // 4 voces graves
        public float[] bassFreqs;    // 1-2 notas de bajo
        public float[] melodyFreqs;  // notas disponibles para melodía
        public int     bars;
    }

    // ── MENU: C - Am - F - G (tranquila pero alegre) ──────────────────────────
    private readonly ChordDef[] _menuChords = new ChordDef[]
    {
        new ChordDef {
            droneFreqs  = new[] { 130.81f, 164.81f, 196.00f, 261.63f },
            bassFreqs   = new[] { 65.41f  },
            melodyFreqs = new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f },
            bars = 2
        },
        new ChordDef {
            droneFreqs  = new[] { 110.00f, 164.81f, 220.00f, 261.63f },
            bassFreqs   = new[] { 55.00f  },
            melodyFreqs = new[] { 440.00f, 523.25f, 659.25f, 880.00f, 1046.5f },
            bars = 2
        },
        new ChordDef {
            droneFreqs  = new[] { 174.61f, 220.00f, 261.63f, 349.23f },
            bassFreqs   = new[] { 87.31f  },
            melodyFreqs = new[] { 349.23f, 523.25f, 698.46f, 880.00f, 1046.5f },
            bars = 2
        },
        new ChordDef {
            droneFreqs  = new[] { 196.00f, 246.94f, 293.66f, 392.00f },
            bassFreqs   = new[] { 98.00f  },
            melodyFreqs = new[] { 392.00f, 493.88f, 587.33f, 783.99f, 523.25f },
            bars = 2
        },
    };

    // ── GAME: C - G - Am - F (energética, acción) ─────────────────────────────
    private readonly ChordDef[] _gameChords = new ChordDef[]
    {
        new ChordDef {
            droneFreqs  = new[] { 130.81f, 196.00f, 261.63f, 329.63f },
            bassFreqs   = new[] { 65.41f, 130.81f },
            melodyFreqs = new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f },
            bars = 1
        },
        new ChordDef {
            droneFreqs  = new[] { 196.00f, 246.94f, 293.66f, 392.00f },
            bassFreqs   = new[] { 98.00f, 196.00f },
            melodyFreqs = new[] { 392.00f, 493.88f, 587.33f, 783.99f, 987.77f },
            bars = 1
        },
        new ChordDef {
            droneFreqs  = new[] { 110.00f, 164.81f, 220.00f, 261.63f },
            bassFreqs   = new[] { 55.00f, 110.00f },
            melodyFreqs = new[] { 440.00f, 523.25f, 659.25f, 880.00f, 1046.5f },
            bars = 1
        },
        new ChordDef {
            droneFreqs  = new[] { 174.61f, 220.00f, 261.63f, 349.23f },
            bassFreqs   = new[] { 87.31f, 174.61f },
            melodyFreqs = new[] { 349.23f, 440.00f, 523.25f, 698.46f, 523.25f },
            bars = 1
        },
    };

    // ── GAME OVER: Am solo ────────────────────────────────────────────────────
    private readonly ChordDef[] _gameOverChords = new ChordDef[]
    {
        new ChordDef {
            droneFreqs  = new[] { 110.00f, 130.81f, 164.81f, 220.00f },
            bassFreqs   = new[] { 55.00f },
            melodyFreqs = new[] { 220.00f },
            bars = 4
        },
    };

    // =========================================================================
    // MELODÍA COMPUESTA — 8 frases para cada modo
    // Patrón: índices en melodyFreqs[] del acorde activo
    // Diseñado para sonar alegre y rebotado (notas cortas, saltos)
    // =========================================================================

    // Frase Game: ascendente animada con saltos
    private readonly int[] _gameMelody = new int[]
    {
        0, 2, 1, 3, 2, 4, 3, 2,
        0, 3, 1, 4, 2, 3, 0, 4
    };

    // Frase Menu: más suave, más lenta
    private readonly int[] _menuMelody = new int[]
    {
        0, 1, 2, 1, 0, 2, 3, 2,
        1, 3, 2, 4, 3, 2, 1, 0
    };

    // ── Estado interno ────────────────────────────────────────────────────────
    private float  _sr;
    private double _lfoPhase = 0.0;
    private bool   _running  = false;

    private const int NumDrones = 4;
    private double[] _dronePhase   = new double[NumDrones];
    private double[] _droneFreqCur = new double[NumDrones];
    private double[] _droneFreqTgt = new double[NumDrones];
    private float    _droneAmp     = 0f;
    private float    _droneAmpTgt  = 0f;

    // Bajo interno (2 osciladores)
    private const int NumBass = 2;
    private double[] _bassPhase   = new double[NumBass];
    private double[] _bassFreqCur = new double[NumBass];
    private double[] _bassFreqTgt = new double[NumBass];

    private readonly float[] _droneHarms = { 1f, 0.25f, 0.06f };
    private readonly float[] _bassHarms  = { 1f, 0.50f, 0.18f, 0.05f };

    private RunnerMusicMode _mode       = RunnerMusicMode.Menu;
    private RunnerMusicMode _newMode    = RunnerMusicMode.Menu;
    private bool            _modeChanged = false;

    private int   _arpPoolIdx = 0;
    private float _speedRatio = 0f;

    private volatile int _activeChord = 0;

    private float CurrentBpm  => _mode == RunnerMusicMode.Game ? bpmGame : bpmMenu;
    private ChordDef[] CurrentProg => _mode == RunnerMusicMode.Menu     ? _menuChords
                                    : _mode == RunnerMusicMode.Game     ? _gameChords
                                    : _gameOverChords;
    private int[] CurrentMelody => _mode == RunnerMusicMode.Game ? _gameMelody : _menuMelody;

    private System.Random _rng = new System.Random();

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _sr = AudioSettings.outputSampleRate;

        for (int i = 0; i < NumDrones; i++)
        {
            _dronePhase[i]   = 0.0;
            _droneFreqCur[i] = _menuChords[0].droneFreqs[i];
            _droneFreqTgt[i] = _menuChords[0].droneFreqs[i];
        }
        for (int i = 0; i < NumBass; i++)
        {
            _bassPhase[i]   = 0.0;
            _bassFreqCur[i] = _menuChords[0].bassFreqs[Mathf.Min(i, _menuChords[0].bassFreqs.Length - 1)];
            _bassFreqTgt[i] = _bassFreqCur[i];
        }

        var aud          = GetComponent<AudioSource>();
        aud.clip         = AudioClip.Create("runner_amb", (int)_sr, 1, (int)_sr, false);
        aud.loop         = true;
        aud.playOnAwake  = false;
        aud.spatialBlend = 0f;
        aud.volume       = 1f;
        aud.Play();
    }

    void Start() => StartAmbient();

    // ── API ───────────────────────────────────────────────────────────────────

    public void SetMode(RunnerMusicMode mode)
    {
        if (_mode == mode && _running) return;
        _newMode     = mode;
        _modeChanged = true;
    }

    public void UpdateSpeed(float speedRatio)
    {
        _speedRatio = Mathf.Clamp01(speedRatio);
    }

    public void StartAmbient()
    {
        if (_running) return;
        _running     = true;
        _droneAmpTgt = 1f;
        StartCoroutine(ProgressionLoop());
        StartCoroutine(MelodyLoop());
    }

    public void StopAmbient()
    {
        _running     = false;
        _droneAmpTgt = 0f;
        StopAllCoroutines();
        if (drums != null) drums.masterVolume = 0f;
        ForceStopArp();
    }

    private void ForceStopArp()
    {
        if (arpPool == null) return;
        foreach (var s in arpPool) if (s != null) s.isActive = false;
    }

    void OnDisable() => StopAmbient();

    // ── Progression Loop ──────────────────────────────────────────────────────

    private IEnumerator ProgressionLoop()
    {
        while (_running)
        {
            if (_modeChanged)
            {
                _mode        = _newMode;
                _modeChanged = false;
                _activeChord = 0;

                // Configurar percusión según modo
                if (drums != null)
                {
                    if (_mode == RunnerMusicMode.Game)
                    {
                        // Patrón 4/4 enérgico — kick y hihat marcados
                        drums.masterVolume  = 0.22f;
                        drums.kickStartFreq = 90f;
                        drums.kickDecay     = 0.18f;
                        drums.hihatDecay    = 0.04f;
                        StartCoroutine(drums.PlayPattern_Basic(bpmGame, 9999));
                    }
                    else if (_mode == RunnerMusicMode.Menu)
                    {
                        // Patrón vals suave en menu
                        drums.masterVolume  = 0.10f;
                        drums.kickStartFreq = 70f;
                        StartCoroutine(drums.PlayPattern_Waltz(bpmMenu, 9999));
                    }
                    else
                    {
                        drums.masterVolume = 0f;
                    }
                }

                _droneAmpTgt = _mode == RunnerMusicMode.GameOver ? 0.5f : 1f;
            }

            var prog = CurrentProg;

            for (int c = 0; c < prog.Length; c++)
            {
                if (!_running || _modeChanged) break;

                _activeChord = c;

                // Actualizar targets de drone y bajo
                for (int d = 0; d < NumDrones; d++)
                    _droneFreqTgt[d] = prog[c].droneFreqs[d];

                for (int b = 0; b < NumBass; b++)
                    _bassFreqTgt[b] = prog[c].bassFreqs[Mathf.Min(b, prog[c].bassFreqs.Length - 1)];

                float beat = 60f / CurrentBpm;
                yield return new WaitForSeconds(beat * prog[c].bars * 4f);
            }
        }
    }

    // ── Melody Loop ───────────────────────────────────────────────────────────
    // Toca la melodía compuesta cuantizada al BPM.
    // En modo Game: notas cortas y rebotadas (decay 0.2s), una cada 0.5 beats.
    // En modo Menu: notas más largas (decay 0.5s), una cada beat.
    // La velocidad aumenta con speedRatio en modo Game.

    private IEnumerator MelodyLoop()
    {
        // Esperar 1 compás antes de entrar la melodía
        yield return new WaitForSeconds(60f / CurrentBpm * 4f);

        int step = 0;

        while (_running)
        {
            if (_mode != RunnerMusicMode.GameOver &&
                arpPool != null && arpPool.Length > 0)
            {
                var prog    = CurrentProg;
                int ci      = _activeChord % prog.Length;
                var chord   = prog[ci];
                var melody  = CurrentMelody;

                int noteIdx = Mathf.Clamp(melody[step % 16], 0, chord.melodyFreqs.Length - 1);
                float freq  = chord.melodyFreqs[noteIdx];

                // Variación leve de frecuencia → vivo, no robótico
                float variation = 1f + (float)(_rng.NextDouble() * 0.04 - 0.02);
                freq *= variation;

                SynthSFX sfx = arpPool[_arpPoolIdx % arpPool.Length];
                _arpPoolIdx++;

                if (sfx != null)
                {
                    if (_mode == RunnerMusicMode.Game)
                    {
                        // Notas cortas y rebotadas — energéticas
                        sfx.numberOfHarmonics  = 4;
                        sfx.harmonicAmplitudes = new float[]
                            { 1f, 0.40f, 0.15f, 0.05f, 0f, 0f, 0f, 0f, 0f, 0f };
                        sfx.Play(freq, SynthSFX.SynthType.Additive,
                            atk: 0.006f, dec: 0.18f, sus: 0f, rel: 0.20f,
                            vol: arpVolume * 0.90f);
                    }
                    else
                    {
                        // Notas más suaves en menu
                        sfx.numberOfHarmonics  = 3;
                        sfx.harmonicAmplitudes = new float[]
                            { 1f, 0.30f, 0.08f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
                        sfx.Play(freq, SynthSFX.SynthType.Additive,
                            atk: 0.01f, dec: 0.45f, sus: 0.05f, rel: 0.40f,
                            vol: arpVolume * 0.70f);
                    }
                }
            }

            step++;

            // Subdivisión cuantizada: Game se acelera con velocidad
            float subdivision;
            if (_mode == RunnerMusicMode.Game)
                subdivision = Mathf.Lerp(0.5f, 0.25f, _speedRatio);
            else
                subdivision = 1.0f;

            yield return new WaitForSeconds((60f / CurrentBpm) * subdivision);
        }
    }

    // ── OnAudioFilterRead — drones + bajo + LFO ───────────────────────────────

    void OnAudioFilterRead(float[] data, int channels)
    {
        double twoPi   = 2.0 * System.Math.PI;
        double lfoInc  = twoPi * lfoFreq / _sr;
        double alpha   = 1.0 - System.Math.Pow(0.001, 1.0 / (_sr * glideTime));
        float  envAlpha = 1f - Mathf.Exp(-3f / ((float)_sr * 0.3f));

        for (int i = 0; i < data.Length; i += channels)
        {
            // ── Fade de amplitud ───────────────────────────────────────────
            _droneAmp += (_droneAmpTgt - _droneAmp) * envAlpha;

            // ── Drones con phase accumulator ───────────────────────────────
            float droneMix = 0f;
            for (int d = 0; d < NumDrones; d++)
            {
                _droneFreqCur[d] += alpha * (_droneFreqTgt[d] - _droneFreqCur[d]);
                _dronePhase[d]   += twoPi * _droneFreqCur[d] / _sr;
                if (_dronePhase[d] > twoPi) _dronePhase[d] -= twoPi;

                float s = 0f;
                for (int h = 0; h < _droneHarms.Length; h++)
                    s += _droneHarms[h] * (float)System.Math.Sin(_dronePhase[d] * (h + 1));
                droneMix += s / _droneHarms.Length;
            }
            droneMix = (droneMix / NumDrones) * droneVolume * _droneAmp;

            // ── Bajo con phase accumulator ─────────────────────────────────
            float bassMix = 0f;
            for (int b = 0; b < NumBass; b++)
            {
                _bassFreqCur[b] += alpha * (_bassFreqTgt[b] - _bassFreqCur[b]);
                _bassPhase[b]   += twoPi * _bassFreqCur[b] / _sr;
                if (_bassPhase[b] > twoPi) _bassPhase[b] -= twoPi;

                float s = 0f;
                for (int h = 0; h < _bassHarms.Length; h++)
                    s += _bassHarms[h] * (float)System.Math.Sin(_bassPhase[b] * (h + 1));
                bassMix += s / _bassHarms.Length;
            }
            bassMix = (bassMix / NumBass) * bassVolume * _droneAmp;

            // ── LFO rápido (tremolo ligero) ────────────────────────────────
            // A 4Hz crea un pulso rítmico muy suave que da energía a la mezcla
            float lfoVal = 0.5f + 0.5f * (float)System.Math.Sin(_lfoPhase);
            float gain   = (1f - lfoDepth) + lfoDepth * lfoVal;

            float outSample = (data[i] + droneMix + bassMix) * gain * masterVolume;
            data[i] = outSample;
            if (channels == 2) data[i + 1] = outSample;

            _lfoPhase += lfoInc;
            if (_lfoPhase > twoPi) _lfoPhase -= twoPi;
        }
    }
}