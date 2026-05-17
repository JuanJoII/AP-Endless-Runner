using System.Collections;
using UnityEngine;

public class CountdownSound : MonoBehaviour
{
    protected float m_TimeToDisable;
    protected const float k_StartDelay = 0.5f;

    // Frecuencias del conteo: 3, 2, 1, GO
    // 3 y 2 son el mismo tono, 1 es más agudo, GO es el más agudo y con sweep
    private readonly float[] _countFreqs = { 440f, 440f, 587f, 880f };

    void OnEnable()
    {
        // Duración aproximada del conteo: 3 segundos + delay
        m_TimeToDisable = 3.5f;
        StartCoroutine(PlayCountdown());
    }

    void Update()
    {
        m_TimeToDisable -= Time.deltaTime;
        if (m_TimeToDisable < 0)
            gameObject.SetActive(false);
    }

    private IEnumerator PlayCountdown()
    {
        yield return new WaitForSeconds(k_StartDelay);

        if (RunnerAudioManager.instance == null) yield break;

        // Beep 3
        RunnerAudioManager.instance.Play(RunnerClip.UINavigate);
        yield return new WaitForSeconds(1.0f);

        // Beep 2
        RunnerAudioManager.instance.Play(RunnerClip.UINavigate);
        yield return new WaitForSeconds(1.0f);

        // Beep 1 — más agudo
        RunnerAudioManager.instance.Play(RunnerClip.UIConfirm);
        yield return new WaitForSeconds(1.0f);

        // GO — confirmación con sweep ascendente
        RunnerAudioManager.instance.Play(RunnerClip.GameStart);
    }
}