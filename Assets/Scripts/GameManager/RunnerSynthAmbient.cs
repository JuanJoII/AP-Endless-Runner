using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// RunnerSynthAmbient v4 — Música procedural energética estilo Geometry Dash.
//
// CAMBIOS vs v3:
//   • Melodía de 32 notas con ritmo mixto (negras + corcheas + semicorcheas)
//     → frase larga que sube y baja, muy euforica, no monótona
//   • Contramelodía en 2do ciclo (sensación de estrofa / coro)
//   • Bajo con walking bass activo: nota por beat + octavas en corcheas
//   • Pad con 4 voces y chorus (2 osciladores desafinados ±3 cents)
//   • Melodía con 5 armónicos (más brillante y llena)
//   • Bajo con 2do armónico (más gordo y redondo)
//   • Modo Menu: groove lento tipo lo-fi, acordes que respiran
//   • BPM del juego subido a 135 para más energía
//   • Reverb sintético (comb filter muy simple) en el pad
// ═══════════════════════════════════════════════════════════════════════════════

public enum RunnerMusicMode { Menu, Game, GameOver }

[RequireComponent(typeof(AudioSource))]
public class RunnerSynthAmbient : MonoBehaviour
{
    [Header("Percusión")]
    public DrumMachine drums;

    [Header("Tempo")]
    [Range(110f, 175f)] public float bpmGame = 135f;
    [Range(70f,  100f)] public float bpmMenu = 80f;

    [Header("Volumen por capa")]
    [Range(0f, 1f)] public float masterVolume  = 0.50f;
    [Range(0f, 1f)] public float melodyVolume  = 0.28f;
    [Range(0f, 1f)] public float bassVolume    = 0.20f;
    [Range(0f, 1f)] public float padVolume     = 0.12f;

    [Header("Capas activas")]
    public bool playMelody = true;
    public bool playBass   = true;
    public bool playPad    = true;
    public bool playDrums  = true;

    // =========================================================================
    // MELODÍA GAME — Do mayor, eufórica, 32 notas, ritmo mixto
    //
    // Compases 1-2 (frase A — subida):
    //   C5  E5  G5  B5  | A5  G5  E5  G5  | F5  A5  C6  A5  | G5  E5  D5  E5
    // Compases 3-4 (frase B — clímax + bajada):
    //   G5  B5  D6  C6  | B5  A5  G5  F5  | E5  G5  A5  G5  | F5  E5  D5  C5
    // =========================================================================

    private readonly float[] _gameMelody = new float[]
    {
        // Frase A — ascenso eufórico
        523.25f, 659.25f, 783.99f, 987.77f,   // C5 E5 G5 B5
        880.00f, 783.99f, 659.25f, 783.99f,   // A5 G5 E5 G5
        698.46f, 880.00f, 1046.5f, 880.00f,   // F5 A5 C6 A5
        783.99f, 659.25f, 587.33f, 659.25f,   // G5 E5 D5 E5
        // Frase B — clímax y regreso al hogar
        783.99f, 987.77f, 1174.7f, 1046.5f,  // G5 B5 D6 C6
        987.77f, 880.00f, 783.99f, 698.46f,  // B5 A5 G5 F5
        659.25f, 783.99f, 880.00f, 783.99f,  // E5 G5 A5 G5
        698.46f, 659.25f, 587.33f, 523.25f,  // F5 E5 D5 C5
    };

    // Ritmo: mezcla de negras (1), corcheas (0.5) y algunos acentos (0.75)
    // El patrón de duración hace que la melodía "baile" en lugar de marchar
    private readonly float[] _gameMelodyDur = new float[]
    {
        0.5f, 0.5f, 0.5f, 0.75f,   // frase A, c1
        0.5f, 0.5f, 0.5f, 0.75f,   // frase A, c2
        0.5f, 0.5f, 0.5f, 0.75f,   // frase A, c3
        0.5f, 0.5f, 0.5f, 0.75f,   // frase A, c4
        0.5f, 0.5f, 0.5f, 0.75f,   // frase B, c1
        0.5f, 0.5f, 0.5f, 0.5f,    // frase B, c2
        0.5f, 0.5f, 0.5f, 0.5f,    // frase B, c3
        0.75f, 0.5f, 0.5f, 1.0f,   // frase B, c4 — nota final larga
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
    // BAJO GAME — walking bass activo, I-V-vi-IV con variación
    // Una nota por beat (4 notas por compás), octavas alternadas
    //
    // Compás I (C):  C2  E2  G2  E2
    // Compás V (G):  G2  B2  D3  B2
    // Compás vi(Am): A1  C2  E2  C2
    // Compás IV(F):  F2  A2  C3  A2
    // =========================================================================

    private readonly float[] _gameBassNotes = new float[]
    {
        65.41f,  82.41f,  98.00f,  82.41f,   // C2 E2 G2 E2
        98.00f,  123.47f, 146.83f, 123.47f,  // G2 B2 D3 B2
        55.00f,  65.41f,  82.41f,  65.41f,   // A1 C2 E2 C2
        87.31f,  110.00f, 130.81f, 110.00f,  // F2 A2 C3 A2
    };
    // Duración de cada nota del bajo en beats
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
    // PAD GAME — 4 voces por acorde (voicing más rico)
    // I=C: C3 E3 G3 B3  |  V=G: G3 B3 D4 F#4
    // vi=Am: A2 C3 E3 G3 | IV=F: F3 A3 C4 E4
    // =========================================================================

    private readonly float[][] _gamePad = new float[][]
    {
        new[] { 130.81f, 164.81f, 196.00f, 246.94f },  // Cmaj7
        new[] { 196.00f, 246.94f, 293.66f, 369.99f },  // Gmaj7 (F#4 ≈ 370)
        new[] { 110.00f, 130.81f, 164.81f, 196.00f },  // Am7
        new[] { 174.61f, 220.00f, 261.63f, 329.63f },  // Fmaj7
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
    private float  _bAttack   = 0.004f;
    private float  _bRelease  = 0.18f;

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
                        drums.masterVolume  = 0.20f;
                        drums.kickStartFreq = 90f;
                        drums.kickEndFreq   = 32f;
                        drums.kickDecay     = 0.14f;
                        drums.hihatDecay    = 0.025f;
                        drums.StartCoroutine(drums.PlayPattern_Basic(bpmGame, 9999));
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

            // ── BAJO — diente de sierra + 2do armónico (gordo) ─────────────────
            float basSample = 0f;
            if (playBass)
            {
                float bTgt  = bOn ? 1f : 0f;
                float alpha = bOn ? bAtkAlpha : bRelAlpha;
                _bEnv += (bTgt - _bEnv) * alpha;

                // Shimmer muy sutil en la frecuencia del bajo
                double bFreqMod = _bFreq * (1.0 + lfo2Val);
                _bPhase += twoPi * bFreqMod / _sr;
                if (_bPhase > twoPi) _bPhase -= twoPi;

                float sawRaw  = (float)((_bPhase / twoPi) * 2.0 - 1.0);
                float sin1    = (float)System.Math.Sin(_bPhase);
                float sin2    = (float)System.Math.Sin(_bPhase * 2) * 0.5f;

                // Mezcla: saw da presencia, senos dan calidez
                basSample = (sawRaw * 0.5f + sin1 * 0.35f + sin2 * 0.15f) * _bEnv * bassVolume;
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