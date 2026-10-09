using System.Collections;
using TMPro;
using UnityEngine;

namespace LogicLegends.Inference
{
    public sealed class ConclusionReveal : MonoBehaviour
    {
        public TMP_Text label;
        public AudioSource successAudio;
        public AudioClip successClip;
        public ParticleSystem successParticles;
        public bool Revealed { get; private set; }
        const string Hidden = "Therefore,\n__________________________";
        string revealedText;

        public void ResetReveal()
        {
            StopAllCoroutines(); Revealed = false; revealedText = null;
            label.text = Hidden; label.color = new Color(0.91f, 0.95f, 0.93f);
            if (successParticles != null) successParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void Reveal(string conclusion)
        {
            if (Revealed) return;
            Revealed = true; revealedText = "Therefore,\n∴ " + conclusion;
            label.text = revealedText;
            if (successAudio != null && successClip != null) successAudio.PlayOneShot(successClip);
            if (successParticles != null) successParticles.Play();
            StartCoroutine(Fade());
        }
        IEnumerator Fade()
        {
            for (float elapsed = 0; elapsed < 0.55f; elapsed += Time.unscaledDeltaTime)
            {
                label.color = new Color(0.55f, 1f, 0.78f, Mathf.Clamp01(elapsed / 0.55f));
                yield return null;
            }
            label.color = new Color(0.55f, 1f, 0.78f);
        }
        void OnDisable()
        {
            StopAllCoroutines();
            if (Revealed && label != null) { label.text = revealedText; label.color = new Color(0.55f, 1f, 0.78f); }
        }
    }
}
