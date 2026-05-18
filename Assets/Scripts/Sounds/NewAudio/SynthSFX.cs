using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// SynthSFX v3
//
// CAMBIOS vs v2:
//   • NoteOff(): dispara el release desde afuera — para slide y powerups sostenidos
//   • ForceStop(): silencio inmediato (sin release) — para reusar slots del pool
//   • El sustain ahora se sostiene indefinidamente hasta que llegue NoteOff()
//     o ForceStop(). Antes se cortaba solo cuando sus<=0.001, ahora el caller decide.
// ═══════════════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(AudioSource))]
public class SynthSFX : MonoBehaviour
{
    public enum SynthType { Sine, Square, Saw, Additive, FM, Wavetable }

    [Header("Debug — valores actuales")]
    public float     frequency = 440f;
    public SynthType synthType = SynthType.Sine;
    public float attack  = 0.01f;
    public float decay   = 0.25f;
    public float sustain = 0f;
    public float release = 0.2f;
    public float volume  = 0.4f;

    [Header("FM")]
    public float fmModFrequency = 220f;
    public float fmModIndex     = 1f;

    [Header("Aditiva")]
    public int     numberOfHarmonics  = 5;
    public float[] harmonicAmplitudes = new float[10]
        { 1f, 0.5f, 0.25f, 0.12f, 0.06f, 0f, 0f, 0f, 0f, 0f };

    [Header("Sweep")]
    public bool  usePitchSweep = false;
    public float sweepEndFreq  = 0f;
    public float sweepDuration = 0.4f;

    [Header("Estado")]
    public bool isActive = false;

    // ── Internos ──────────────────────────────────────────────────────────────
    private float       _sr;
    private AudioSource _aud;

    private volatile bool _pendingReset  = false;
    private volatile bool _pendingStop   = false;
    private volatile bool _pendingNoteOff = false;

    private float     _pFreq, _pAtk, _pDec, _pSus, _pRel, _pVol;
    private float     _pFmFreq, _pFmIdx, _pSweepEnd, _pSweepDur;
    private bool      _pSweep;
    private SynthType _pType;
    private float[]   _pHarm  = new float[10];
    private int       _pNHarm = 5;

    private int   _t        = 0;
    private bool  _offSent  = false;
    private int   _offIdx   = 0;
    private float _envVal   = 0f;
    // Cuando es true, el sustain se sostiene hasta recibir NoteOff/ForceStop
    private bool  _holdSustain = false;

    private float[] _wt;
    private const int WTS = 2048;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _sr  = AudioSettings.outputSampleRate;
        _aud = GetComponent<AudioSource>();
        _aud.clip         = AudioClip.Create("sfx_loop", (int)_sr, 1, (int)_sr, false);
        _aud.loop         = true;
        _aud.playOnAwake  = false;
        _aud.spatialBlend = 0f;
        _aud.volume       = 1f;
        _aud.enabled      = true;
        _aud.Play();
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispara el sonido. Si holdUntilNoteOff=true, el sustain se mantiene
    /// indefinidamente hasta llamar NoteOff() o ForceStop().
    /// </summary>
    public void Play(
        float freq, SynthType type,
        float atk, float dec, float sus, float rel,
        float vol           = 0.4f,
        float fmFreq        = 220f,
        float fmIdx         = 1f,
        bool  sweep         = false,
        float sweepEnd      = 0f,
        float sweepDur      = 0.4f,
        bool  holdUntilNoteOff = false)
    {
        _pFreq    = freq;   _pType    = type;
        _pAtk     = atk;    _pDec     = dec;
        _pSus     = sus;    _pRel     = rel;
        _pVol     = vol;
        _pFmFreq  = fmFreq; _pFmIdx   = fmIdx;
        _pSweep   = sweep;  _pSweepEnd = sweepEnd; _pSweepDur = sweepDur;
        _pNHarm   = numberOfHarmonics;
        System.Array.Copy(harmonicAmplitudes, _pHarm, 10);
        _holdSustain = holdUntilNoteOff;

        if (type == SynthType.Wavetable) BuildWT();
        if (!_aud.isPlaying) _aud.Play();

        isActive        = true;
        _pendingStop    = false;
        _pendingNoteOff = false;
        _pendingReset   = true;
    }

    /// <summary>
    /// Dispara el release del sonido sostenido. Llamar cuando termina la acción
    /// (ej: el personaje deja de deslizarse, o termina el powerup).
    /// </summary>
    public void NoteOff()
    {
        if (isActive) _pendingNoteOff = true;
    }

    /// <summary>
    /// Silencio inmediato sin release. Para reusar el slot del pool.
    /// </summary>
    public void ForceStop()
    {
        isActive      = false;
        _pendingStop  = true;
        _holdSustain  = false;
    }

    // ── Wavetable ─────────────────────────────────────────────────────────────

    private void BuildWT()
    {
        _wt = new float[WTS];
        for (int i = 0; i < WTS; i++)
        {
            float s = 0f, tot = 0f;
            for (int h = 0; h < numberOfHarmonics; h++)
            {
                s   += harmonicAmplitudes[h] * Mathf.Sin(2f * Mathf.PI * (h+1) * i / WTS);
                tot += harmonicAmplitudes[h];
            }
            _wt[i] = tot > 0f ? s / tot : 0f;
        }
    }

    // ── Síntesis ──────────────────────────────────────────────────────────────

    private float Sine   (float f, int t) => Mathf.Sin(2f * Mathf.PI * f * t / _sr);
    private float Square (float f, int t) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t / _sr));
    private float Saw    (float f, int t) { float T = _sr/f; return Mathf.Lerp(1f,-1f,(t%T)/T); }

    private float Additive(float f, int t)
    {
        float s = 0f, tot = 0f;
        for (int h = 0; h < _pNHarm; h++)
        {
            float hf = f * (h+1);
            if (hf >= _sr * 0.5f) break;
            s   += _pHarm[h] * Mathf.Sin(2f * Mathf.PI * hf * t / _sr);
            tot += _pHarm[h];
        }
        return tot > 0f ? s / tot : 0f;
    }

    private float FM(float carrier, int t)
    {
        float cp = 2f * Mathf.PI * carrier  * t / _sr;
        float mp = 2f * Mathf.PI * _pFmFreq * t / _sr;
        return Mathf.Sin(cp + _pFmIdx * Mathf.Sin(mp));
    }

    private float SampleWT(int t)
    {
        if (_wt == null) return 0f;
        int idx = Mathf.Abs(Mathf.RoundToInt((t * _pFreq / _sr) * WTS)) % WTS;
        return _wt[idx];
    }

    private float CurFreq()
    {
        if (!_pSweep) return _pFreq;
        return Mathf.Lerp(_pFreq, _pSweepEnd, Mathf.Clamp01((_t / _sr) / _pSweepDur));
    }

    // ── ADSR ─────────────────────────────────────────────────────────────────

    private float ADSR()
    {
        float atkS = _pAtk * _sr;
        float decS = _pDec * _sr;
        float relS = _pRel * _sr;
        float env  = 0f;

        if (!_offSent)
        {
            if (_t < atkS)
            {
                env = _t / atkS;
            }
            else if (_t < atkS + decS)
            {
                env = Mathf.Lerp(1f, _pSus, (_t - atkS) / decS);
            }
            else
            {
                env = _pSus;
                // Si _holdSustain=true, se queda aquí hasta NoteOff/ForceStop.
                // Si _holdSustain=false y sus=0, entra al release automáticamente.
                if (!_holdSustain && _pSus <= 0.001f)
                {
                    _offSent = true;
                    _offIdx  = _t;
                    _envVal  = 0f;
                }
            }
        }
        else
        {
            int el = _t - _offIdx;
            env = el < relS ? Mathf.Lerp(_envVal, 0f, el / relS) : 0f;
            if (el >= relS) isActive = false;
        }

        _envVal = env;
        return env;
    }

    // ── OnAudioFilterRead ─────────────────────────────────────────────────────

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (_pendingStop)
        {
            for (int i = 0; i < data.Length; i++) data[i] = 0f;
            _pendingStop = false;
            return;
        }

        if (_pendingReset)
        {
            _t              = 0;
            _offSent        = false;
            _offIdx         = 0;
            _envVal         = 0f;
            _pendingReset   = false;
            _pendingNoteOff = false;
        }

        // Aplicar NoteOff si llegó desde el hilo principal
        if (_pendingNoteOff && !_offSent)
        {
            _offSent        = true;
            _offIdx         = _t;
            _envVal         = _envVal > 0f ? _envVal : _pSus;
            _pendingNoteOff = false;
            _holdSustain    = false;
        }

        for (int i = 0; i < data.Length; i += channels)
        {
            float s = 0f;
            if (isActive)
            {
                float f   = CurFreq();
                float raw = 0f;
                switch (_pType)
                {
                    case SynthType.Sine:      raw = Sine    (f, _t); break;
                    case SynthType.Square:    raw = Square  (f, _t); break;
                    case SynthType.Saw:       raw = Saw     (f, _t); break;
                    case SynthType.Additive:  raw = Additive(f, _t); break;
                    case SynthType.FM:        raw = FM      (f, _t); break;
                    case SynthType.Wavetable: raw = SampleWT(_t);    break;
                }
                s = raw * ADSR() * _pVol;
                _t++;
            }
            data[i] = s;
            if (channels == 2) data[i+1] = s;
        }
    }
}