using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotSavaslari.Arena
{
    public enum ArenaContent { Floor, Obstacle, Hazard, Conveyor }
    public enum ArenaSpawn { None, Player, Opponent }
    public enum ArenaWall { Standard, Reinforced }

    [Serializable]
    public sealed class ArenaCellData
    {
        public Vector2Int coordinate;
        public ArenaContent content;
        public ArenaSpawn spawn;
        public ArenaWall wall;
        public Vector2 direction = Vector2.right;
    }

    [CreateAssetMenu(menuName = "Robot Savaslari/Arena Layout")]
    public sealed class ArenaLayout : ScriptableObject
    {
        public string displayName;
        public List<ArenaCellData> cells = new List<ArenaCellData>();
        public const int Columns = 8;
        public const float Spacing = 2;
        public const float ObstacleWidth = 1.4f;
        public static Vector2 Position(Vector2Int cell) => new Vector2((cell.x-3.5f)*Spacing,(cell.y-3.5f)*Spacing);

        public void CreateDefault(bool tactical)
        {
            displayName = tactical ? "Tactical" : "Open";
            cells.Clear();
            for (int x=0;x<8;x++) for (int y=0;y<8;y++)
                cells.Add(new ArenaCellData { coordinate=new Vector2Int(x,y) });
            cells.Find(c=>c.coordinate==new Vector2Int(1,3)).spawn=ArenaSpawn.Player;
            cells.Find(c=>c.coordinate==new Vector2Int(6,4)).spawn=ArenaSpawn.Opponent;
            if (tactical)
                foreach (var p in new[]{new Vector2Int(3,3),new Vector2Int(3,4)}) cells.Find(c=>c.coordinate==p).content=ArenaContent.Obstacle;
            cells.Find(c=>c.coordinate==new Vector2Int(6,6)).content=ArenaContent.Hazard;
            cells.Find(c=>c.coordinate==new Vector2Int(2,1)).content=ArenaContent.Conveyor;
            cells.Find(c=>c.coordinate==new Vector2Int(3,1)).content=ArenaContent.Conveyor;
            cells.Find(c=>c.coordinate==new Vector2Int(0,3)).wall=ArenaWall.Reinforced;
        }
    }

    public static class ArenaLayoutValidator
    {
        public static List<string> Validate(ArenaLayout layout, float radius)
        {
            var errors=new List<string>();
            if(float.IsNaN(radius)||float.IsInfinity(radius)||radius<=0){errors.Add("Robot turning radius must be finite and positive.");return errors;}
            if (layout==null) { errors.Add("Layout is missing."); return errors; }
            if (layout.cells==null || layout.cells.Count!=64) { errors.Add("Layout must contain exactly 64 cells."); return errors; }
            var seen=new HashSet<Vector2Int>();
            int player=0,opponent=0;
            foreach(var c in layout.cells)
            {
                if(c==null) {errors.Add("Null cell.");continue;}
                if(c.coordinate.x<0||c.coordinate.x>7||c.coordinate.y<0||c.coordinate.y>7) errors.Add("Cell outside arena: "+c.coordinate);
                if(!seen.Add(c.coordinate)) errors.Add("Overlapping cell definitions: "+c.coordinate);
                if(!Enum.IsDefined(typeof(ArenaContent),c.content)||!Enum.IsDefined(typeof(ArenaSpawn),c.spawn)||!Enum.IsDefined(typeof(ArenaWall),c.wall)) errors.Add("Unknown cell content.");
                if(c.spawn==ArenaSpawn.Player) player++;
                if(c.spawn==ArenaSpawn.Opponent) opponent++;
                if(c.spawn!=ArenaSpawn.None && c.content!=ArenaContent.Floor) errors.Add("Spawn requires unobstructed normal floor: "+c.coordinate);
                if(c.content==ArenaContent.Conveyor && (float.IsNaN(c.direction.x)||float.IsNaN(c.direction.y)||float.IsInfinity(c.direction.x)||float.IsInfinity(c.direction.y)||c.direction.sqrMagnitude<.01f)) errors.Add("Conveyor requires a finite nonzero XZ direction: "+c.coordinate);
                if(c.wall!=ArenaWall.Standard && c.coordinate.x!=0 && c.coordinate.x!=7 && c.coordinate.y!=0 && c.coordinate.y!=7) errors.Add("Wall variation must be on the perimeter: "+c.coordinate);
            }
            if(player!=1||opponent!=1) errors.Add("Exactly one distinct Player and Opponent spawn is required.");
            if(errors.Count>0) return errors;
            var route=FindRoute(layout,radius);
            if(route.Count==0) errors.Add("Spawns are disconnected or the corridor is too narrow for the robot and turning clearance.");
            return errors;
        }

        // .5 m samples in configuration space, with obstacles expanded by the full turn radius.
        public static List<Vector2> FindRoute(ArenaLayout layout,float radius)
        {
            var route=new List<Vector2>();
            var player=layout.cells.Find(c=>c.spawn==ArenaSpawn.Player);
            var opponent=layout.cells.Find(c=>c.spawn==ArenaSpawn.Opponent);
            if(player==null||opponent==null) return route;
            Vector2 start=ArenaLayout.Position(player.coordinate), end=ArenaLayout.Position(opponent.coordinate);
            if(!IsClear(layout,start,radius)||!IsClear(layout,end,radius)) return route;
            var queue=new Queue<Vector2Int>();
            var previous=new Dictionary<Vector2Int,Vector2Int>();
            Vector2Int first=new Vector2Int(Mathf.RoundToInt(start.x*2),Mathf.RoundToInt(start.y*2));
            Vector2Int last=new Vector2Int(Mathf.RoundToInt(end.x*2),Mathf.RoundToInt(end.y*2));
            queue.Enqueue(first); previous[first]=first;
            var moves=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
            while(queue.Count>0)
            {
                var p=queue.Dequeue();
                foreach(var move in moves)
                {
                    var n=p+move;
                    if(previous.ContainsKey(n)||!IsClear(layout,(Vector2)n*.5f,radius)) continue;
                    previous[n]=p;queue.Enqueue(n);
                }
            }
            // Reject isolated free pockets, not merely a missing spawn-to-spawn route.
            for(int x=-16;x<=16;x++) for(int y=-16;y<=16;y++)
                if(IsClear(layout,new Vector2(x,y)*.5f,radius)&&!previous.ContainsKey(new Vector2Int(x,y))) return route;
            if(!previous.ContainsKey(last)) return route;
            for(var p=last;;p=previous[p]) {route.Add((Vector2)p*.5f);if(p==first) break;}
            route.Reverse(); return route;
        }

        public static bool IsBlocking(ArenaContent content) => content==ArenaContent.Obstacle || content==ArenaContent.Hazard;
        public static bool IsClear(ArenaLayout layout,Vector2 point,float radius)
        {
            float limit=7.81f-radius;
            if(Mathf.Abs(point.x)>limit||Mathf.Abs(point.y)>limit) return false;
            float extent=ArenaLayout.ObstacleWidth*.5f+radius+.05f;
            foreach(var c in layout.cells)
                if(IsBlocking(c.content))
                {
                    Vector2 d=point-ArenaLayout.Position(c.coordinate);
                    if(Mathf.Abs(d.x)<=extent&&Mathf.Abs(d.y)<=extent) return false;
                }
            return true;
        }

        public static float MeasureRadius(RobotPrototypeController robot)
        {
            float radius=0;
            foreach(var c in robot.GetComponentsInChildren<Collider>())
            {
                if(c.isTrigger||!c.enabled) continue;
                var b=c.bounds;
                Vector3 d=b.center-robot.transform.position;
                radius=Mathf.Max(radius,new Vector2(Mathf.Abs(d.x)+b.extents.x,Mathf.Abs(d.z)+b.extents.z).magnitude);
            }
            return Mathf.Max(radius,robot.ArenaFootprintRadius);
        }
    }
}
