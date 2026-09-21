using UnityEngine;

public class EarthquakeTrigger : MonoBehaviour
{
    public CameraShake cameraShake;

    public void TriggerShake()
    {
        cameraShake.Shake(20f, 2f);
    }
}