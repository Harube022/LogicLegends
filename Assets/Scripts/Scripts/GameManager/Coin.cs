using UnityEngine;
using Photon.Pun;

public class Coin : MonoBehaviourPun
{
    [Header("Coin Settings")]
    [SerializeField] private int coinValue = 10;
    [SerializeField] private AudioClip collectSound;

    [Header("Animation Settings")]
    [SerializeField] private float rotationSpeed = 100f; // How fast it spins
    [SerializeField] private float floatSpeed = 2f;      // How fast it bobs up and down
    [SerializeField] private float floatAmount = 0.2f;   // How high/low it goes

    private bool isCollected = false;
    private float startY; // Remembers the original height

    private void Start()
    {
        // Record the starting Y position so it knows where to float from
        startY = transform.position.y;
    }

    private void Update()
    {
        // 1. Handle Rotation (Spins around the Y axis)
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // 2. Handle Floating (Smooth up and down movement using a Sine wave)
        float newY = startY + (Mathf.Sin(Time.time * floatSpeed) * floatAmount);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Player player) && !isCollected)
        {
            // ---> NEW: Safe check for BOTH Solo Mode and Multiplayer! <---
            bool isLocalPlayer = false;

            // If we have no network view, or we aren't in a room, we must be in Solo Mode
            if (player.photonView == null || !PhotonNetwork.InRoom)
            {
                isLocalPlayer = true;
            }
            // Otherwise, ask Photon if this character belongs to us
            else if (player.photonView.IsMine)
            {
                isLocalPlayer = true;
            }

            // If it is us, pick up the coin!
            if (isLocalPlayer)
            {
                isCollected = true;

                if (collectSound != null)
                {
                    Play2DSound(collectSound);
                }

                UpdateFirebaseCoins(coinValue);

                if (photonView != null && PhotonNetwork.InRoom)
                {
                    photonView.RPC("DestroyCoinRPC", RpcTarget.AllBuffered);
                }
                else
                {
                    Destroy(gameObject); // Destroys instantly in Solo Mode
                }
            }
        }
    }

    private void Play2DSound(AudioClip clip)
    {
        GameObject audioObj = new GameObject("2D_CoinSound");
        AudioSource source = audioObj.AddComponent<AudioSource>();
        AudioVolumeSettings.Route(source, GameAudioChannel.SoundFX);
        source.clip = clip;
        source.spatialBlend = 0f;
        source.Play();

        Destroy(audioObj, clip.length);
    }

    private void UpdateFirebaseCoins(int amount)
    {
        StageCompleteManager.AwardCoins(amount);
    }

    [PunRPC]
    private void DestroyCoinRPC()
    {
        Destroy(gameObject);
    }
}
