using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// RunnerAudioManager v4
//
// CAMBIOS vs v3:
//
// SLIDE:
//   • Usa un slot dedicado (_slideSlot) que persiste mientras el personaje
//     está agachado. PlaySlide() inicia el sonido con holdUntilNoteOff=true.
//     StopSlide() llama NoteOff() para el release limpio.
//   • El sonido es un sweep descendente suave de 600→150Hz (no FM agresivo).
//   • El caller (CharacterInputController o quien maneje el slide) debe llamar
//     PlaySlide() al entrar al slide y StopSlide() al salir.
//
// MONEDAS (huesos de pescado):
//   • Arpeggio ascendente acumulativo: cada recolección consecutiva sube un
//     semitono. Si pasan >0.5s sin recolectar, el tono se resetea.
//   • Dos capas: ping agudo (sine) + subarmónico suave — como moneda metálica.
//
// COLLECTABLES / POWERUPS (imán, invencibilidad, multiplicador):
//   • Ya no usan PlayLooping(). Tienen un slot SynthSFX dedicado (_powerupSlot)
//     que se sostiene con holdUntilNoteOff=true durante toda la duración.
//   • El tono del powerup varía según el tipo (frecuencias distintas).
//   • Un LFO suave se simula alternando dos notas con glide para dar movimiento
//     sin cortes.
//   • StopPowerupSound() llama NoteOff() para el release final.
//
// ═══════════════════════════════════════════════════════════════════════════════

public enum RunnerClip
{
    Jump, Slide, LaneLeft, LaneRight, Hit, Death,
    Coin, CoinPremium, PowerUp, ObstacleHit,
    Object_Barrier, Object_Missile,
    Object_Magnet, Object_Invincible, Object_Multiplier,
    UINavigate, UIConfirm, UICancel, GameOver, GameStart,
    UIPurchase, UIError, Pause,
}

[RequireComponent(typeof(AudioSource))]
public class RunnerAudioManager : MonoBehaviour
{
    public static RunnerAudioManager instance { get; private set; }

    [Header("Pool de SynthSFX (6 instancias para one-shots)")]
    public SynthSFX[] sfxPool;

    [Header("Slot dedicado para el slide (1 instancia)")]
    public SynthSFX slideSlot;

    [Header("Slot dedicado para powerups sostenidos (1 instancia)")]
    public SynthSFX powerupSlot;

    [Header("Música de fondo")]
    public RunnerSynthAmbient synthAmbient;

    [Header("Volúmenes")]
    [Range(0f, 1f)] public float sfxVolume    = 1f;
    [Range(0f, 1f)] public float masterVolume = 1f;

    // ── Estado interno ────────────────────────────────────────────────────────
    private int   _poolIdx   = 0;
    private float _lastSpeed = 0f;

    // Arpeggio acumulativo de monedas
    private int   _coinStep      = 0;   // semitono actual (0..11)
    private float _lastCoinTime  = -1f;
    private const float COIN_RESET_TIME = 0.5f; // tiempo sin recolectar → reset
    // Escala pentatónica menor para que suene musical aunque suba mucho
    // (en lugar de semitono libre, sube por la escala)
    private static readonly float[] s_CoinScale = new float[]
    {
        1.000f,  // C
        1.122f,  // D
        1.260f,  // E
        1.498f,  // G
        1.682f,  // A
        2.000f,  // C octava alta → vuelve a empezar
    };

    private System.Random _rng = new System.Random();

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Pool (solo one-shots) ─────────────────────────────────────────────────

    private SynthSFX Next()
    {
        SynthSFX sfx = sfxPool[_poolIdx % sfxPool.Length];
        _poolIdx++;
        if (sfx.isActive && sfx != slideSlot && sfx != powerupSlot) sfx.ForceStop();
        return sfx;
    }

    private float Vary(float freq, float range = 0.05f)
    {
        return freq * (1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * range);
    }

    // ── API pública ───────────────────────────────────────────────────────────

    public void Play(RunnerClip clip, float volumeScale = 1f)
    {
        if (sfxPool == null || sfxPool.Length == 0) return;
        TriggerSFX(clip, volumeScale * sfxVolume * masterVolume);
    }

    // ── Slide — se llama una sola vez al entrar al slide ──────────────────────

    /// <summary>
    /// Llama esto cuando el personaje empieza a deslizarse.
    /// Sonido de "whoosh" con FM synthesis — aire + fricción metálica.
    /// El sonido se sostiene hasta que llames StopSlide().
    /// </summary>
    public void PlaySlide()
    {
        if (slideSlot == null) return;
        if (slideSlot.isActive) slideSlot.ForceStop();

        float vol = sfxVolume * masterVolume;

        // FM synthesis: carrier square → sweep down to 70Hz (grave de slide)
        // Modulator: 280Hz con index 5.5 → sidebands metálicos/aireados tipo whoosh
        // Attack rápido, sustain largo, release suave
        // Más volumen y decay para asegurar que siempre se escuche completo
        slideSlot.Play(
            freq: 250f,
            type: SynthSFX.SynthType.FM,
            atk:  0.005f,
            dec:  0.40f,
            sus:  0.45f,
            rel:  0.30f,
            vol:  0.65f * vol,
            fmFreq: 280f,
            fmIdx:  5.5f,
            sweep: true,
            sweepEnd: 65f,
            sweepDur: 0.50f,
            holdUntilNoteOff: true
        );
    }

    /// <summary>
    /// Llama esto cuando el personaje termina de deslizarse.
    /// Dispara el release del sonido.
    /// </summary>
    public void StopSlide()
    {
        if (slideSlot != null) slideSlot.NoteOff();
    }

    // ── Powerup sostenido ─────────────────────────────────────────────────────

    /// <summary>
    /// Inicia el sonido de un powerup activo. Se sostiene hasta StopPowerupSound().
    /// </summary>
    public void PlayPowerupSound(RunnerClip clip)
    {
        if (powerupSlot == null) return;
        if (powerupSlot.isActive) powerupSlot.ForceStop();

        float vol = sfxVolume * masterVolume;

        switch (clip)
        {
            // Imán: pulso suave, tono medio-bajo, sweep de octava lento
            case RunnerClip.Object_Magnet:
                powerupSlot.Play(
                    freq: 280f,
                    type: SynthSFX.SynthType.Sine,
                    atk:  0.20f, dec: 0.40f, sus: 0.45f, rel: 0.60f,
                    vol:  0.28f * vol,
                    sweep: true, sweepEnd: 350f, sweepDur: 8f,
                    holdUntilNoteOff: true);
                break;

            // Invencibilidad: brillante, agudo, sweep ascendente amplio
            case RunnerClip.Object_Invincible:
                powerupSlot.Play(
                    freq: 660f,
                    type: SynthSFX.SynthType.Additive,
                    atk:  0.12f, dec: 0.35f, sus: 0.55f, rel: 0.70f,
                    vol:  0.32f * vol,
                    sweep: true, sweepEnd: 880f, sweepDur: 9f,
                    holdUntilNoteOff: true);
                break;

            // Multiplicador x2: FM suave, pulsante, tono medio
            case RunnerClip.Object_Multiplier:
                powerupSlot.Play(
                    freq: 520f,
                    type: SynthSFX.SynthType.FM,
                    atk:  0.10f, dec: 0.30f, sus: 0.40f, rel: 0.65f,
                    vol:  0.28f * vol,
                    fmFreq: 520f * 0.25f, fmIdx: 0.5f,
                    sweep: true, sweepEnd: 600f, sweepDur: 9f,
                    holdUntilNoteOff: true);
                break;
        }
    }

    /// <summary>
    /// Termina el sonido del powerup con release suave.
    /// </summary>
    public void StopPowerupSound()
    {
        if (powerupSlot != null) powerupSlot.NoteOff();
    }

    // Compatibilidad con el sistema anterior de looping (por si acaso)
    public void PlayLooping(RunnerClip clip, float duration)
    {
        PlayPowerupSound(clip);
    }
    public void StopLooping()
    {
        StopPowerupSound();
    }

    public void StartMenuMusic()    { if (synthAmbient != null) synthAmbient.SetMode(RunnerMusicMode.Menu); }
    public void StartGameMusic()    { if (synthAmbient != null) synthAmbient.SetMode(RunnerMusicMode.Game); }
    public void StartGameOverMusic(){ if (synthAmbient != null) synthAmbient.SetMode(RunnerMusicMode.GameOver); }

    public void UpdateSpeed(float speedRatio)
    {
        _lastSpeed = speedRatio;
        if (synthAmbient != null) synthAmbient.UpdateSpeed(speedRatio);
    }

    // ── Mapa de clips → síntesis (one-shots) ─────────────────────────────────

    private void TriggerSFX(RunnerClip clip, float vol)
    {
        switch (clip)
        {
            // ── SALTO ─────────────────────────────────────────────────────────
            case RunnerClip.Jump:
            {
                float f = Vary(400f + _lastSpeed * 60f);
                Next().Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.005f, dec: 0.16f, sus: 0f, rel: 0.12f,
                    vol: 0.46f * vol,
                    sweep: true, sweepEnd: f * 2.2f, sweepDur: 0.18f);
                break;
            }

            // ── SLIDE — redirigido a PlaySlide() ──────────────────────────────
            case RunnerClip.Slide:
                PlaySlide();
                break;

            // ── CARRIL IZQUIERDA ──────────────────────────────────────────────
            case RunnerClip.LaneLeft:
            {
                float f = Vary(500f);
                Next().Play(f, SynthSFX.SynthType.Sine,
                    atk: 0.003f, dec: 0.08f, sus: 0f, rel: 0.05f,
                    vol: 0.24f * vol,
                    sweep: true, sweepEnd: f * 0.75f, sweepDur: 0.08f);
                break;
            }

            // ── CARRIL DERECHA ────────────────────────────────────────────────
            case RunnerClip.LaneRight:
            {
                float f = Vary(380f);
                Next().Play(f, SynthSFX.SynthType.Sine,
                    atk: 0.003f, dec: 0.08f, sus: 0f, rel: 0.05f,
                    vol: 0.24f * vol,
                    sweep: true, sweepEnd: f * 1.33f, sweepDur: 0.08f);
                break;
            }

            // ── GOLPE ─────────────────────────────────────────────────────────
            case RunnerClip.Hit:
            {
                float f = Vary(210f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.002f, dec: 0.26f, sus: 0f, rel: 0.14f,
                    vol: 0.58f * vol,
                    fmFreq: f * 1.8f, fmIdx: 4.2f);
                Next().Play(Vary(95f), SynthSFX.SynthType.Sine,
                    atk: 0.003f, dec: 0.38f, sus: 0f, rel: 0.25f,
                    vol: 0.28f * vol);
                break;
            }

            // ── MUERTE ────────────────────────────────────────────────────────
            case RunnerClip.Death:
                StartCoroutine(PlayDeathSequence(vol));
                break;

            // ── MONEDA ──────────────────────────────────────────────────────────────
            // Sonido simple y corto: ping agudo metálico sin arpeggio
            case RunnerClip.Coin:
            {
                // Hueso de pescado: dos capas
                // Capa 1 — "toc" percusivo grave: FM con índice alto = sonido hueco de hueso
                float bone = Vary(420f, 0.06f);
                Next().Play(bone, SynthSFX.SynthType.FM,
                    atk: 0.002f, dec: 0.08f, sus: 0f, rel: 0.06f,
                    vol: 0.35f * vol,
                    fmFreq: bone * 1.4f, fmIdx: 3.2f);

                // Capa 2 — "pip" agudo: seno corto que le da el toque alegre
                // Sale con un pequeño delay perceptual (0 en código pero llega
                // después porque es la segunda llamada al pool)
                Next().Play(Vary(1400f, 0.04f), SynthSFX.SynthType.Sine,
                    atk: 0.001f, dec: 0.05f, sus: 0f, rel: 0.04f,
                    vol: 0.18f * vol);
                break;
            }

            // ── MONEDA PREMIUM ────────────────────────────────────────────────
            case RunnerClip.CoinPremium:
                StartCoroutine(PlayPremiumArpeggio(vol));
                break;

            // ── POWER-UP (recoger el objeto) ──────────────────────────────────
            case RunnerClip.PowerUp:
            {
                float f = Vary(500f);
                Next().Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.006f, dec: 0.38f, sus: 0.08f, rel: 0.28f,
                    vol: 0.48f * vol,
                    sweep: true, sweepEnd: f * 2.1f, sweepDur: 0.40f);
                break;
            }

            // ── OBSTÁCULO ─────────────────────────────────────────────────────
            case RunnerClip.ObstacleHit:
            {
                float f = Vary(145f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.002f, dec: 0.18f, sus: 0f, rel: 0.10f,
                    vol: 0.52f * vol,
                    fmFreq: f * 2.4f, fmIdx: 5.0f);
                break;
            }

            // ── BARRERA ───────────────────────────────────────────────────────
            case RunnerClip.Object_Barrier:
            {
                float f = Vary(270f);
                Next().Play(f, SynthSFX.SynthType.Square,
                    atk: 0.002f, dec: 0.12f, sus: 0f, rel: 0.07f,
                    vol: 0.40f * vol);
                break;
            }

            // ── MISIL ─────────────────────────────────────────────────────────
            case RunnerClip.Object_Missile:
            {
                float f = Vary(880f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.30f, sus: 0f, rel: 0.18f,
                    vol: 0.38f * vol,
                    fmFreq: f * 0.5f, fmIdx: 2.0f,
                    sweep: true, sweepEnd: 190f, sweepDur: 0.32f);
                break;
            }

            // ── OBJETOS CON SONIDO DE IMPACTO (ONE-SHOTS DE 1s) ──────────────────
            case RunnerClip.Object_Magnet:
            {
                // Pulso eléctrico: FM rápido con vibrato agresivo
                float f = Vary(320f);
                Next().Play(f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.40f, sus: 0f, rel: 0.20f,
                    vol: 0.35f * vol,
                    fmFreq: 50f, fmIdx: 12.0f); // FM agresivo para el chisporroteo
                break;
            }

            case RunnerClip.Object_Invincible:
            {
                // Ráfaga estelar: Sweep ascendente brillante
                float f = 440f;
                Next().Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.01f, dec: 0.60f, sus: 0f, rel: 0.30f,
                    vol: 0.40f * vol,
                    sweep: true, sweepEnd: f * 4f, sweepDur: 0.5f);
                break;
            }

            case RunnerClip.Object_Multiplier:
                StartCoroutine(PlayMultiplierSequence(vol));
                break;

            // ── UI ────────────────────────────────────────────────────────────
            case RunnerClip.UINavigate:
                Next().Play(Vary(680f, 0.04f), SynthSFX.SynthType.Sine,
                    atk: 0.002f, dec: 0.050f, sus: 0f, rel: 0.030f,
                    vol: 0.16f * vol);
                break;

            case RunnerClip.UIConfirm:
            {
                float f = Vary(920f, 0.04f);
                Next().Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.002f, dec: 0.09f, sus: 0f, rel: 0.06f,
                    vol: 0.20f * vol,
                    sweep: true, sweepEnd: f * 1.18f, sweepDur: 0.09f);
                break;
            }

            case RunnerClip.UICancel:
                Next().Play(Vary(390f, 0.04f), SynthSFX.SynthType.Square,
                    atk: 0.002f, dec: 0.07f, sus: 0f, rel: 0.04f,
                    vol: 0.16f * vol);
                break;

            case RunnerClip.UIPurchase:
                StartCoroutine(PlayPurchaseSequence(vol));
                break;

            case RunnerClip.UIError:
                Next().Play(Vary(120f, 0.02f), SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.25f, sus: 0f, rel: 0.15f,
                    vol: 0.25f * vol,
                    fmFreq: 127f, fmIdx: 8.0f);
                break;

            case RunnerClip.Pause:
                Next().Play(Vary(440f, 0.02f), SynthSFX.SynthType.Sine,
                    atk: 0.005f, dec: 0.15f, sus: 0f, rel: 0.10f,
                    vol: 0.20f * vol);
                break;

            // ── GAME OVER ─────────────────────────────────────────────────────
            case RunnerClip.GameOver:
                Next().Play(270f, SynthSFX.SynthType.FM,
                    atk: 0.010f, dec: 0.95f, sus: 0f, rel: 0.75f,
                    vol: 0.48f * vol,
                    fmFreq: 135f, fmIdx: 1.9f,
                    sweep: true, sweepEnd: 75f, sweepDur: 1.1f);
                break;

            // ── GAME START ────────────────────────────────────────────────────
            case RunnerClip.GameStart:
            {
                float f = Vary(540f);
                Next().Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.005f, dec: 0.30f, sus: 0.06f, rel: 0.20f,
                    vol: 0.50f * vol,
                    sweep: true, sweepEnd: f * 2.0f, sweepDur: 0.25f);
                break;
            }
        }
    }

    // ── Secuencias ────────────────────────────────────────────────────────────

    private IEnumerator PlayDeathSequence(float vol)
    {
        Next().Play(230f, SynthSFX.SynthType.FM,
            atk: 0.003f, dec: 0.42f, sus: 0f, rel: 0.32f,
            vol: 0.58f * vol,
            fmFreq: 115f, fmIdx: 2.7f,
            sweep: true, sweepEnd: 55f, sweepDur: 0.52f);

        yield return new WaitForSeconds(0.13f);

        Next().Play(172f, SynthSFX.SynthType.FM,
            atk: 0.003f, dec: 0.52f, sus: 0f, rel: 0.42f,
            vol: 0.48f * vol,
            fmFreq: 86f, fmIdx: 2.1f,
            sweep: true, sweepEnd: 42f, sweepDur: 0.62f);

        yield return new WaitForSeconds(0.13f);

        Next().Play(115f, SynthSFX.SynthType.Saw,
            atk: 0.003f, dec: 0.70f, sus: 0f, rel: 0.52f,
            vol: 0.42f * vol,
            sweep: true, sweepEnd: 28f, sweepDur: 0.82f);
    }

    private IEnumerator PlayPremiumArpeggio(float vol)
    {
        float[] freqs  = { 1046.5f, 1318.5f, 1568.0f, 2093.0f };
        float[] delays = { 0.07f, 0.07f, 0.06f, 0f };

        for (int i = 0; i < freqs.Length; i++)
        {
            float f = Vary(freqs[i], 0.025f);
            bool isLast = i == freqs.Length - 1;
            Next().Play(f, SynthSFX.SynthType.Sine,
                atk: 0.001f,
                dec: isLast ? 0.42f : 0.12f,
                sus: 0f,
                rel: isLast ? 0.32f : 0.09f,
                vol: (0.30f + i * 0.04f) * vol);

            if (delays[i] > 0f)
                yield return new WaitForSeconds(delays[i]);
        }
    }

    private IEnumerator PlayPurchaseSequence(float vol)
    {
        float[] freqs = { 1046.5f, 1318.5f, 1568.0f };
        float[] delays = { 0.08f, 0.08f, 0f };

        for (int i = 0; i < freqs.Length; i++)
        {
            float f = Vary(freqs[i], 0.02f);
            SynthSFX sfx = Next();
            sfx.numberOfHarmonics = 4;
            sfx.harmonicAmplitudes = new float[] { 1f, 0.5f, 0.25f, 0.12f, 0f, 0f, 0f, 0f, 0f, 0f };
            sfx.Play(f, SynthSFX.SynthType.Additive,
                atk: 0.001f,
                dec: i == freqs.Length - 1 ? 0.25f : 0.07f,
                sus: 0f,
                rel: i == freqs.Length - 1 ? 0.20f : 0.06f,
                vol: (0.22f + i * 0.05f) * vol);

            if (delays[i] > 0f)
                yield return new WaitForSeconds(delays[i]);
        }
    }

    private IEnumerator PlayMultiplierSequence(float vol)
    {
        // Arpegio ascendente Do Mayor (Do-Mi-Sol-Do) rápido y brillante
        float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
        float[] delays = { 0.05f, 0.05f, 0.05f, 0f };

        for (int i = 0; i < freqs.Length; i++)
        {
            float f = Vary(freqs[i], 0.015f);
            bool isLast = i == freqs.Length - 1;
            Next().Play(f, SynthSFX.SynthType.Additive,
                atk: 0.005f,
                dec: isLast ? 0.40f : 0.08f,
                sus: 0f,
                rel: isLast ? 0.30f : 0.06f,
                vol: (0.32f + i * 0.04f) * vol);

            if (delays[i] > 0f)
                yield return new WaitForSeconds(delays[i]);
        }
    }
}