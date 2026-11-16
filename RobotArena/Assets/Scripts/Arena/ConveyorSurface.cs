using System.Collections.Generic;
using RobotSavaslari.Match;
using UnityEngine;

namespace RobotSavaslari.Arena
{
    [DefaultExecutionOrder(120)]
    public sealed class ConveyorSurface : MonoBehaviour
    {
        public const float SurfaceSpeed = .8f;
        private MatchController match;
        private Vector3 direction;
        private static readonly List<ConveyorSurface> surfaces = new List<ConveyorSurface>();
        private static readonly Dictionary<Rigidbody,Vector3> applied = new Dictionary<Rigidbody,Vector3>();
        private static readonly Dictionary<Rigidbody,ConveyorSurface> contacts = new Dictionary<Rigidbody,ConveyorSurface>();
        private static readonly Collider[] overlaps = new Collider[128];
        private static double appliedAt = double.MinValue;
        public Vector3 SurfaceVelocity => direction * SurfaceSpeed;
        public static Vector3 AppliedVelocity(Rigidbody body) => applied.TryGetValue(body,out var value) ? value : Vector3.zero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRegistry() { surfaces.Clear();applied.Clear();contacts.Clear();appliedAt=double.MinValue; }

        public void Configure(MatchController controller,Vector2 heading,Material material)
        {
            match=controller;direction=new Vector3(heading.x,0,heading.y).normalized;
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name="Conveyor Deck";plate.transform.SetParent(transform,false);
            plate.transform.localPosition=Vector3.up*.006f;plate.transform.localScale=new Vector3(1.94f,.01f,1.94f);
            var collider=plate.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            plate.GetComponent<Renderer>().sharedMaterial=material;
            var color=new MaterialPropertyBlock();color.SetColor("_BaseColor",new Color(.08f,.36f,.48f));plate.GetComponent<Renderer>().SetPropertyBlock(color);
            var arrow=new GameObject("Conveyor Direction");arrow.transform.SetParent(transform,false);
            arrow.transform.localPosition=Vector3.up*.02f;arrow.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            AddArrowBar(arrow.transform,new Vector3(0,0,-.15f),new Vector3(.16f,.02f,1),0,material);
            AddArrowBar(arrow.transform,new Vector3(-.18f,0,.2f),new Vector3(.13f,.02f,.55f),40,material);
            AddArrowBar(arrow.transform,new Vector3(.18f,0,.2f),new Vector3(.13f,.02f,.55f),-40,material);
        }
        private static void AddArrowBar(Transform parent,Vector3 position,Vector3 scale,float yaw,Material material)
        {
            var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);bar.name="Direction Mark";bar.transform.SetParent(parent,false);
            bar.transform.localPosition=position;bar.transform.localScale=scale;bar.transform.localRotation=Quaternion.Euler(0,yaw,0);
            bar.GetComponent<Renderer>().sharedMaterial=material;
            var collider=bar.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
        }
        private void OnEnable() { if(!surfaces.Contains(this))surfaces.Add(this); }
        private void OnDisable() { surfaces.Remove(this);RemovePreviousMotion(); }

        public static void RemovePreviousMotion()
        {
            foreach(var pair in applied)
            {
                var body=pair.Key;if(body==null||body.isKinematic)continue;
                Vector3 axis=pair.Value.normalized;
                // A collision may have consumed the surface velocity. Never manufacture a reverse kick.
                float along=Vector3.Dot(body.linearVelocity,axis);
                float remaining=Mathf.Abs(along)<.001f ? 0 : pair.Value.magnitude;
                body.linearVelocity-=axis*remaining;
            }
            applied.Clear();contacts.Clear();
        }

        private void FixedUpdate()
        {
            if(appliedAt==Time.fixedTimeAsDouble)return;
            appliedAt=Time.fixedTimeAsDouble;contacts.Clear();
            foreach(var surface in surfaces)
            {
                if(surface==null||!surface.isActiveAndEnabled||surface.match==null||surface.match.State!=MatchController.MatchState.Playing)continue;
                int count=Physics.OverlapBoxNonAlloc(surface.transform.position+Vector3.up*.3f,new Vector3(.98f,.38f,.98f),overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                // Fail closed if the bounded query is saturated.
                if(count==overlaps.Length)continue;
                for(int i=0;i<count;i++)
                {
                    var collider=overlaps[i];var body=collider.attachedRigidbody;
                    if(body==null||body.isKinematic)continue;
                    var robot=body.GetComponent<RobotPrototypeController>();
                    if(robot==null||robot.CombatStopped||body.GetComponent<RobotHealth>().IsDefeated)continue;
                    // Surface contact, not an airborne robot merely projected over the cell.
                    if(collider.bounds.min.y>surface.transform.position.y+.12f)continue;
                    if(!contacts.TryGetValue(body,out var other) || Distance(body,surface)<Distance(body,other) ||
                        (Mathf.Approximately(Distance(body,surface),Distance(body,other)) && surface.GetInstanceID()<other.GetInstanceID()))contacts[body]=surface;
                }
            }
            foreach(var pair in contacts)
            {
                Vector3 velocity=pair.Value.SurfaceVelocity;
                pair.Key.linearVelocity+=velocity;applied[pair.Key]=velocity;
            }
        }
        private static float Distance(Rigidbody body,ConveyorSurface surface) => (body.position-surface.transform.position).sqrMagnitude;
    }
}
