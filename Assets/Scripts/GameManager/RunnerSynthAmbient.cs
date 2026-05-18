using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// RunnerSynthAmbient v5 — C minor blues, vibe funk callejero
//
// BASADO EN PROPUESTA DEL USUARIO:
//   • Melodía: C minor blues (C4 Eb4 F4 G4 | Bb4 G4 F4 C4 | C4 Eb4 F4 F#4 G4 | C5 G4 Eb4 C4)
//   • Blue note F#4 en compás 3 para el "toque sucio jazz"
//   • Bajo square bass funk siguiendo progresión Cm7 | Fm7 | G7 | Cm7
//   • Bateria: kick/snare/hihat pattern funk
//   • Loop de 4 compases a 130 BPM
// ═══════════════════════════════════════════════════════════════════════════════

public enum RunnerMusicMode { Menu, Game, GameOver }

[RequireComponent(typeof(AudioSource))]
public class RunnerSynthAmbient : MonoBehaviour
{
    [Header("Percusión")]
    public DrumMachine drums;

    [Header("Tempo")]
    [Range(110f, 175f)] public float bpmGame = 130f;
    [Range(70f,  100f)] public float bpmMenu = 80f;

    [Header("Volumen por capa")]
    [Range(0f, 1f)] public float masterVolume  = 0.50f;
    [Range(0f, 1f)] public float melodyVolume  = 0.28f;
    [Range(0f, 1f)] public float bassVolume    = 0.32f;
    [Range(0f, 1f)] public float padVolume     = 0.12f;

    [Header("Capas activas")]
    public bool playMelody = true;
    public bool playBass   = true;
    public bool playPad    = true;
    public bool playDrums  = true;

    // =========================================================================
    // MELODÍA GAME — C minor blues, 130 BPM, proposal exacta
    //
    // C1: C4 Eb4 | F4  G4  -  |
    // C2: Bb4 G4 | F4  C4  -  |
    // C3: C4 Eb4 F4 F#4 | G4  -  -  -  | (blue note F#4!)
    // C4: C5 G4 | Eb4 C4 -  -  |
    // =========================================================================

    private readonly float[] _gameMelody = new float[]
    {
        // Compás 1 — trote inicial
        261.63f,   // C4 (corchea)
        311.13f,   // Eb4 (corchea)
        349.23f,   // F4 (negra)
        392.00f,   // G4 (negra)
        0f,        // silencio (negra)
        // Compás 2 — esquivando obstáculos
        466.16f,   // Bb4 (corchea)
        392.00f,   // G4 (corchea)
        349.23f,   // F4 (negra)
        261.63f,   // C4 (negra)
        0f,        // silencio (negra)
        // Compás 3 — blue note + blanca
        261.63f,   // C4 (corchea)
        311.13f,   // Eb4 (corchea)
        349.23f,   // F4 (corchea)
        369.99f,   // F#4 (corchea) — blue note!
        392.00f,   // G4 (blanca)
        // Compás 4 — salto y aterrizaje
        523.25f,   // C5 (corchea)
        392.00f,   // G4 (corchea)
        311.13f,   // Eb4 (negra)
        261.63f,   // C4 (negra)
        0f,        // silencio (negra)
    };

    // 20 notas (2+2+2+1 silencias = 20 positions)
    private readonly float[] _gameMelodyDur = new float[]
    {
        // C1: 2 corcheas + 2 negras + silencio
        0.5f, 0.5f, 1.0f, 1.0f, 1.0f,
        // C2: 2 corcheas + 2 negras + silencio
        0.5f, 0.5f, 1.0f, 1.0f, 1.0f,
        // C3: 4 corcheas + 1 blanca
        0.5f, 0.5f, 0.5f, 0.5f, 2.0f,
        // C4: 2 corcheas + 2 negras + silencio
        0.5f, 0.5f, 1.0f, 1.0f, 1.0f,
    };

    // Melodía Menu — más suave, tipo lo-fi chill
    // C5  G4  E5  D5 | C5  A4  G4  C5
    private readonly float[] _menuMelody = new float[]
    {
        523.25f, 392.00f, 659.25f, 587.33f,
        523.25f, 440.00f, 392.00f, 523.25f
    };
    private readonly float[] _menuMelodyDur = new float[]
    {
        1.0f, 1.0f, 1.0f, 1.0f, 1.5f, 0.5f, 1.0f, 2.0f
    };

    // =========================================================================
    // BAJO GAME — C minor blues, square bass funk
    // I=Cm7 | IV=Fm7 | V=G7 | I=Cm7
    // Square wave para ese tono "saxofón/trompeta funk"
    //
    // C1 (Cm):  C2  C2  Eb2  G2
    // C2 (Fm):  F1  F1  Ab1  C2
    // C3 (G7):  G1  G1  Bb1  D2
    // C4 (Cm):  C2  C2  Eb2  G2
    // =========================================================================

    private readonly float[] _gameBassNotes = new float[]
    {
        65.41f,   65.41f,   77.78f,   98.00f,   // Cm: C2 C2 Eb2 G2
        43.65f,   43.65f,   51.91f,   65.41f,   // Fm: F1 F1 Ab1 C2
        49.00f,   49.00f,   58.27f,   73.42f,   // G7: G1 G1 Bb1 D2
        65.41f,   65.41f,   77.78f,   98.00f,   // Cm: C2 C2 Eb2 G2
    };
    private readonly float[] _gameBassDur = new float[]
    {
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f,
    };

    // Bajo Menu — más simple, solo raíces
    private readonly float[] _menuBassNotes = new float[]
    {
        65.41f, 65.41f, 98.00f, 98.00f,
        55.00f, 55.00f, 87.31f, 87.31f,
    };
    private readonly float[] _menuBassDur = new float[]
    {
        2f, 2f, 2f, 2f, 2f, 2f, 2f, 2f,
    };

    // =========================================================================
    // PAD GAME — C minor blues, acordes menores con 7ma
    // I=Cm7: C3 Eb3 G3 Bb3  |  IV=Fm7: F3 Ab3 C4 Eb4
    // V=G7: G2 Bb2 D3 F3    |  I=Cm7: C3 Eb3 G3 Bb3
    // =========================================================================

    private readonly float[][] _gamePad = new float[][]
    {
        new[] { 130.81f, 155.56f, 196.00f, 233.08f },  // Cm7: C Eb G Bb
        new[] { 174.61f, 207.65f, 261.63f, 311.13f },  // Fm7: F Ab C Eb
        new[] { 98.00f,  116.54f, 146.83f, 174.61f },  // G7: G Bb D F
        new[] { 130.81f, 155.56f, 196.00f, 233.08f },  // Cm7: C Eb G Bb
    };

    // ── Estado interno ────────────────────────────────────────────────────────
    private float _sr;
    private bool  _running = false;

    // ── Melodía ───────────────────────────────────────────────────────────────
    private double _mPhase    = 0.0;
    private double _mFreq     = 523.25;
    private float  _mEnv      = 0f;
    private float  _mAttack   = 0.008f;
    private float  _mRelease  = 0.06f;

    // ── Bajo ──────────────────────────────────────────────────────────────────
    private double _bPhase    = 0.0;
    private double _bFreq     = 65.41;
    private float  _bEnv      = 0f;
    private float  _bAttack   = 0.003f;
    private float  _bRelease  = 0.12f;

    // ── Pad — 4 voces + chorus (2 osciladores desafinados ±4 cents) ───────────
    // _pPhase[0..3] = voces principales, _pPhase[4..7] = coro desafinado
    private double[] _pPhase  = new double[8];
    private double[] _pFreq   = new double[8] { 130.81, 164.81, 196.00, 246.94,
                                                 130.81, 164.81, 196.00, 246.94 };
    private double[] _pFreqTgt = new double[8] { 130.81, 164.81, 196.00, 246.94,
                                                  130.81, 164.81, 196.00, 246.94 };
    private float    _pEnv    = 0f;
    private double   _padAlpha = 0.0;
    // Factor de desafinación del chorus (±4 cents → ~1.0023)
    private const double CHORUS_DETUNE = 1.00231f;

    // ── LFO ───────────────────────────────────────────────────────────────────
    private double _lfoPhase  = 0.0;
    private float  _lfoFreq   = 0.4f;   // un poco más lento → más hipnótico
    private float  _lfoDepth  = 0.07f;

    // ── LFO2 — modulación sutil del filtro del bajo (shimmer) ─────────────────
    private double _lfo2Phase = 0.0;
    private float  _lfo2Freq  = 2.0f;

    // ── Modo ──────────────────────────────────────────────────────────────────
    private RunnerMusicMode _mode        = RunnerMusicMode.Menu;
    private RunnerMusicMode _newMode     = RunnerMusicMode.Menu;
    private bool            _modeChanged = false;

    // Referencia al BassAndPadLoop activo para cancelarlo limpiamente al cambiar modo
    private Coroutine _bassLoopCoroutine = null;

    // ── Comunicación hilo principal → hilo de audio ───────────────────────────
    private volatile float _nextMelodyFreq  = 523.25f;
    private volatile bool  _melodyNoteOn    = false;
    private volatile float _nextBassFreq    = 65.41f;
    private volatile bool  _bassNoteOn      = false;
    private volatile float _nextPad0        = 130.81f;
    private volatile float _nextPad1        = 164.81f;
    private volatile float _nextPad2        = 196.00f;
    private volatile float _nextPad3        = 246.94f;
    private volatile bool  _padNoteOn       = false;
    private volatile float _globalAmpTarget = 0f;
    private volatile float _bpmRatio        = 1f; // para ajuste dinámico de velocidad

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _sr       = AudioSettings.outputSampleRate;
        // Alpha de glide del pad: 1.5s de tiempo de respuesta
        _padAlpha = 1.0 - System.Math.Pow(0.001, 1.0 / (_sr * 1.5));

        var aud          = GetComponent<AudioSource>();
        aud.clip         = AudioClip.Create("runner_v4", (int)_sr, 1, (int)_sr, false);
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
        // Sube el BPM ligeramente con la velocidad (hasta +8%)
        _bpmRatio = 1f + Mathf.Clamp01(speedRatio) * 0.08f;
    }

    public void StartAmbient()
    {
        if (_running) return;
        _running         = true;
        _globalAmpTarget = 1f;
        StartCoroutine(MusicLoop());
    }

    public void StopAmbient()
    {
        _running         = false;
        _globalAmpTarget = 0f;
        _melodyNoteOn    = false;
        _bassNoteOn      = false;
        _padNoteOn       = false;
        StopAllCoroutines();
        if (drums != null) drums.masterVolume = 0f;
    }

    void OnDisable() => StopAmbient();

    // ── Music Loop ────────────────────────────────────────────────────────────

    private IEnumerator MusicLoop()
    {
        while (_running)
        {
            if (_modeChanged)
            {
                _mode        = _newMode;
                _modeChanged = false;

                if (drums != null)
                {
                    // IMPORTANTE: llamar StopAllCoroutines en el DrumMachine,
                    // NO en este MonoBehaviour — de lo contrario MusicLoop se mata a sí mismo.
                    drums.StopAllCoroutines();

                    if (_mode == RunnerMusicMode.Game && playDrums)
                    {
                        drums.masterVolume  = 0.22f;
                        drums.kickStartFreq = 85f;
                        drums.kickEndFreq   = 35f;
                        drums.kickDecay     = 0.12f;
                        drums.snareDecay    = 0.15f;
                        drums.snareToneFreq = 180f;
                        drums.hihatDecay    = 0.03f;
                        drums.StartCoroutine(drums.PlayPattern_Funk(bpmGame, 9999));
                    }
                    else if (_mode == RunnerMusicMode.Menu && playDrums)
                    {
                        drums.masterVolume  = 0.08f;
                        drums.kickStartFreq = 70f;
                        drums.kickDecay     = 0.22f;
                        drums.StartCoroutine(drums.PlayPattern_Waltz(bpmMenu, 9999));
                    }
                    else
                    {
                        drums.masterVolume = 0f;
                    }
                }

                _globalAmpTarget = (_mode == RunnerMusicMode.GameOver) ? 0.25f : 1f;
            }

            // ── GAME OVER ─────────────────────────────────────────────────────
            if (_mode == RunnerMusicMode.GameOver)
            {
                // Pad Am con cuarta suspendida, descendente y triste
                SetPad(110.00f, 146.83f, 164.81f, 196.00f);
                yield return new WaitForSeconds(4f);
                continue;
            }

            // ── GAME / MENU ───────────────────────────────────────────────────
            bool isGame    = (_mode == RunnerMusicMode.Game);
            float bpm      = isGame ? bpmGame : bpmMenu;
            float beat     = 60f / bpm;

            float[] melody    = isGame ? _gameMelody     : _menuMelody;
            float[] melDur    = isGame ? _gameMelodyDur  : _menuMelodyDur;
            float[] bassNotes = isGame ? _gameBassNotes  : _menuBassNotes;
            float[] bassDur   = isGame ? _gameBassDur    : _menuBassDur;
            float[][] pad     = _gamePad;

            // Cancelar el loop anterior de bajo/pad si sigue corriendo, luego lanzar uno nuevo
            if (_bassLoopCoroutine != null) StopCoroutine(_bassLoopCoroutine);
            _bassLoopCoroutine = StartCoroutine(BassAndPadLoop(beat, bassNotes, bassDur, pad));

            // Loop de melodía — itera sobre todas las notas
            for (int n = 0; n < melody.Length; n++)
            {
                if (!_running || _modeChanged) break;

                if (playMelody)
                {
                    _nextMelodyFreq = melody[n];
                    _melodyNoteOn   = true;
                }

                float noteDur  = melDur[n] * beat;
                // Hold del 80% de la nota → ligadura natural entre notas cercanas
                float holdTime = noteDur * 0.80f;
                float gapTime  = noteDur * 0.20f;

                yield return new WaitForSeconds(holdTime);
                _melodyNoteOn = false;
                yield return new WaitForSeconds(gapTime);
            }
        }
    }

    // ── Loop de bajo y pad — corre en paralelo ────────────────────────────────

    private IEnumerator BassAndPadLoop(float beat, float[] bassNotes, float[] bassDur, float[][] pad)
    {
        // Cuántos "bloques de acorde" hay (1 acorde = 4 beats)
        // El bajo tiene una nota por beat, el pad cambia cada 4 beats
        int totalBassNotes = bassNotes.Length;
        int padChanges     = totalBassNotes / 4; // 4 notas de bajo por acorde

        for (int b = 0; b < totalBassNotes; b++)
        {
            if (!_running || _modeChanged) yield break;

            // Actualizar el pad cada 4 notas de bajo (= cada compás)
            if (b % 4 == 0 && playPad)
            {
                int padIdx = (b / 4) % pad.Length;
                float[] chord = pad[padIdx];
                SetPad(chord[0], chord[1], chord[2],
                       chord.Length > 3 ? chord[3] : chord[2] * 1.5f);
            }

            // Bajo
            if (playBass)
            {
                _nextBassFreq = bassNotes[b];
                _bassNoteOn   = true;

                float dur = bassDur[b] * beat;
                yield return new WaitForSeconds(dur * 0.55f);
                _bassNoteOn = false;
                yield return new WaitForSeconds(dur * 0.45f);
            }
            else
            {
                yield return new WaitForSeconds(bassDur[b] * beat);
            }
        }
    }

    private void SetPad(float f0, float f1, float f2, float f3 = 0f)
    {
        _nextPad0  = f0;
        _nextPad1  = f1;
        _nextPad2  = f2;
        _nextPad3  = f3 > 0f ? f3 : f2 * 1.498f; // quinta si no se especifica
        _padNoteOn = true;
    }

    // ── OnAudioFilterRead — síntesis pura ─────────────────────────────────────

    void OnAudioFilterRead(float[] data, int channels)
    {
        double twoPi = 2.0 * System.Math.PI;

        float mAtkAlpha = 1f - Mathf.Exp(-1f / (_mAttack  * (float)_sr));
        float mRelAlpha = 1f - Mathf.Exp(-1f / (_mRelease * (float)_sr));
        float bAtkAlpha = 1f - Mathf.Exp(-1f / (_bAttack  * (float)_sr));
        float bRelAlpha = 1f - Mathf.Exp(-1f / (_bRelease * (float)_sr));
        float pAlpha    = 1f - Mathf.Exp(-2f  / (float)_sr);
        float gAlpha    = 1f - Mathf.Exp(-1.5f / (float)_sr);

        double lfoInc  = twoPi * _lfoFreq  / _sr;
        double lfo2Inc = twoPi * _lfo2Freq / _sr;

        // Leer volátiles una sola vez
        double mFreqNew = _nextMelodyFreq;
        bool   mOn      = _melodyNoteOn;
        double bFreqNew = _nextBassFreq;
        bool   bOn      = _bassNoteOn;
        bool   pOn      = _padNoteOn;
        double p0 = _nextPad0, p1 = _nextPad1, p2 = _nextPad2, p3 = _nextPad3;
        float  gTgt     = _globalAmpTarget;

        _mFreq = mFreqNew;
        _bFreq = bFreqNew;
        // Voz principal del pad
        _pFreqTgt[0] = p0; _pFreqTgt[1] = p1;
        _pFreqTgt[2] = p2; _pFreqTgt[3] = p3;
        // Chorus: ligeramente desafinado
        _pFreqTgt[4] = p0 * CHORUS_DETUNE;
        _pFreqTgt[5] = p1 * CHORUS_DETUNE;
        _pFreqTgt[6] = p2 * CHORUS_DETUNE;
        _pFreqTgt[7] = p3 * CHORUS_DETUNE;

        float _gAmp = 0f;

        for (int i = 0; i < data.Length; i += channels)
        {
            // ── Amp global ────────────────────────────────────────────────────
            _gAmp += (gTgt - _gAmp) * gAlpha;

            // ── LFO principal (modulación de amplitud del pad) ─────────────────
            float lfoVal  = 0.5f + 0.5f * (float)System.Math.Sin(_lfoPhase);
            float lfoGain = (1f - _lfoDepth) + _lfoDepth * lfoVal;

            // ── LFO2 (shimmer en el bajo — modulación de tono muy leve) ─────────
            float lfo2Val = (float)System.Math.Sin(_lfo2Phase) * 0.003f; // ±0.3%

            // ── MELODÍA — onda cuadrada con 5 armónicos (brillante y llena) ────
            float melSample = 0f;
            if (playMelody)
            {
                float mTgt  = mOn ? 1f : 0f;
                float alpha = mOn ? mAtkAlpha : mRelAlpha;
                _mEnv += (mTgt - _mEnv) * alpha;

                _mPhase += twoPi * _mFreq / _sr;
                if (_mPhase > twoPi) _mPhase -= twoPi;

                // Cuadrada aproximada con armónicos impares 1, 3, 5, 7, 9
                // Amplitudes: 1/1, 1/3, 1/5, 1/7, 1/9 (serie de Fourier de cuadrada)
                float m1 = (float)System.Math.Sin(_mPhase);
                float m3 = (float)System.Math.Sin(_mPhase * 3) * 0.333f;
                float m5 = (float)System.Math.Sin(_mPhase * 5) * 0.200f;
                float m7 = (float)System.Math.Sin(_mPhase * 7) * 0.143f;
                float m9 = (float)System.Math.Sin(_mPhase * 9) * 0.111f;

                // Normalizar y aplicar envelope
                melSample = (m1 + m3 + m5 + m7 + m9) / 1.787f * _mEnv * melodyVolume;
            }

            // ── BAJO — square wave (saxofón/trompeta funk) + saw sub ──────────────
            float basSample = 0f;
            if (playBass)
            {
                float bTgt  = bOn ? 1f : 0f;
                float alpha = bOn ? bAtkAlpha : bRelAlpha;
                _bEnv += (bTgt - _bEnv) * alpha;

                double bFreqMod = _bFreq * (1.0 + lfo2Val);
                _bPhase += twoPi * bFreqMod / _sr;
                if (_bPhase > twoPi) _bPhase -= twoPi;

                // Square wave principal (fuerte, brillante)
                float squareRaw = (float)System.Math.Sign(System.Math.Sin(_bPhase));
                // Saw sub para grosor
                float sawSub  = (float)((_bPhase / twoPi) * 2.0 - 1.0);
                // Segundo armónico para calidez
                float sin2    = (float)System.Math.Sin(_bPhase * 2) * 0.3f;

                // Mezcla: square da punch, saw sub da cuerpo, sin2 da calidez
                basSample = (squareRaw * 0.55f + sawSub * 0.25f + sin2 * 0.20f) * _bEnv * bassVolume;
            }

            // ── PAD — 4 voces seno + chorus de 4 voces desafinadas ───────────────
            float padSample = 0f;
            if (playPad)
            {
                float pTgt = pOn ? 1f : 0.6f;
                _pEnv += (pTgt - _pEnv) * pAlpha;

                // 8 osciladores: 4 principales + 4 chorus (desafinados)
                for (int v = 0; v < 8; v++)
                {
                    _pFreq[v] += _padAlpha * (_pFreqTgt[v] - _pFreq[v]);
                    _pPhase[v] += twoPi * _pFreq[v] / _sr;
                    if (_pPhase[v] > twoPi) _pPhase[v] -= twoPi;

                    // Voces del chorus suenan un poco más suaves
                    float amp = (v < 4) ? 1.0f : 0.5f;
                    padSample += (float)System.Math.Sin(_pPhase[v]) * amp;
                }
                // Normalizar (4×1 + 4×0.5 = 6 unidades)
                padSample = (padSample / 6f) * _pEnv * padVolume * lfoGain;
            }

            // ── Mezcla final ──────────────────────────────────────────────────
            // Soft clip muy suave para evitar clipping sin perder punch
            float mix = (melSample + basSample + padSample) * _gAmp * masterVolume;
            mix = mix / (1f + Mathf.Abs(mix) * 0.3f); // soft clip hiperbólico

            data[i] += mix;
            if (channels == 2) data[i + 1] += mix;

            _lfoPhase  += lfoInc;
            if (_lfoPhase  > twoPi) _lfoPhase  -= twoPi;
            _lfo2Phase += lfo2Inc;
            if (_lfo2Phase > twoPi) _lfo2Phase -= twoPi;
        }
    }
}