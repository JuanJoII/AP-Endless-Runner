using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// RunnerAudioManager v2 — Audio procedural mejorado para Endless Runner.
//
// MEJORAS vs v1:
//   • Variación procedural de frecuencia ±8% en cada disparo → sonido "vivo"
//   • PlayLooping() para consumables con duración (invencibilidad, imán, x2)
//   • Slide rediseñado: FM con modulación alta + sweep descendente
//   • Moneda premium: arpegio de 5 notas con aceleración
//   • SFX del gato más expresivos (jump con aceleración, death más dramático)
// ═══════════════════════════════════════════════════════════════════════════════

public enum RunnerClip
{
    // Jugador
    Jump, Slide, LaneLeft, LaneRight, Hit, Death,
    // Objetos
    Coin, CoinPremium, PowerUp, ObstacleHit,
    // Objetos con identidad propia
    Object_Barrier, Object_Missile,
    Object_Magnet, Object_Invincible, Object_Multiplier,
    // UI
    UINavigate, UIConfirm, UICancel, GameOver, GameStart,
}

[RequireComponent(typeof(AudioSource))]
public class RunnerAudioManager : MonoBehaviour
{
    public static RunnerAudioManager instance { get; private set; }

    [Header("Pool de SynthSFX (6 instancias)")]
    public SynthSFX[] sfxPool;

    [Header("Pool de looping (3 instancias para consumables)")]
    public SynthSFX[] loopPool;

    [Header("Música de fondo")]
    public RunnerSynthAmbient synthAmbient;

    [Header("Volúmenes")]
    [Range(0f, 1f)] public float sfxVolume    = 1f;
    [Range(0f, 1f)] public float masterVolume = 1f;

    private int   _poolIdx    = 0;
    private float _lastSpeed  = 0f;

    // Coroutine activa de looping para poder cancelarla
    private Coroutine _loopCoroutine = null;
    private SynthSFX  _loopingSFX    = null;

    private System.Random _rng = new System.Random();

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Pool ──────────────────────────────────────────────────────────────────

    private SynthSFX Next()
    {
        SynthSFX sfx = sfxPool[_poolIdx % sfxPool.Length];
        _poolIdx++;
        return sfx;
    }

    // Variación procedural de frecuencia ±range%
    // Hace que cada sonido sea ligeramente diferente → evita monotonía robótica
    private float Vary(float freq, float range = 0.08f)
    {
        float variation = 1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * range;
        return freq * variation;
    }

    // ── API pública ───────────────────────────────────────────────────────────

    public void Play(RunnerClip clip, float volumeScale = 1f)
    {
        if (sfxPool == null || sfxPool.Length == 0) return;
        TriggerSFX(clip, volumeScale * sfxVolume * masterVolume);
    }

    // Reproduce un sonido en loop durante 'duration' segundos
    // Usado por consumables (invencibilidad, imán, multiplicador)
    public void PlayLooping(RunnerClip clip, float duration)
    {
        StopLooping();
        if (loopPool == null || loopPool.Length == 0) return;
        _loopingSFX    = loopPool[0];
        _loopCoroutine = StartCoroutine(LoopSFX(clip, duration));
    }

    public void StopLooping()
    {
        if (_loopCoroutine != null)
        {
            StopCoroutine(_loopCoroutine);
            _loopCoroutine = null;
        }
        if (_loopingSFX != null)
        {
            _loopingSFX.isActive = false;
            _loopingSFX = null;
        }
    }

    private IEnumerator LoopSFX(RunnerClip clip, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            TriggerLooping(clip);
            // Repetir cada 0.8s — el release del sonido cubre el gap
            yield return new WaitForSeconds(0.8f);
            elapsed += 0.8f;
        }
        StopLooping();
    }

    public void StartMenuMusic()
    {
        if (synthAmbient != null) synthAmbient.SetMode(RunnerMusicMode.Menu);
    }

    public void StartGameMusic()
    {
        if (synthAmbient != null) synthAmbient.SetMode(RunnerMusicMode.Game);
    }

    public void StartGameOverMusic()
    {
        if (synthAmbient != null) synthAmbient.SetMode(RunnerMusicMode.GameOver);
    }

    public void UpdateSpeed(float speedRatio)
    {
        _lastSpeed = speedRatio;
        if (synthAmbient != null) synthAmbient.UpdateSpeed(speedRatio);
    }

    // ── Mapa de eventos → síntesis ────────────────────────────────────────────

    private void TriggerSFX(RunnerClip clip, float vol)
    {
        switch (clip)
        {
            // ── SALTO ─────────────────────────────────────────────────────────
            // Aditiva con sweep ascendente: impulso y elevación
            // Variación de ±8% en frecuencia → cada salto suena ligeramente distinto
            // La frecuencia sube con la velocidad del juego (más rápido = más agudo)
            case RunnerClip.Jump:
            {
                float baseFreq = Vary(280f + _lastSpeed * 80f);
                Next().Play(baseFreq, SynthSFX.SynthType.Additive,
                    atk: 0.008f, dec: 0.25f, sus: 0f, rel: 0.20f,
                    vol: 0.55f * vol,
                    sweep: true, sweepEnd: baseFreq * 2.1f, sweepDur: 0.25f);
                break;
            }

            // ── DESLIZAMIENTO ─────────────────────────────────────────────────
            // FM con índice alto + sweep descendente: fricción real, movimiento bajo
            // El índice 4.5 genera ruido casi blanco → textura de roce
            // Sweep de 300→80Hz en 0.4s = sensación de agacharse rápido
            case RunnerClip.Slide:
            {
                float f = Vary(300f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.01f, dec: 0.35f, sus: 0.05f, rel: 0.25f,
                    vol: 0.50f * vol,
                    fmFreq: f * 0.5f, fmIdx: 4.5f,
                    sweep: true, sweepEnd: 80f, sweepDur: 0.35f);
                break;
            }

            // ── CARRIL IZQUIERDA ──────────────────────────────────────────────
            // Seno descendente: movimiento hacia la izquierda = tono baja
            case RunnerClip.LaneLeft:
            {
                float f = Vary(500f);
                Next().Play(f, SynthSFX.SynthType.Sine,
                    atk: 0.005f, dec: 0.10f, sus: 0f, rel: 0.07f,
                    vol: 0.28f * vol,
                    sweep: true, sweepEnd: f * 0.72f, sweepDur: 0.10f);
                break;
            }

            // ── CARRIL DERECHA ────────────────────────────────────────────────
            // Seno ascendente: movimiento hacia la derecha = tono sube
            case RunnerClip.LaneRight:
            {
                float f = Vary(370f);
                Next().Play(f, SynthSFX.SynthType.Sine,
                    atk: 0.005f, dec: 0.10f, sus: 0f, rel: 0.07f,
                    vol: 0.28f * vol,
                    sweep: true, sweepEnd: f * 1.38f, sweepDur: 0.10f);
                break;
            }

            // ── GOLPE ─────────────────────────────────────────────────────────
            // FM con índice muy alto: impacto brusco, inarmónico
            // Dos capas simultáneas: golpe + resonancia
            case RunnerClip.Hit:
            {
                float f = Vary(200f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.003f, dec: 0.30f, sus: 0f, rel: 0.18f,
                    vol: 0.65f * vol,
                    fmFreq: f * 1.7f, fmIdx: 4.0f);
                // Segunda capa: resonancia grave
                Next().Play(Vary(95f), SynthSFX.SynthType.Sine,
                    atk: 0.005f, dec: 0.45f, sus: 0f, rel: 0.30f,
                    vol: 0.35f * vol);
                break;
            }

            // ── MUERTE ────────────────────────────────────────────────────────
            // FM grave descendente largo + arpeggio descendente
            // Transmite colapso total, derrota
            case RunnerClip.Death:
                StartCoroutine(PlayDeathSequence(vol));
                break;

            // ── MONEDA ────────────────────────────────────────────────────────
            // Aditiva brillante, corta
            // Variación alta (±12%) → cada moneda suena diferente, no robótico
            case RunnerClip.Coin:
            {
                float f = Vary(880f, 0.12f);
                Next().Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.004f, dec: 0.12f, sus: 0f, rel: 0.10f,
                    vol: 0.40f * vol);
                break;
            }

            // ── MONEDA PREMIUM ────────────────────────────────────────────────
            // Arpeggio de 5 notas con aceleración = más especial que la moneda normal
            case RunnerClip.CoinPremium:
                StartCoroutine(PlayPremiumArpeggio(vol));
                break;

            // ── POWER-UP ──────────────────────────────────────────────────────
            // FM con sweep ascendente de octava = activación energética
            case RunnerClip.PowerUp:
            {
                float f = Vary(440f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.01f, dec: 0.45f, sus: 0.15f, rel: 0.35f,
                    vol: 0.52f * vol,
                    fmFreq: f * 0.5f, fmIdx: 1.5f,
                    sweep: true, sweepEnd: f * 2f, sweepDur: 0.45f);
                break;
            }

            // ── OBSTÁCULO GOLPEADO ────────────────────────────────────────────
            // FM muy alto + saw: crujido físico de madera/metal
            case RunnerClip.ObstacleHit:
            {
                float f = Vary(130f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.003f, dec: 0.22f, sus: 0f, rel: 0.12f,
                    vol: 0.60f * vol,
                    fmFreq: f * 2.1f, fmIdx: 5.0f);
                break;
            }

            // ── BARRERA ───────────────────────────────────────────────────────
            case RunnerClip.Object_Barrier:
            {
                float f = Vary(110f);
                Next().Play(f, SynthSFX.SynthType.Square,
                    atk: 0.003f, dec: 0.18f, sus: 0f, rel: 0.08f,
                    vol: 0.48f * vol);
                break;
            }

            // ── MISIL ─────────────────────────────────────────────────────────
            // FM con sweep descendente = proyectil que pasa y se aleja
            case RunnerClip.Object_Missile:
            {
                float f = Vary(750f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.008f, dec: 0.38f, sus: 0f, rel: 0.25f,
                    vol: 0.42f * vol,
                    fmFreq: f * 0.5f, fmIdx: 2.0f,
                    sweep: true, sweepEnd: 180f, sweepDur: 0.38f);
                break;
            }

            // ── IMÁN (looping, llamado cada 0.8s) ────────────────────────────
            // Wavetable suave con sweep leve = campo magnético pulsante
            case RunnerClip.Object_Magnet:
            {
                float f = Vary(320f, 0.04f);
                Next().Play(f, SynthSFX.SynthType.Wavetable,
                    atk: 0.08f, dec: 0.5f, sus: 0.35f, rel: 0.6f,
                    vol: 0.30f * vol);
                break;
            }

            // ── INVENCIBILIDAD (looping) ──────────────────────────────────────
            // Aditiva brillante con sweep leve = escudo activo, energía
            case RunnerClip.Object_Invincible:
            {
                float f = Vary(660f, 0.04f);
                Next().Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.05f, dec: 0.4f, sus: 0.3f, rel: 0.55f,
                    vol: 0.38f * vol,
                    sweep: true, sweepEnd: f * 1.15f, sweepDur: 0.4f);
                break;
            }

            // ── MULTIPLICADOR x2 (looping) ────────────────────────────────────
            // FM ligero pulsante = puntuación activa, ritmo de multiplicación
            case RunnerClip.Object_Multiplier:
            {
                float f = Vary(550f, 0.04f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.03f, dec: 0.35f, sus: 0.25f, rel: 0.50f,
                    vol: 0.32f * vol,
                    fmFreq: f * 0.25f, fmIdx: 0.6f);
                break;
            }

            // ── UI ────────────────────────────────────────────────────────────
            case RunnerClip.UINavigate:
                Next().Play(Vary(660f, 0.05f), SynthSFX.SynthType.Sine,
                    atk: 0.004f, dec: 0.06f, sus: 0f, rel: 0.04f,
                    vol: 0.20f * vol);
                break;

            case RunnerClip.UIConfirm:
                Next().Play(Vary(900f, 0.05f), SynthSFX.SynthType.Square,
                    atk: 0.004f, dec: 0.09f, sus: 0f, rel: 0.06f,
                    vol: 0.22f * vol);
                break;

            case RunnerClip.UICancel:
                Next().Play(Vary(420f, 0.05f), SynthSFX.SynthType.Square,
                    atk: 0.004f, dec: 0.09f, sus: 0f, rel: 0.06f,
                    vol: 0.20f * vol);
                break;

            // ── GAME OVER ─────────────────────────────────────────────────────
            case RunnerClip.GameOver:
                Next().Play(320f, SynthSFX.SynthType.FM,
                    atk: 0.015f, dec: 1.1f, sus: 0f, rel: 0.9f,
                    vol: 0.52f * vol,
                    fmFreq: 160f, fmIdx: 1.8f,
                    sweep: true, sweepEnd: 95f, sweepDur: 1.3f);
                break;

            // ── GAME START (GO del countdown) ─────────────────────────────────
            case RunnerClip.GameStart:
            {
                float f = Vary(523f);
                Next().Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.008f, dec: 0.35f, sus: 0.1f, rel: 0.25f,
                    vol: 0.55f * vol,
                    sweep: true, sweepEnd: f * 2f, sweepDur: 0.28f);
                break;
            }
        }
    }

    // ── Sonido looping para consumables ───────────────────────────────────────
    // Se llama desde LoopSFX cada 0.8s — usa el loopPool separado del sfxPool

    private void TriggerLooping(RunnerClip clip)
    {
        if (_loopingSFX == null) return;
        float vol = sfxVolume * masterVolume;

        switch (clip)
        {
            case RunnerClip.Object_Magnet:
            {
                float f = Vary(320f, 0.04f);
                _loopingSFX.Play(f, SynthSFX.SynthType.Wavetable,
                    atk: 0.08f, dec: 0.5f, sus: 0.35f, rel: 0.6f,
                    vol: 0.28f * vol);
                break;
            }
            case RunnerClip.Object_Invincible:
            {
                float f = Vary(660f, 0.04f);
                _loopingSFX.Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.05f, dec: 0.4f, sus: 0.3f, rel: 0.55f,
                    vol: 0.35f * vol,
                    sweep: true, sweepEnd: f * 1.15f, sweepDur: 0.4f);
                break;
            }
            case RunnerClip.Object_Multiplier:
            {
                float f = Vary(550f, 0.04f);
                _loopingSFX.Play(f, SynthSFX.SynthType.FM,
                    atk: 0.03f, dec: 0.35f, sus: 0.25f, rel: 0.50f,
                    vol: 0.30f * vol,
                    fmFreq: f * 0.25f, fmIdx: 0.6f);
                break;
            }
        }
    }

    // ── Secuencias ────────────────────────────────────────────────────────────

    // Muerte: acorde descendente Am → colapso total
    private IEnumerator PlayDeathSequence(float vol)
    {
        // Primera nota: impacto grave
        Next().Play(220f, SynthSFX.SynthType.FM,
            atk: 0.005f, dec: 0.5f, sus: 0f, rel: 0.4f,
            vol: 0.65f * vol,
            fmFreq: 110f, fmIdx: 2.5f,
            sweep: true, sweepEnd: 55f, sweepDur: 0.6f);

        yield return new WaitForSeconds(0.15f);

        // Segunda nota: más grave aún
        Next().Play(165f, SynthSFX.SynthType.FM,
            atk: 0.005f, dec: 0.6f, sus: 0f, rel: 0.5f,
            vol: 0.55f * vol,
            fmFreq: 82f, fmIdx: 2.0f,
            sweep: true, sweepEnd: 40f, sweepDur: 0.7f);

        yield return new WaitForSeconds(0.15f);

        // Tercera nota: colapso final
        Next().Play(110f, SynthSFX.SynthType.Saw,
            atk: 0.005f, dec: 0.8f, sus: 0f, rel: 0.6f,
            vol: 0.50f * vol,
            sweep: true, sweepEnd: 30f, sweepDur: 0.9f);
    }

    // Moneda premium: 5 notas con aceleración + sweep final
    private IEnumerator PlayPremiumArpeggio(float vol)
    {
        float[] freqs   = { 523f, 659f, 784f, 987f, 1047f };
        float[] delays  = { 0.09f, 0.08f, 0.07f, 0.06f, 0f };

        for (int i = 0; i < freqs.Length; i++)
        {
            float f = Vary(freqs[i], 0.04f);
            bool isLast = i == freqs.Length - 1;
            Next().Play(f, SynthSFX.SynthType.Additive,
                atk: 0.004f,
                dec: isLast ? 0.5f : 0.18f,
                sus: isLast ? 0.1f : 0f,
                rel: isLast ? 0.4f : 0.12f,
                vol: (0.45f + i * 0.03f) * vol,
                sweep: isLast, sweepEnd: f * 1.3f, sweepDur: 0.4f);

            if (delays[i] > 0f)
                yield return new WaitForSeconds(delays[i]);
        }
    }
}