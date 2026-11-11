using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static partial class OpeningRemakeV2
{
    [Serializable] sealed class PillarRingPose
    {
        public string path;
        public Vector3 position,center;
        public Quaternion rotation;
        public float degreesPerSecond;
    }
    [Serializable] sealed class PillarRingPoses { public PillarRingPose[] rings; }
    static PillarRingPose[] pillarRingRest;
    static Transform[] pillarSymbolRings;
    const float PillarRingBrakeSeconds=.9f;

    static void BindPillarSymbolRings()
    {
        var sanctuary=film.GetRootGameObjects().Single(r=>r.name=="Sanctuary").transform;
        string path=Output+"/EditSource/pillar-symbol-ring-rest.json";
        if(!File.Exists(path))
        {
            var poses=Enumerable.Range(1,10).Select(i=>
            {
                var ring=sanctuary.Find("Text."+i.ToString("D3"));
                if(ring==null)throw new Exception("Missing original Pillar symbol ring "+i);
                // The imported meshes have their origin below the circle. Keep
                // their geometric center fixed instead of spinning that origin.
                int band=i==1?0:i==2?-1:(i-3)%3;
                return new PillarRingPose{path=AnimationUtility.CalculateTransformPath(ring,sanctuary),position=ring.position,
                    center=ring.GetComponent<Renderer>().bounds.center,rotation=ring.rotation,
                    degreesPerSecond=band<0?5.5f:band==0?7:band==1?-10:14};
            }).ToArray();
            File.WriteAllText(path,JsonUtility.ToJson(new PillarRingPoses{rings=poses},true));
        }
        pillarRingRest=JsonUtility.FromJson<PillarRingPoses>(File.ReadAllText(path)).rings;
        pillarSymbolRings=pillarRingRest.Select(p=>sanctuary.Find(p.path)).ToArray();
        if(pillarSymbolRings.Any(t=>t==null))throw new Exception("Pillar symbol ring rest paths no longer match the review scene.");
    }

    // Integrated smooth braking: angular velocity reaches exactly zero at the
    // first crack. Absolute film time makes seeking and crossfades repeatable.
    public static float PillarSymbolOrbitClock(float time)
    {
        float stop=timeline.firstCrack,start=Mathf.Max(0,stop-PillarRingBrakeSeconds),duration=stop-start;
        if(time<=start)return Mathf.Max(0,time);
        float u=Mathf.Clamp01((time-start)/duration);
        return start+duration*(u-u*u*u+.5f*u*u*u*u);
    }
    static void EvaluatePillarSymbolRings(float time)
    {
        float clock=PillarSymbolOrbitClock(time);
        for(int i=0;i<pillarSymbolRings.Length;i++)
        {
            var rest=pillarRingRest[i];var spin=Quaternion.AngleAxis(clock*rest.degreesPerSecond,rest.rotation*Vector3.forward);
            pillarSymbolRings[i].SetPositionAndRotation(rest.center+spin*(rest.position-rest.center),spin*rest.rotation);
        }
    }
}
