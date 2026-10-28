using UnityEngine;

namespace LogicLegends.Graph
{
    public sealed class GraphEdge : MonoBehaviour
    {
        public GraphEdgeData data;
        public Transform bridge;
        public GameObject arrows;
        public Collider reverseGate;
        public Transform artwork;
        public Renderer[] pathLighting;
        public Light[] pathLights;
        public bool Walked { get; private set; }
        public static readonly Color AvailableColor = new Color(1f, .35f, .035f);
        public static readonly Color WalkedColor = new Color(.04f, .42f, 1f);
        private MaterialPropertyBlock lightingProperties;
        public void Configure(GraphEdgeData edge, Vector3 source, Vector3 destination)
        {
            data = edge;
            transform.localScale = GraphGeometry.EdgeScale;
            transform.localPosition = (source + destination) * .5f;
            transform.localRotation = Quaternion.LookRotation(destination - source);
            float length = Vector3.Distance(source, destination);
            float deckLength = Mathf.Max(1, length - 2f * (GraphGeometry.VertexRadius - GraphGeometry.PlatformOverlap));
            bridge.localScale = new Vector3(4.4f, .5f, deckLength / GraphGeometry.EdgeScale.z);
            if (artwork != null) artwork.localScale = new Vector3(1, 1, deckLength / GraphGeometry.EdgeScale.z);
            arrows.SetActive(edge.directed);
            reverseGate.transform.localPosition = new Vector3(0, 2, (length * .5f - GraphGeometry.VertexRadius) / GraphGeometry.EdgeScale.z);
            if (pathLights != null) foreach (var light in pathLights)
                if (light != null) light.range = 3f * GraphGeometry.EdgeScale.x;
            reverseGate.enabled = edge.directed || !edge.traversalEnabled;
            var gateBox=reverseGate as BoxCollider;
            if(gateBox!=null){var size=gateBox.size;size.x=GraphGeometry.BridgeHalfWidth*2/GraphGeometry.EdgeScale.x;gateBox.size=size;}
            AlignWalkSurface();
            if (edge.directed && length > GraphGeometry.VertexRadius * 8) BuildContinuousDeck(deckLength);
            SetWalked(false);
        }
        private void AlignWalkSurface()
        {
            if (artwork == null || transform.parent == null) return;
            var sourceVertex = System.Array.Find(transform.parent.GetComponentsInChildren<GraphVertex>(), v => v.vertexId == data.source);
            Physics.SyncTransforms();
            if (sourceVertex == null || !sourceVertex.TryGetSurfaceHeight(sourceVertex.transform.position, out float floor) ||
                !GraphGeometry.TryGetSurfaceHeight(artwork, transform.position, out float deck)) return;
            // Imported bounds include the tall posts. Align the actual deck,
            // rather than a hidden placeholder or the overall mesh bounds.
            artwork.position += Vector3.up * (floor - deck);
            var arrowPosition = arrows.transform.position; arrowPosition.y = floor + .06f;
            arrows.transform.position = arrowPosition;
            var gatePosition = reverseGate.transform.position;
            gatePosition.y = floor + 2 * transform.lossyScale.y;
            reverseGate.transform.position = gatePosition;
            Physics.SyncTransforms();
        }
        Mesh continuousDeck;
        void BuildContinuousDeck(float length)
        {
            // The source long bridge has a two-unit lowered end step. Keep its rails
            // in their original positions and add a matching level stone landing.
            if (artwork == null || !GraphGeometry.TryGetSurfaceHeight(artwork,transform.position,out float floor)) return;
            RaycastHit surface=default;
            foreach(var collider in artwork.GetComponentsInChildren<Collider>())
                if(collider.Raycast(new Ray(transform.position+Vector3.up*20,Vector3.down),out var hit,40)&&hit.normal.y>.5f)
                    if(surface.collider==null||hit.point.y>surface.point.y)surface=hit;
            var renderer=surface.collider!=null?surface.collider.GetComponent<Renderer>():null;
            if(renderer==null)return;
            var child=new GameObject("Continuous stone landing",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));
            child.transform.SetParent(transform,false);
            float x=GraphGeometry.BridgeHalfWidth*.70f/transform.lossyScale.x,z=length*.5f/transform.lossyScale.z;
            float y=(floor+.012f-transform.position.y)/transform.lossyScale.y;
            continuousDeck=new Mesh{name="Graph level bridge landing"};
            continuousDeck.vertices=new[]{new Vector3(-x,y,-z),new Vector3(-x,y,z),new Vector3(x,y,z),new Vector3(x,y,-z)};
            continuousDeck.triangles=new[]{0,1,2,0,2,3};
            continuousDeck.uv=new[]{surface.textureCoord,surface.textureCoord,surface.textureCoord,surface.textureCoord};
            continuousDeck.RecalculateNormals();continuousDeck.RecalculateBounds();
            child.GetComponent<MeshFilter>().sharedMesh=continuousDeck;
            child.GetComponent<MeshRenderer>().sharedMaterial=renderer.sharedMaterial;
            var box=child.GetComponent<BoxCollider>();box.center=new Vector3(0,y-.125f,0);box.size=new Vector3(x*2,.25f,z*2);
        }
        void OnDestroy()
        {
            if(continuousDeck==null)return;
            if(Application.isPlaying)Destroy(continuousDeck);else DestroyImmediate(continuousDeck);
        }
        public void SetWalked(bool walked)
        {
            Walked = walked;
            Color color = data != null && !data.traversalEnabled ? Color.gray : walked ? WalkedColor : AvailableColor;
            if (lightingProperties == null) lightingProperties = new MaterialPropertyBlock();
            lightingProperties.SetColor("_BaseColor", color);
            lightingProperties.SetColor("_Color", color);
            lightingProperties.SetColor("_EmissionColor", color * 1.5f);
            if (pathLighting != null) foreach (var renderer in pathLighting)
                if (renderer != null) renderer.SetPropertyBlock(lightingProperties);
            if (pathLights != null) foreach (var light in pathLights)
                if (light != null) { light.color = color; light.enabled = data == null || data.traversalEnabled; }
        }
        public void RefreshTraversal(GraphTraversalTracker tracker)
        {
            // Lighting records completed crossings, never a bridge merely entered.
            bool walked = false;
            for (int i = 1; i < tracker.Route.Count && !walked; i++)
                walked = (tracker.Route[i-1] == data.source && tracker.Route[i] == data.destination) ||
                    (!data.directed && tracker.Route[i-1] == data.destination && tracker.Route[i] == data.source);
            if (Walked != walked) SetWalked(walked);
        }
        public void UpdateGate(string currentVertex, Vector3 playerPosition, float playerFootprint)
        {
            if (!data.traversalEnabled) { reverseGate.enabled = true; return; }
            // A gate on another incident bridge must not obstruct arrival at this vertex.
            if (!data.directed || currentVertex != data.destination) { reverseGate.enabled = false; return; }
            // Arrival can be recorded while the capsule still straddles the gate.
            // Wait until it clears the gate before closing behind it, so closing
            // does not push ordinary forward movement off the bridge.
            if (reverseGate.enabled) return;
            var box = reverseGate as BoxCollider;
            float halfDepth = box == null ? .125f : box.size.z * reverseGate.transform.lossyScale.z * .5f;
            float clearance = Vector3.Dot(playerPosition - reverseGate.transform.position, reverseGate.transform.forward);
            reverseGate.enabled = clearance > playerFootprint + halfDepth;
        }
    }
}
