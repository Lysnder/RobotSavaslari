using System.Collections.Generic;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Arena
{
    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(ArenaBounds))]
    [DisallowMultipleComponent]
    public sealed class ArenaEnvironment : MonoBehaviour
    {
        [SerializeField] private ArenaLayout[] layouts;
        [SerializeField] private int selectedLayout;
        [SerializeField] private MatchController match;
        [SerializeField] private Transform playerSpawn,opponentSpawn;
        [SerializeField] private Material obstacleMaterial;
        private RobotHealth playerHealth,opponentHealth;
        public ArenaLayout Layout => layouts!=null && layouts.Length>selectedLayout ? layouts[selectedLayout] : null;
        public IReadOnlyList<ArenaLayout> Layouts => layouts;
        public MatchController Match => match;
        public Transform Contents => transform.Find("Layout Contents");
        public float ClearanceRadius => Mathf.Max(ArenaLayoutValidator.MeasureRadius(match.Player),ArenaLayoutValidator.MeasureRadius(match.Opponent));
        public bool CanSelect => match!=null && match.IsInitialized && match.State==MatchController.MatchState.Ready &&
            match.GetComponent<MainMenuScreen>()!=null && match.GetComponent<MainMenuScreen>().isActiveAndEnabled &&
            match.GetComponent<SettingsPanelController>()!=null && !match.GetComponent<SettingsPanelController>().IsOpen;

        public void Configure(ArenaLayout[] choices,MatchController controller,Transform playerStart,Transform opponentStart,Material material)
        {
            var selected=Layout;
            layouts=choices;selectedLayout=System.Array.IndexOf(layouts,selected);if(selectedLayout<0)selectedLayout=0;
            match=controller;playerSpawn=playerStart;opponentSpawn=opponentStart;obstacleMaterial=material;
        }

#if UNITY_EDITOR
        public void GenerateLayout(ArenaLayout layout)
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play mode before authoring.");
            var errors=ArenaLayoutValidator.Validate(layout,ClearanceRadius);
            if(errors.Count>0)throw new System.InvalidOperationException(string.Join("\n",errors));
            var choices=new List<ArenaLayout>(layouts);
            if(!choices.Contains(layout))choices.Add(layout);
            layouts=choices.ToArray();selectedLayout=choices.IndexOf(layout);Rebuild();
        }
#endif

        private void Start() { Rebuild(); }
        private void OnEnable()
        {
            if(match==null)return;
            playerHealth=match.Player!=null?match.Player.GetComponent<RobotHealth>():null;
            opponentHealth=match.Opponent!=null?match.Opponent.GetComponent<RobotHealth>():null;
            if(playerHealth!=null)playerHealth.Restored+=ResetEffects;
            if(opponentHealth!=null)opponentHealth.Restored+=ResetEffects;
        }
        private void OnDisable()
        {
            if(playerHealth!=null)playerHealth.Restored-=ResetEffects;
            if(opponentHealth!=null)opponentHealth.Restored-=ResetEffects;
            ResetEffects();
        }
        public void ResetEffects()
        {
            ConveyorSurface.RemovePreviousMotion();
            if(Contents==null)return;
            foreach(var barrier in Contents.GetComponentsInChildren<WarningBarrier>())barrier.ResetCycle();
        }
        public bool Select(int index)
        {
            if(!CanSelect || index<0 || index>=layouts.Length) return false;
            var errors=ArenaLayoutValidator.Validate(layouts[index],ClearanceRadius);
            if(errors.Count>0) {Debug.LogWarning(string.Join("\n",errors));return false;}
            selectedLayout=index;Rebuild();return true;
        }

        public void Rebuild()
        {
            if(Application.isPlaying && match.IsInitialized && match.State!=MatchController.MatchState.Ready)
                throw new System.InvalidOperationException("Layout regeneration is only allowed while Ready.");
            var errors=ArenaLayoutValidator.Validate(Layout,ClearanceRadius);
            if(errors.Count>0) throw new System.InvalidOperationException(string.Join("\n",errors));
            Transform old=Contents;
            if(old!=null)
            {
                old.gameObject.SetActive(false);
                old.name="Retired Layout Contents";
                if(Application.isPlaying) Destroy(old.gameObject);else DestroyImmediate(old.gameObject);
            }
            var owned=new GameObject("Layout Contents");owned.transform.SetParent(transform,false);
            var bounds=GetComponent<ArenaBounds>();
            if(GetComponent<ConveyorMotion>()==null)gameObject.AddComponent<ConveyorMotion>();
            foreach(var c in Layout.cells)
            {
                Vector3 center=bounds.CellCenter(c.coordinate.x,c.coordinate.y);
                if(c.wall==ArenaWall.Reinforced)
                {
                    if(c.coordinate.x==0||c.coordinate.x==7)CreateWall(owned.transform,new Vector3(c.coordinate.x==0?-8:8,center.y+.6f,center.z),new Vector3(.3f,1.2f,2));
                    if(c.coordinate.y==0||c.coordinate.y==7)CreateWall(owned.transform,new Vector3(center.x,center.y+.6f,c.coordinate.y==0?-8:8),new Vector3(2,1.2f,.3f));
                }
                if(c.content==ArenaContent.Obstacle)
                {
                    var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.name="Arena Obstacle "+c.coordinate;
                    obstacle.transform.SetParent(owned.transform,false);
                    obstacle.transform.position=center+Vector3.up*.65f;
                    obstacle.transform.localScale=new Vector3(ArenaLayout.ObstacleWidth,1.3f,ArenaLayout.ObstacleWidth);
                    obstacle.GetComponent<Renderer>().sharedMaterial=obstacleMaterial;
                }
                if(c.spawn==ArenaSpawn.Player) SetSpawn(playerSpawn,center,90);
                if(c.content==ArenaContent.Hazard)
                {
                    var hazard=new GameObject("Arena Warning Barrier "+c.coordinate);
                    hazard.transform.SetParent(owned.transform,false);hazard.transform.position=center;
                    hazard.AddComponent<WarningBarrier>().Configure(match,obstacleMaterial);
                }
                if(c.spawn==ArenaSpawn.Opponent) SetSpawn(opponentSpawn,center,-90);
                if(c.content==ArenaContent.Conveyor)
                {
                    var belt=new GameObject("Arena Conveyor "+c.coordinate);
                    belt.transform.SetParent(owned.transform,false);belt.transform.position=center;
                    belt.AddComponent<ConveyorSurface>().Configure(match,c.direction,obstacleMaterial);
                }
            }
            if(Application.isPlaying && match.IsInitialized) match.Prepare();
            else if(!Application.isPlaying)
            {
                match.Player.transform.SetPositionAndRotation(playerSpawn.position,playerSpawn.rotation);
                match.Opponent.transform.SetPositionAndRotation(opponentSpawn.position,opponentSpawn.rotation);
            }
        }

        private static void SetSpawn(Transform spawn,Vector3 position,float yaw) => spawn.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        private void CreateWall(Transform parent,Vector3 position,Vector3 scale)
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Reinforced Perimeter";
            wall.transform.SetParent(parent,false);wall.transform.position=position;wall.transform.localScale=scale;
            wall.GetComponent<Renderer>().sharedMaterial=obstacleMaterial;
        }

        private void OnGUI()
        {
            if(!CanSelect) return;
            var previous=GUI.matrix;
            float scale=Mathf.Max(.01f,Mathf.Min(Screen.height/720f,Screen.width/600f));
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUILayout.BeginArea(new Rect(16,Screen.height/scale-105,290,90),GUI.skin.box);
            GUILayout.Label("Arena: "+Layout.displayName);
            GUILayout.BeginHorizontal();
            for(int i=0;i<layouts.Length;i++) if(GUILayout.Button(layouts[i].displayName)) Select(i);
            GUILayout.EndHorizontal();GUILayout.EndArea();GUI.matrix=previous;
        }
    }
}
