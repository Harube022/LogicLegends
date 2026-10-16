using TMPro;
using UnityEngine;

namespace LogicLegends.Inference
{
    [RequireComponent(typeof(GrabbableObject))]
    [DefaultExecutionOrder(1500)] // Billboard after the gameplay camera has moved.
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
        Bounds towerBounds;
        bool hasTowerLabel;
        Vector3 originalLabelScale;
        float trappedSince = -1f;
        public Vector3 PickupOrigin => origin;

        public void ConfigureTowerLabel(Transform tower)
        {
            var renderers = tower.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) return;
            towerBounds = renderers[0].bounds;
            foreach (var renderer in renderers) towerBounds.Encapsulate(renderer.bounds);
            hasTowerLabel = true;
            originalLabelScale = label.transform.localScale;
            label.rectTransform.sizeDelta = new Vector2(24f, 5f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        void RestoreSolidCollider()
        {
            if (!hasPillarPickup || pickupCollider == null) return;
            pickupCollider.center = originalColliderCenter; pickupCollider.radius = originalColliderRadius;
            pickupCollider.isTrigger = false;
        }

        // Called synchronously by the existing carry system before gravity is
        // enabled, so an immediate drop cannot begin inside the tower.
        public bool PrepareAccessibleDrop()
        {
            if (!hasPillarPickup) return true;
            RestoreSolidCollider();
            var player = Player.LocalInstance;
            if (player == null) return false;
            float radius = pickupCollider.radius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
            Vector3 forward = player.transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            foreach (float distance in new[] { radius + 2f, radius + 3.5f, radius + 5f })
                foreach (Vector3 direction in new[] { -forward, right, -right, forward, (right - forward).normalized, (-right - forward).normalized })
                {
                    Vector3 point = player.transform.position + direction * distance;
                    var hits = Physics.RaycastAll(point + Vector3.up * 6f, Vector3.down, 24f, ~0, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var hit in hits)
                    {
                        if (hit.collider.GetComponentInParent<Player>() != null || hit.collider.GetComponentInParent<InferenceCrystal>() != null || hit.normal.y < .65f) continue;
                        Vector3 candidate = hit.point + Vector3.up * (radius + .12f) - originalColliderCenter;
                        if (IsEmbedded(candidate, radius * .08f)) break;
                        transform.SetPositionAndRotation(candidate, Quaternion.identity);
                        body = GetComponent<Rigidbody>(); body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        trappedSince = -1f;
                        return true;
                    }
                }
            return false;
        }

        bool IsEmbedded(Vector3 position, float tolerance)
        {
            if (pickupCollider == null) return false;
            float radius = pickupCollider.radius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
            foreach (var other in Physics.OverlapSphere(position + originalColliderCenter, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other.transform.IsChildOf(transform) || other.GetComponentInParent<Player>() != null) continue;
                Vector3 direction; float depth;
                if (Physics.ComputePenetration(pickupCollider, position, Quaternion.identity, other, other.transform.position, other.transform.rotation, out direction, out depth) && depth > tolerance) return true;
            }
            return false;
        }

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
            trappedSince = -1f;
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
                    RestoreSolidCollider();
                }
            }
            if (label != null && Camera.main != null)
            {
                label.transform.rotation = Camera.main.transform.rotation;
                bool found = false; Bounds visual = new Bounds(transform.position, Vector3.zero);
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    if (renderer.GetComponent<TMP_Text>() != null || renderer.GetComponent<TMP_SubMesh>() != null) continue;
                    if (!found) { visual = renderer.bounds; found = true; } else visual.Encapsulate(renderer.bounds);
                }
                if (found) label.transform.position = new Vector3(visual.center.x, visual.max.y + .8f, visual.center.z);
                // Put the name on the camera-facing side of the whole tower's
                // bounds, rather than inside its frame or high above its roof.
                if (hasTowerLabel && !IsPlaced && grabbable != null && !grabbable.IsHeld &&
                    (transform.position - origin).sqrMagnitude < .01f)
                {
                    Vector3 towardCamera = Camera.main.transform.position - towerBounds.center;
                    towardCamera.y = 0f;
                    if (towardCamera.sqrMagnitude < .01f) towardCamera = Vector3.back;
                    towardCamera.Normalize();
                    float edge = Mathf.Abs(towardCamera.x) * towerBounds.extents.x + Mathf.Abs(towardCamera.z) * towerBounds.extents.z;
                    Vector3 position = towerBounds.center + towardCamera * (edge + 3f);
                    position.y = visual.max.y + 1.5f;
                    label.transform.position = position;
                    float distance = Vector3.Distance(position, Camera.main.transform.position);
                    label.transform.localScale = originalLabelScale * Mathf.Clamp(distance * .045f, .3f, 1f);
                    label.transform.rotation = distance > .01f
                        ? Quaternion.LookRotation(position - Camera.main.transform.position, Vector3.up)
                        : Camera.main.transform.rotation;
                }
                else if (hasTowerLabel) label.transform.localScale = originalLabelScale;
            }
            // A dropped answer must remain recoverable, including if it falls off the island.
            if (!IsPlaced && transform.position.y < origin.y - 12f) ReturnToOrigin();
            if (hasPillarPickup && !IsPlaced && grabbable != null && !grabbable.IsHeld && body != null && !body.isKinematic)
            {
                bool trapped = IsEmbedded(transform.position, originalColliderRadius * .3f);
                if (!trapped) trappedSince = -1f;
                else if (trappedSince < 0f) trappedSince = Time.unscaledTime;
                else if (Time.unscaledTime - trappedSince > .7f) ReturnToOrigin();
            }
        }
    }
}
