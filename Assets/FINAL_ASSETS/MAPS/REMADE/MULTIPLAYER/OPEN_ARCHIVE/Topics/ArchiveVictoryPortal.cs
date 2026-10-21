using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Presentation and aperture geometry only. ArchiveTopicSession owns every escape and win.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveVictoryPortal : MonoBehaviour
    {
        public ArchiveTopicSession session;
        [Header("Aperture (world-sized when root scale is one)")]
        [Min(2)] public float radius = 11;
        [Min(4)] public float height = 26;
        [Min(.5f)] public float entryDepth = 2.75f;
        [Min(.2f)] public float formationSeconds = 2.2f;
        [Header("Appearance")]
        [ColorUsage(true,true)] public Color energyColor = new Color(.08f,.95f,.9f,1);
        [ColorUsage(true,true)] public Color runeColor = new Color(1.3f,.74f,.16f,1);
        [Range(.1f,3)] public float glow = 1;
        [Min(0)] public float rotationSpeed = 7;
        public Transform visuals, outerRing, innerRing, runeOrbit, groundSeal;
        public Renderer membrane;
        public Renderer[] energyRenderers, runeRenderers;
        public ParticleSystem motes;
        public AudioSource openingSound;
        public BoxCollider aperture;
        MaterialPropertyBlock properties;
        bool wasOpen;
        public float Formation => session == null || !session.PortalOpened ? 0 :
            Mathf.Clamp01((float)(ArchiveTopicSession.Clock-session.State.exit.openedAt)/Mathf.Max(.2f,formationSeconds));
        public bool Formed => isActiveAndEnabled && Formation >= 1;
        public Vector3 Center => transform.TransformPoint(Vector3.up*height*.5f);

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (visuals != null) visuals.gameObject.SetActive(false);
            if (groundSeal != null) groundSeal.gameObject.SetActive(false);
            if (aperture != null) aperture.enabled = false;
        }
        void Update()
        {
            if (session == null) session = ArchiveTopicSession.Current;
            bool open = session != null && session.PortalOpened;
            if (open && !wasOpen && Formation < 1 && openingSound != null) openingSound.Play();
            if (motes != null && open != wasOpen)
            {
                if (open) motes.Play(); else motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            wasOpen = open;
            if (visuals != null) visuals.gameObject.SetActive(open);
            if (groundSeal != null) groundSeal.gameObject.SetActive(open);
            if (aperture != null) aperture.enabled = Formed && !session.TeamWon;
            if (!open) return;
            float formation = Mathf.SmoothStep(0,1,Formation);
            if (visuals != null)
            {
                visuals.localPosition = Vector3.up*(height*.5f*formation);
                visuals.localScale = new Vector3(radius*formation,height*.5f*formation,1);
            }
            float angle = (float)(ArchiveTopicSession.Clock % 3600)*rotationSpeed;
            if (outerRing != null) outerRing.localRotation = Quaternion.Euler(0,0,angle);
            if (innerRing != null) innerRing.localRotation = Quaternion.Euler(0,0,-angle*.67f);
            if (runeOrbit != null) runeOrbit.localRotation = Quaternion.Euler(0,0,-angle*.28f);
            if (groundSeal != null)
            {
                groundSeal.localRotation = Quaternion.Euler(90,0,-angle*.7f);
                groundSeal.localScale = Vector3.one*(radius*formation);
            }
            Apply(membrane,energyColor,formation);
            if (energyRenderers != null) foreach (var r in energyRenderers) Apply(r,energyColor,formation);
            if (runeRenderers != null) foreach (var r in runeRenderers) Apply(r,runeColor,formation);
        }
        void Apply(Renderer renderer, Color color, float formation)
        {
            if (renderer == null) return;
            properties.Clear(); properties.SetColor("_Tint",color); properties.SetFloat("_Glow",glow);
            properties.SetFloat("_Formation",formation); renderer.SetPropertyBlock(properties);
        }
        public bool ContainsPlayer(Player player)
        {
            if (!Formed || player == null || !player.gameObject.activeInHierarchy) return false;
            var capsule = player.GetComponent<CharacterController>();
            Vector3 center = capsule == null ? player.transform.position+Vector3.up : player.transform.TransformPoint(capsule.center);
            Vector3 local = transform.InverseTransformPoint(center);
            float padding = capsule == null ? .5f : capsule.radius*Mathf.Max(Mathf.Abs(player.transform.lossyScale.x),Mathf.Abs(player.transform.lossyScale.z))/Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.x));
            float x = local.x/Mathf.Max(.5f,radius-padding), y=(local.y-height*.5f)/Mathf.Max(1,height*.5f-padding);
            return Mathf.Abs(local.z) <= entryDepth+padding && x*x+y*y <= 1;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.2f,1,1,.5f);
            var previous=Gizmos.matrix;Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up*height*.5f,new Vector3(radius*2,height,entryDepth*2));Gizmos.matrix=previous;
        }
    }
}
