using TMPro;
using UnityEngine;

namespace LogicLegends.Graph
{
    public sealed class GraphVertex : MonoBehaviour
    {
        public string vertexId;
        public Collider arrivalVolume;
        public TMP_Text label;
        [Tooltip("Graph-local copy of Area_circles_blue; inactive outside answer feedback.")]
        public GameObject answerArea;
        private ParticleSystem[] answerParticles;
        private float feedbackUntil;
        public bool TryGetSurfaceHeight(Vector3 position, out float height)
        {
            return GraphGeometry.TryGetSurfaceHeight(transform.Find("Bilog artwork"), position, out height);
        }
        public void Configure(GraphVertexData data)
        {
            vertexId = data.id;
            transform.localScale = Vector3.one * GraphGeometry.VertexScale;
            transform.localPosition = data.position;
            if (label != null) label.text = data.label;
            HideAnswerFeedback();
        }
        public void ShowAnswerFeedback(bool correct, float seconds)
        {
            if (answerArea == null) return;
            HideAnswerFeedback();
            if (answerParticles == null) answerParticles = answerArea.GetComponentsInChildren<ParticleSystem>(true);
            var color = correct ? new Color(.15f, 1f, .25f, .65f) : new Color(1f, .12f, .08f, .65f);
            foreach (var particles in answerParticles)
            {
                // These overrides apply only to this vertex's nested effect instance.
                if (particles.name == "points_large" || particles.name == "points_small")
                { particles.gameObject.SetActive(false); continue; }
                var main = particles.main;
                main.playOnAwake = false;
                main.startColor = color;
            }
            feedbackUntil = Time.time + seconds;
            answerArea.SetActive(true);
            foreach (var particles in answerParticles)
                if (particles.gameObject.activeInHierarchy) particles.Play(false);
        }
        public void HideAnswerFeedback()
        {
            if (answerArea == null) return;
            if (answerParticles == null) answerParticles = answerArea.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particles in answerParticles)
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            answerArea.SetActive(false);
        }
        private void Update()
        {
            if (answerArea != null && answerArea.activeSelf && Time.time >= feedbackUntil)
                HideAnswerFeedback();
        }
        private void OnDisable() => HideAnswerFeedback();
    }
}
