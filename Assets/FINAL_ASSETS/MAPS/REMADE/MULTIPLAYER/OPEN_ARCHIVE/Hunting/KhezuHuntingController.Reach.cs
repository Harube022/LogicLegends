using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.OpenArchive.Hunting
{
    public sealed partial class KhezuHuntingController
    {
        [Header("Reach onto raised rocks and ledges")]
        [Tooltip("Additional arm reach in monster-local meters, used only for raised targets. Ground pursuit keeps its existing catch distance.")]
        [Range(0, 1.2f)] public float elevatedReachDistance = .65f;
        [Tooltip("World-space feet height above Khezu's ground before upper-body reach is used.")]
        [Min(.25f)] public float elevatedFeetThreshold = .85f;
        readonly List<Transform> upperReachBones = new List<Transform>(4);

        void CacheUpperBodyReach()
        {
            upperReachBones.Clear();
            foreach (var bone in GetComponentsInChildren<Transform>(true))
                if (bone.gameObject.activeInHierarchy && (bone.name == "Head" || bone.name == "Chest" ||
                    bone.name == "UpperArm.L" || bone.name == "UpperArm.R")) upperReachBones.Add(bone);
        }

        // Turning sweeps the existing broad body through a larger footprint than a
        // single oriented-box face. Raised-target reach must account for that sweep,
        // rather than creating narrow safe strips at the edges of its NavMesh clearance.
        float RaisedTargetGap(Target target, Vector3 position, Quaternion rotation)
        {
            var scale = transform.lossyScale;
            var center = Vector3.Scale(body.center, scale);
            var half = Vector3.Scale(body.size, scale) * .5f;
            float x = Mathf.Abs(center.x) + half.x, z = Mathf.Abs(center.z) + half.z;
            float radius = Mathf.Min(agent.radius, Mathf.Sqrt(x * x + z * z));
            Vector3 targetCenter = target.capsule == null ? target.transform.position + Vector3.up : target.transform.TransformPoint(target.capsule.center);
            float targetRadius = target.capsule == null ? .8f : target.capsule.radius * Mathf.Max(Mathf.Abs(target.transform.lossyScale.x), Mathf.Abs(target.transform.lossyScale.z));
            float segment = target.capsule == null ? 1 : Mathf.Max(0, target.capsule.height * Mathf.Abs(target.transform.lossyScale.y) * .5f - targetRadius);
            float horizontal = Mathf.Max(0, new Vector2(targetCenter.x - position.x, targetCenter.z - position.z).magnitude - radius);
            float vertical = Mathf.Max(0, Mathf.Max(targetCenter.y - segment - (position.y + center.y + half.y), (position.y + center.y - half.y) - (targetCenter.y + segment)));
            float gap = Mathf.Max(0, Mathf.Sqrt(horizontal * horizontal + vertical * vertical) - targetRadius);
            if (gap > ElevatedReachLimit()) return gap;
            // The turning footprint is for a runner standing on scenery. An ordinary
            // airborne jump retains the previous oriented-body reach and chase balance.
            float feet = TargetFeet(target);RaycastHit support;
            if (Physics.Raycast(new Vector3(targetCenter.x, feet + .25f, targetCenter.z), Vector3.down, out support, .75f, catchObstructionMask, QueryTriggerInteraction.Ignore) &&
                support.normal.y > .4f && support.point.y - position.y > elevatedFeetThreshold) return gap;
            var local = Quaternion.Inverse(rotation) * (targetCenter - position);
            var nearest = Vector3.Min(center + half, Vector3.Max(center - half, local));
            local.y = Mathf.Clamp(nearest.y, local.y - segment, local.y + segment);
            nearest = Vector3.Min(center + half, Vector3.Max(center - half, local));
            return Mathf.Max(0, Vector3.Distance(local, nearest) - targetRadius);
        }

        float ElevatedReachLimit()
        {
            return catchSurfaceDistance + elevatedReachDistance * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        }

        float ElevatedApproachPenalty(Target target, Vector3 position)
        {
            var toward = target.transform.position - position; toward.y = 0;
            var rotation = toward.sqrMagnitude > .01f ? Quaternion.LookRotation(toward) : transform.rotation;
            if (TargetFeet(target) - position.y > elevatedFeetThreshold && CanReachElevatedFromPose(target, position, rotation)) return 0;
            return 40 + Mathf.Min(40, Mathf.Max(0, RaisedTargetGap(target, position, rotation) - ElevatedReachLimit()) * 6);
        }

        bool CanReachElevatedTarget(Target target)
        {
            var toward = target.transform.position - transform.position; toward.y = 0;
            // Turn toward the runner before beginning the normal capture sequence.
            if (toward.sqrMagnitude > .01f && Vector3.Dot(transform.forward, toward.normalized) < .25f) return false;
            return CanReachElevatedFromPose(target, transform.position, transform.rotation);
        }

        bool CanReachElevatedFromPose(Target target, Vector3 position, Quaternion rotation)
        {
            float feet = TargetFeet(target);
            // Never extend reach to the top of a high column or a distant platform.
            float top = position.y + (body.center.y + body.size.y * .5f) * Mathf.Abs(transform.lossyScale.y);
            if (feet > top || RaisedTargetGap(target, position, rotation) > ElevatedReachLimit()) return false;
            Vector3 center = target.capsule == null ? target.transform.position + Vector3.up : target.transform.TransformPoint(target.capsule.center);
            Vector3 lowerTorso = center;
            lowerTorso.y = Mathf.Lerp(feet, center.y, .55f);
            foreach (var bone in upperReachBones)
            {
                if (bone == null) continue;
                var offset = Quaternion.Inverse(transform.rotation) * (bone.position - transform.position);
                var from = position + rotation * offset;
                if (from.y < position.y + body.bounds.size.y * .35f) continue;
                // A ray starting within masonry can miss its containing collider.
                if (Physics.CheckSphere(from, .08f, catchObstructionMask, QueryTriggerInteraction.Ignore)) continue;
                if (ClearUpperReach(from, center, target.transform) ||
                    ClearUpperReach(from, lowerTorso, target.transform)) return true;
            }
            return false;
        }

        bool ClearUpperReach(Vector3 from, Vector3 to, Transform target)
        {
            RaycastHit obstruction;
            return !Physics.Linecast(from, to, out obstruction, catchObstructionMask, QueryTriggerInteraction.Ignore) ||
                obstruction.transform == target || obstruction.transform.IsChildOf(target);
        }
    }
}
