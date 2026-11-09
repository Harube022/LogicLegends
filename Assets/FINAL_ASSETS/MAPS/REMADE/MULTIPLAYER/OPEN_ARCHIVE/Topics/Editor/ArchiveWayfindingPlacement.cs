using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Measures authored walking surfaces. A ground ray alone does not identify a roadside shoulder.</summary>
    public sealed class ArchiveWayfindingPlacement
    {
        struct Triangle {public Vector2 a,b,c;public Rect bounds;}
        readonly List<Triangle> roads=new List<Triangle>();
        readonly List<Triangle> groundcover=new List<Triangle>();
        readonly List<Bounds> treeBounds=new List<Bounds>();
        public const float SignClearance=4.5f;
        // A topic waymark is 5.1 m wide, including its brass frame.
        public const float TopicSignClearance=3.1f;
        public ArchiveWayfindingPlacement(Transform environment)
        {
            foreach(string group in new[]{"MainPaths","SecondaryPaths"})
            {
                var root=environment.Find(group);if(root==null)continue;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.gameObject.activeInHierarchy||filter.sharedMesh==null||filter.name=="Clearing_Buried_Foundation_Corners")continue;
                    var vertices=filter.sharedMesh.vertices;var indices=filter.sharedMesh.triangles;
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        Vector2 P(int index){var p=filter.transform.TransformPoint(vertices[index]);return new Vector2(p.x,p.z);}
                        var a=P(indices[i]);var b=P(indices[i+1]);var c=P(indices[i+2]);
                        if(Mathf.Abs(Cross(b-a,c-a))<.0001f)continue;
                        var min=Vector2.Min(a,Vector2.Min(b,c));var max=Vector2.Max(a,Vector2.Max(b,c));
                        roads.Add(new Triangle{a=a,b=b,c=c,bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y)});
                    }
                }
            }
            foreach(var filter in environment.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.gameObject.activeInHierarchy||filter.sharedMesh==null||
                    !(filter.name.StartsWith("Vegetation_Cluster_",StringComparison.Ordinal)||filter.name=="Reclaimed_Natural_Boulder_Groups"||filter.name=="Riverbank_RockClusters"))continue;
                var vertices=filter.sharedMesh.vertices;var indices=filter.sharedMesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=filter.transform.TransformPoint(vertices[indices[i]]);var b=filter.transform.TransformPoint(vertices[indices[i+1]]);var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                    if(Mathf.Max(a.y,Mathf.Max(b.y,c.y))<.25f)continue;
                    var p=new Vector2(a.x,a.z);var q=new Vector2(b.x,b.z);var r=new Vector2(c.x,c.z);
                    var min=Vector2.Min(p,Vector2.Min(q,r));var max=Vector2.Max(p,Vector2.Max(q,r));
                    groundcover.Add(new Triangle{a=p,b=q,c=r,bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y)});
                }
            }
            var trees=environment.Find("Trees");
            if(trees!=null)
                foreach(var renderer in trees.GetComponentsInChildren<Renderer>(true))
                    // Batched bushes cover entire sectors in renderer bounds;
                    // they are tested triangle by triangle above instead.
                    if(renderer.gameObject.activeInHierarchy&&renderer.name.StartsWith("Tree_",StringComparison.Ordinal))treeBounds.Add(renderer.bounds);
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float SegmentDistanceSquared(Vector2 point,Vector2 a,Vector2 b)
        {var delta=b-a;float t=Mathf.Clamp01(Vector2.Dot(point-a,delta)/Mathf.Max(.00001f,delta.sqrMagnitude));return (point-a-delta*t).sqrMagnitude;}
        public bool ClearOfRoads(Vector3 position,float radius=SignClearance)
            =>ClearOfTriangles(roads,position,radius);
        public bool ClearOfGroundcover(Vector3 position,float radius)
            =>ClearOfTriangles(groundcover,position,radius);
        static bool ClearOfTriangles(List<Triangle> triangles,Vector3 position,float radius)
        {
            var p=new Vector2(position.x,position.z);float square=radius*radius;
            foreach(var triangle in triangles)
            {
                if(p.x<triangle.bounds.xMin-radius||p.x>triangle.bounds.xMax+radius||p.y<triangle.bounds.yMin-radius||p.y>triangle.bounds.yMax+radius)continue;
                float a=Cross(triangle.b-triangle.a,p-triangle.a),b=Cross(triangle.c-triangle.b,p-triangle.b),c=Cross(triangle.a-triangle.c,p-triangle.c);
                if((a>=0&&b>=0&&c>=0)||(a<=0&&b<=0&&c<=0))return false;
                if(SegmentDistanceSquared(p,triangle.a,triangle.b)<square||SegmentDistanceSquared(p,triangle.b,triangle.c)<square||SegmentDistanceSquared(p,triangle.c,triangle.a)<square)return false;
            }
            return true;
        }
        public static bool Ground(Vector3 candidate,float expectedY,out Vector3 grounded)
        {
            grounded=candidate;
            var hits=Physics.RaycastAll(new Vector3(candidate.x,expectedY+2,candidate.z),Vector3.down,7,~(1<<7),QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.85f&&Mathf.Abs(h.point.y-expectedY)<1).OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)return false;
            grounded.y=hits[0].point.y+.025f;return true;
        }
        public static bool ClearOfProps(Vector3 position,float radius=3.2f)
        {
            foreach(var collider in Physics.OverlapBox(position+Vector3.up*3,new Vector3(radius,2.7f,radius),Quaternion.identity,~(1<<7),QueryTriggerInteraction.Ignore))
                if(collider.enabled&&collider.bounds.max.y>position.y+1.4f)return false;
            return true;
        }
        public Vector3 BesideBridge(Transform bridge)
        {
            var deck=bridge.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="Wooden_Bridge_Deck");
            var matrix=bridge.worldToLocalMatrix*deck.transform.localToWorldMatrix;
            var min=Vector3.one*float.MaxValue;var max=Vector3.one*float.MinValue;
            foreach(var vertex in deck.sharedMesh.vertices){var p=matrix.MultiplyPoint3x4(vertex);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            float end=Mathf.Max(Mathf.Abs(min.z),Mathf.Abs(max.z))*Mathf.Abs(bridge.lossyScale.z);
            float side=Mathf.Max(Mathf.Abs(min.x),Mathf.Abs(max.x))*Mathf.Abs(bridge.lossyScale.x);
            // Search the dry shoulder before the ring junction. Both complete board
            // footprint and clearance stay outside the paving, deck and nearby props.
            foreach(float along in new[]{3f,5f,1f,7f,-1f})
                foreach(float across in new[]{10f,12f,8f,14f,16f})
                    foreach(float sign in new[]{1f,-1f})
                    {
                        var candidate=bridge.position+bridge.forward*(end+along)+bridge.right*((side+across)*sign);
                        if(ClearOfRoads(candidate)&&Ground(candidate,bridge.position.y,out var grounded)&&ClearOfProps(grounded))return grounded;
                    }
            throw new InvalidOperationException("No clear dry shoulder for bridge sign: "+bridge.position);
        }
        public Vector3 BesideTopic(ArchiveTopicStation station)
        {
            var best=Vector3.zero;float bestScore=float.PositiveInfinity;
            // Follow the two shoulders of the approach/ring junction. Do not
            // retreat into the grove when one corner is occupied: try the
            // opposite corner, including rendered foliage without colliders.
            for(float across=7.5f;across<=14;across+=.25f)
                for(float along=12;along<=16.5f;along+=.25f)
                    foreach(float side in new[]{1f,-1f})
                    {
                        var candidate=station.transform.TransformPoint(new Vector3(across*side,0,along));
                        float score=(across-9.5f)*(across-9.5f)+(along-14.75f)*(along-14.75f);
                        if(score>=bestScore||!ClearOfRoads(candidate,TopicSignClearance)||ClearOfRoads(candidate,6))continue;
                        if(!ClearOfGroundcover(candidate,TopicSignClearance)||!ClearOfTrees(candidate,TopicSignClearance))continue;
                        if(!ClearReadingApproach(station,candidate))continue;
                        if(!Ground(candidate,station.transform.position.y,out var grounded)||!ClearOfProps(grounded))continue;
                        best=grounded;bestScore=score;
                    }
            if(!float.IsPositiveInfinity(bestScore))return best;
            throw new InvalidOperationException("No clear dry shoulder for topic sign: T"+(station.topic+1));
        }
        public bool ClearOfTrees(Vector3 position,float radius)
        {
            var footprint=new Bounds(position+Vector3.up*2.4f,new Vector3(radius*2,4.8f,radius*2));
            return !treeBounds.Any(b=>b.Intersects(footprint));
        }
        public bool ClearReadingApproach(ArchiveTopicStation station,Vector3 position)
        {
            // Check the complete sign face from the ring junction, not only
            // its post. Decorative tree canopies often have no collider.
            var reader=station.transform.TransformPoint(new Vector3(0,1.35f,19));
            var forward=position-reader;forward.y=0;forward.Normalize();
            var right=Vector3.Cross(Vector3.up,forward);
            foreach(float side in new[]{-1f,0f,1f})
            {
                var target=position+Vector3.up*3.42f+right*(side*2.5f);
                var delta=target-reader;var ray=new Ray(reader,delta.normalized);
                foreach(var bounds in treeBounds)
                    if(bounds.IntersectRay(ray,out float distance)&&distance<delta.magnitude)return false;
            }
            return true;
        }
    }
}
