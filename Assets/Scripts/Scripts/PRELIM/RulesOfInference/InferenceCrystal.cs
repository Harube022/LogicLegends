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
        SphereCollider pickupCollider;
        Rigidbody body;
        Vector3 originalColliderCenter;
        float originalColliderRadius;
        float pillarPickupDrop, pillarPickupRadius;
        bool hasPillarPickup;

        public void ConfigureReachablePickup(float drop, float radius)
        {
            pickupCollider = GetComponent<SphereCollider>();
            if (pickupCollider == null) return;
            if (!hasPillarPickup)
            {
                originalColliderCenter = pickupCollider.center;
                originalColliderRadius = pickupCollider.radius;
            }
            pillarPickupDrop = drop;
            pillarPickupRadius = radius;
            hasPillarPickup = true;
            SetPillarPickupCollider();
        }

        void SetPillarPickupCollider()
        {
            if (!hasPillarPickup || pickupCollider == null) return;
            pickupCollider.center = Vector3.down * pillarPickupDrop;
            pickupCollider.radius = pillarPickupRadius;
            pickupCollider.isTrigger = true;
        }

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
            body = GetComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            SetPillarPickupCollider();
            transform.SetParent(owner, true);
            transform.position = origin;
        }

        void LateUpdate()
        {
            // A released crystal needs its original solid collider to land on the ground.
            // Returning it to a pillar restores the extended, nonblocking pickup trigger.
            if (hasPillarPickup && !IsPlaced && pickupCollider != null)
            {
                if (body == null) body = GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic && pickupCollider.isTrigger)
                {
                    pickupCollider.center = originalColliderCenter;
                    pickupCollider.radius = originalColliderRadius;
                    pickupCollider.isTrigger = false;
                }
            }
            if (label != null && Camera.main != null)
                label.transform.rotation = Camera.main.transform.rotation;
            // A dropped answer must remain recoverable, including if it falls off the island.
            if (!IsPlaced && transform.position.y < origin.y - 12f) ReturnToOrigin();
        }
    }
}
