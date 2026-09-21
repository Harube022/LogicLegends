using TMPro;
using UnityEngine;

namespace LogicLegends.Inference
{
    [RequireComponent(typeof(GrabbableObject))]
    public class InferenceCrystal : MonoBehaviour
    {
        public TMP_Text label;
        public string QuestionId { get; private set; }
        public string Answer { get; private set; }
        public bool IsPlaced { get; private set; }
        Vector3 origin;
        Transform owner;
        GrabbableObject grabbable;

        public void Configure(string questionId, string answer, Transform parent)
        {
            QuestionId = questionId; Answer = answer; owner = parent;
            origin = transform.position;
            grabbable = GetComponent<GrabbableObject>();
            label.text = answer;
            ReturnToOrigin();
        }

        public void Place(Transform socket)
        {
            IsPlaced = true;
            label.gameObject.SetActive(false); // The board already shows the placed conclusion.
            grabbable.ConfigureInventoryState(true, socket, false);
            transform.SetPositionAndRotation(socket.position, socket.rotation);
        }

        public void ReturnToOrigin()
        {
            IsPlaced = false;
            label.gameObject.SetActive(true);
            grabbable.ConfigureInventoryState(false, null, false);
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            transform.SetParent(owner, true);
            transform.position = origin;
        }

        void LateUpdate()
        {
            if (label != null && Camera.main != null)
                label.transform.rotation = Camera.main.transform.rotation;
            // A dropped answer must remain recoverable, including if it falls off the island.
            if (!IsPlaced && transform.position.y < origin.y - 12f) ReturnToOrigin();
        }
    }
}
