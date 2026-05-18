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
        if (sfx.isActive) sfx.ForceStop();
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
    /// El sonido se sostiene hasta que llames StopSlide().
    /// </summary>
    public void PlaySlide()
    {
        if (slideSlot == null) return;
        if (slideSlot.isActive) slideSlot.ForceStop();

        float vol = sfxVolume * masterVolume;

        // Sweep de 550Hz → 140Hz en 0.35s: fricción que baja al agacharse
        // Sine suave — no FM agresivo, no duele los oídos
        // holdUntilNoteOff=true → se queda en sustain hasta StopSlide()
        slideSlot.Play(
            freq: 550f,
            type: SynthSFX.SynthType.Sine,
            atk:  0.015f,
            dec:  0.30f,
            sus:  0.08f,   // sustain bajo pero audible
            rel:  0.25f,
            vol:  0.35f * vol,
            sweep: true,
            sweepEnd: 140f,
            sweepDur: 0.35f,
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
            // El slide ya no se dispara desde Play(RunnerClip.Slide).
            // El CharacterInputController debe llamar PlaySlide()/StopSlide().
            // Este case queda como fallback por si algo llama Play(Slide).
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

            // ── MONEDA / HUESO DE PESCADO ─────────────────────────────────────
            // Arpeggio acumulativo: sube por la escala pentatónica con cada
            // recolección consecutiva. Se resetea si pasan más de 0.5s.
            case RunnerClip.Coin:
            {
                // Resetear si pasó demasiado tiempo desde la última moneda
                if (Time.time - _lastCoinTime > COIN_RESET_TIME)
                    _coinStep = 0;
                _lastCoinTime = Time.time;

                float baseFreq = 1100f; // C6 como raíz
                float ratio    = s_CoinScale[_coinStep % s_CoinScale.Length];
                // Si llegamos al final de la escala, duplicar la octava base
                int octave     = _coinStep / s_CoinScale.Length;
                float freq     = baseFreq * ratio * Mathf.Pow(2f, octave);
                freq = Mathf.Clamp(freq, 800f, 3200f); // no subir infinito

                // Capa principal: ping agudo
                Next().Play(freq, SynthSFX.SynthType.Sine,
                    atk: 0.001f, dec: 0.07f, sus: 0f, rel: 0.10f,
                    vol: 0.36f * vol);
                // Subarmónico suave para dar cuerpo
                Next().Play(freq * 0.5f, SynthSFX.SynthType.Sine,
                    atk: 0.001f, dec: 0.05f, sus: 0f, rel: 0.07f,
                    vol: 0.14f * vol);

                _coinStep++;
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

            // ── OBJETOS CON SONIDO SOSTENIDO ──────────────────────────────────
            // Estos se manejan con PlayPowerupSound() / StopPowerupSound().
            // Pero si alguien llama Play() directamente, lo redirigimos.
            case RunnerClip.Object_Magnet:
            case RunnerClip.Object_Invincible:
            case RunnerClip.Object_Multiplier:
                PlayPowerupSound(clip);
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
        // C6 E6 G6 C7 — arpegio de Do mayor octava alta, notas claras
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
}