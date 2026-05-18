using UnityEngine;

/// <summary>
/// Script auxiliar para invocar sonidos de UI desde eventos de botones en el Inspector.
/// Solo necesitas arrastrar este script a un objeto (o usar el que ya tiene el RunnerAudioManager)
/// y asignar las funciones públicas a los botones.
/// </summary>
public class RunnerUISounds : MonoBehaviour
{
    // Métodos para botones genéricos
    public void PlayClick()    => Play(RunnerClip.UINavigate);
    public void PlayConfirm()  => Play(RunnerClip.UIConfirm);
    public void PlayCancel()   => Play(RunnerClip.UICancel);
    
    // Métodos específicos
    public void PlayPurchase() => Play(RunnerClip.UIPurchase);
    public void PlayError()    => Play(RunnerClip.UIError);
    public void PlayPause()    => Play(RunnerClip.Pause);
    public void PlayGameStart()=> Play(RunnerClip.GameStart);
    
    // Helper interno
    private void Play(RunnerClip clip)
    {
        if (RunnerAudioManager.instance != null)
        {
            RunnerAudioManager.instance.Play(clip);
        }
        else
        {
            Debug.LogWarning("RunnerUISounds: No se encontró RunnerAudioManager.instance");
        }
    }
}
