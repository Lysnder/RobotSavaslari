using RobotSavaslari.Match;
using UnityEngine;

namespace RobotSavaslari.Arena
{
    public enum BarrierPhase { Idle, Warning, Rising, Active, Lowering }

    public sealed class WarningBarrier : MonoBehaviour
    {
        [SerializeField,Min(.1f)] private float idleSeconds=1.2f;
        [SerializeField,Min(.1f)] private float warningSeconds=1.4f;
        [SerializeField,Min(.1f)] private float travelSeconds=.8f;
        [SerializeField,Min(.1f)] private float activeSeconds=1.5f;
        [SerializeField,Min(.1f)] private float occupiedTimeout=2f;
        public BarrierPhase Phase {get;private set;}
        public float Elapsed {get;private set;}
        public int CompletedCycles {get;private set;}
        public int DeferredCycles {get;private set;}
        public float WarningSeconds=>warningSeconds;
        public Collider BarrierCollider=>blockCollider;
        private MatchController match;
        private Rigidbody body;
        private BoxCollider blockCollider;
        private Renderer warning;
        private readonly Collider[] overlaps=new Collider[128];
        private Vector3 lowered,raised;
        private float occupiedSeconds;
        private MaterialPropertyBlock appearance;

        public void Configure(MatchController controller,Material material)
        {
            match=controller;
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name="Moving Barrier";block.transform.SetParent(transform,false);
            block.transform.localScale=new Vector3(1.4f,1.1f,1.4f);
            block.GetComponent<Renderer>().sharedMaterial=material;
            blockCollider=block.GetComponent<BoxCollider>();
            body=block.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            lowered=transform.position-Vector3.up*.60f;
            raised=transform.position+Vector3.up*.55f;
            var indicator=GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name="Barrier Warning";indicator.transform.SetParent(transform,false);
            indicator.transform.localPosition=Vector3.up*.012f;
            indicator.transform.localScale=new Vector3(1.6f,.015f,1.6f);
            var indicatorCollider=indicator.GetComponent<Collider>();indicatorCollider.enabled=false;
            if(Application.isPlaying)Destroy(indicatorCollider);else DestroyImmediate(indicatorCollider);
            warning=indicator.GetComponent<Renderer>();warning.sharedMaterial=material;
            appearance=new MaterialPropertyBlock();ResetCycle();
        }

        public bool IsOccupied()
        {
            int count=Physics.OverlapBoxNonAlloc(transform.position+Vector3.up*.6f,new Vector3(.9f,.7f,.9f),overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return true;
            for(int i=0;i<count;i++) if(overlaps[i].GetComponentInParent<RobotPrototypeController>()!=null)return true;
            return false;
        }

        private void FixedUpdate()
        {
            if(body==null||match==null||match.State!=MatchController.MatchState.Playing||Time.timeScale<=0)return;
            Advance(Time.fixedDeltaTime);
        }

        public void Advance(float deltaTime)
        {
            if(body==null||deltaTime<=0)return;
            Elapsed+=deltaTime;
            switch(Phase)
            {
                case BarrierPhase.Idle: if(Elapsed>=idleSeconds)Enter(BarrierPhase.Warning);break;
                case BarrierPhase.Warning:
                    if(Elapsed<warningSeconds)break;
                    if(IsOccupied())
                    {
                        occupiedSeconds+=deltaTime;
                        if(occupiedSeconds>=occupiedTimeout){DeferredCycles++;Enter(BarrierPhase.Idle);}
                    }
                    else Enter(BarrierPhase.Rising);
                    break;
                case BarrierPhase.Rising:
                    if(IsOccupied()){blockCollider.enabled=false;Enter(BarrierPhase.Lowering);break;}
                    body.MovePosition(Vector3.Lerp(lowered,raised,Mathf.Clamp01(Elapsed/travelSeconds)));
                    if(Elapsed>=travelSeconds)Enter(BarrierPhase.Active);
                    break;
                case BarrierPhase.Active: if(Elapsed>=activeSeconds)Enter(BarrierPhase.Lowering);break;
                case BarrierPhase.Lowering:
                    body.MovePosition(Vector3.MoveTowards(body.position,lowered,(1.15f/travelSeconds)*deltaTime));
                    if(Elapsed>=travelSeconds){CompletedCycles++;Enter(BarrierPhase.Idle);}
                    break;
            }
            if(warning!=null)
            {
                warning.enabled=Phase==BarrierPhase.Warning || Phase==BarrierPhase.Rising;
                appearance.SetColor("_BaseColor",Mathf.Repeat(Time.time*3,1)>.5f?new Color(1,.55f,.02f):new Color(1,.95f,.2f));
                warning.SetPropertyBlock(appearance);
            }
        }

        private void Enter(BarrierPhase phase)
        {
            Phase=phase;Elapsed=0;occupiedSeconds=0;
            if(phase==BarrierPhase.Idle){blockCollider.enabled=false;body.position=lowered;}
            if(phase==BarrierPhase.Rising)blockCollider.enabled=true;
            if(phase==BarrierPhase.Active)body.position=raised;
        }

        public void ResetCycle()
        {
            CompletedCycles=DeferredCycles=0;
            // Match.Prepare can run before the environment rebuilds serialized preview objects.
            if(body==null || blockCollider==null){Phase=BarrierPhase.Idle;Elapsed=occupiedSeconds=0;return;}
            Enter(BarrierPhase.Idle);
            if(warning!=null)warning.enabled=false;
        }
        private void OnDisable(){if(body!=null)ResetCycle();}
    }
}
