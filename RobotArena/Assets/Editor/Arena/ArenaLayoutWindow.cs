using System;
using System.Linq;
using RobotSavaslari.Match.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotSavaslari.Arena.Editor
{
    public sealed class ArenaLayoutWindow : EditorWindow
    {
        private const string DirectoryPath="Assets/Editor/Arena/";
        [SerializeField] private ArenaLayout layout;
        [SerializeField] private Vector2Int selected;
        private ObjectField asset;
        private EnumField content,spawn,wall;
        private Vector2Field direction;
        private Label cellTitle,validation;
        private TextField displayName;
        private Button generate;
        private VisualElement hazardFields;
        private FloatField warning, active, recovery, damage, repeat;
        private FloatField AddHazardField(string label)
        {
            var field = new FloatField(label);
            hazardFields.Add(field);
            return field;
        }
        private void ShowHazardFields(ArenaContent type)
        {
            hazardFields.style.display = type == ArenaContent.Pulse || type == ArenaContent.Sweep ? DisplayStyle.Flex : DisplayStyle.None;
            repeat.SetEnabled(type == ArenaContent.Pulse);
        }

        [MenuItem("RobotSavaslari/Gameplay/Arena Layout Editor")]
        public static ArenaLayoutWindow Open()
        {
            var window=GetWindow<ArenaLayoutWindow>("Arena Layout");window.minSize=new Vector2(770,700);return window;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DirectoryPath+"ArenaLayoutWindow.uxml").CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(DirectoryPath+"ArenaLayoutWindow.uss"));
            asset=new ObjectField("Layout asset"){objectType=typeof(ArenaLayout),allowSceneObjects=false};
            asset.RegisterValueChangedCallback(e=>{layout=e.newValue as ArenaLayout;Refresh();});rootVisualElement.Q("assetTools").Add(asset);
            var shortcuts=rootVisualElement.Q("shortcuts");
            foreach(string name in new[]{"Open","Tactical"})shortcuts.Add(new Button(()=>SetLayout(AssetDatabase.LoadAssetAtPath<ArenaLayout>("Assets/Settings/Arena/"+name+".asset"))){text=name});
            shortcuts.Add(new Button(CreateAsset){text="Create layout asset"});
            var inspector=rootVisualElement.Q("inspector");
            displayName=new TextField("Layout name");inspector.Add(displayName);
            cellTitle=new Label();inspector.Add(cellTitle);
            content=new EnumField("Content",ArenaContent.Floor);spawn=new EnumField("Spawn",ArenaSpawn.None);wall=new EnumField("Wall",ArenaWall.Standard);
            direction=new Vector2Field("Direction XZ");inspector.Add(content);inspector.Add(spawn);inspector.Add(wall);inspector.Add(direction);
            hazardFields = new VisualElement();
            inspector.Add(hazardFields);
            warning = AddHazardField("Warning (s)");
            active = AddHazardField("Active (s)");
            recovery = AddHazardField("Recovery (s)");
            damage = AddHazardField("Damage (0 = default)");
            repeat = AddHazardField("Pulse interval (s)");
            content.RegisterValueChangedCallback(evt => ShowHazardFields((ArenaContent)evt.newValue));
            inspector.Add(new Button(ApplyCell){text="Apply cell"});
            inspector.Add(new Label("One exclusive content per cell. Spawn requires Floor. Reinforced walls are valid only on the outer row/column."){name="rules"});
            inspector.Q<Label>("rules").AddToClassList("help");
            validation=rootVisualElement.Q<Label>("validation");var actions=rootVisualElement.Q("actions");
            actions.Add(new Button(ValidateLayout){text="Validate robot clearance"});
            generate=new Button(Generate){text="Generate in MainArena and save"};actions.Add(generate);
            if(layout==null)layout=AssetDatabase.LoadAssetAtPath<ArenaLayout>("Assets/Settings/Arena/Open.asset");
            Refresh();Undo.undoRedoPerformed+=Refresh;EditorApplication.playModeStateChanged+=PlayModeChanged;
        }
        private void OnDisable(){Undo.undoRedoPerformed-=Refresh;EditorApplication.playModeStateChanged-=PlayModeChanged;}
        private void PlayModeChanged(PlayModeStateChange state){Refresh();}
        public void SetLayout(ArenaLayout value){layout=value;Refresh();}
        private void Refresh()
        {
            if(asset==null)return;asset.SetValueWithoutNotify(layout);
            var grid=rootVisualElement.Q("grid");grid.Clear();
            for(int y=7;y>=0;y--)
            {
                var row=new VisualElement();row.AddToClassList("grid-row");grid.Add(row);
                for(int x=0;x<8;x++)
                {
                    var coordinate=new Vector2Int(x,y);var cell=layout?.cells?.FirstOrDefault(c=>c!=null&&c.coordinate==coordinate);
                    var button=new Button(()=>{selected=coordinate;Refresh();});button.AddToClassList("cell");
                    button.AddToClassList(cell==null?"floor":cell.content.ToString().ToLowerInvariant());
                    if(selected==coordinate)button.AddToClassList("selected");
                    button.text=x+","+y+"\n"+(cell==null?"Missing":cell.spawn==ArenaSpawn.Player?"Player":cell.spawn==ArenaSpawn.Opponent?"Rival":cell.wall==ArenaWall.Reinforced?"Wall":cell.content==ArenaContent.Conveyor?"Belt":cell.content==ArenaContent.Obstacle?"Block":cell.content.ToString());
                    button.tooltip=cell==null?"Missing cell":cell.content+" / "+cell.spawn+" / "+cell.wall;row.Add(button);
                }
            }
            var current=layout?.cells?.FirstOrDefault(c=>c!=null&&c.coordinate==selected);
            cellTitle.text="Cell "+selected;displayName.SetValueWithoutNotify(layout!=null?layout.displayName:"");
            if(current!=null){content.SetValueWithoutNotify(current.content);spawn.SetValueWithoutNotify(current.spawn);wall.SetValueWithoutNotify(current.wall);direction.SetValueWithoutNotify(current.direction);}
            if (current != null)
            {
                warning.SetValueWithoutNotify(current.warningSeconds);
                active.SetValueWithoutNotify(current.activeSeconds);
                recovery.SetValueWithoutNotify(current.recoverySeconds);
                damage.SetValueWithoutNotify(current.damageOverride);
                repeat.SetValueWithoutNotify(current.repeatSeconds);
                ShowHazardFields(current.content);
            }
            rootVisualElement.Q("inspector").SetEnabled(layout!=null&&!EditorApplication.isPlayingOrWillChangePlaymode);
            ValidateLayout();
        }
        private float Radius()
        {
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaEnvironment>();return arena!=null?arena.ClearanceRadius:1.6f;
        }
        public void ValidateLayout()
        {
            var errors=ArenaLayoutValidator.Validate(layout,Radius());
            validation.text=errors.Count==0?"Valid: 64 cells, distinct spawns and connected robot turning clearance. Generate to apply this asset to MainArena.":string.Join("\n",errors);
            validation.EnableInClassList("error",errors.Count>0);validation.EnableInClassList("valid",errors.Count==0);
            generate.SetEnabled(errors.Count==0&&!EditorApplication.isPlayingOrWillChangePlaymode);
        }
        private void ApplyCell()
        {
            if(layout==null||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var current=layout.cells.FirstOrDefault(c=>c!=null&&c.coordinate==selected);if(current==null)return;
            Undo.RecordObject(layout,"Edit arena cell");layout.displayName=displayName.value;
            current.content=(ArenaContent)content.value;current.spawn=(ArenaSpawn)spawn.value;current.wall=(ArenaWall)wall.value;current.direction=direction.value;
            if (current.IsDamaging)
            {
                current.warningSeconds = warning.value;
                current.activeSeconds = active.value;
                current.recoverySeconds = recovery.value;
                current.damageOverride = damage.value;
                current.repeatSeconds = repeat.value;
            }
            EditorUtility.SetDirty(layout);AssetDatabase.SaveAssets();Refresh();
        }
        private void CreateAsset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string path=EditorUtility.SaveFilePanelInProject("Create arena layout","ArenaLayout","asset","Choose an arena layout path");if(string.IsNullOrEmpty(path))return;
            var value=CreateInstance<ArenaLayout>();value.CreateDefault(false);value.displayName=System.IO.Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(value,path);AssetDatabase.SaveAssets();SetLayout(value);
        }
        public void Generate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var errors=ArenaLayoutValidator.Validate(layout,Radius());if(errors.Count>0){ValidateLayout();return;}
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=MainArenaSceneBuilder.ScenePath)
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
                EditorSceneManager.OpenScene(MainArenaSceneBuilder.ScenePath);
            }
            var arena=UnityEngine.Object.FindFirstObjectByType<ArenaEnvironment>();
            if(arena==null)throw new InvalidOperationException("Run the existing Rebuild Main Arena tool first.");
            arena.GenerateLayout(layout);
            EditorSceneManager.MarkSceneDirty(arena.gameObject.scene);EditorSceneManager.SaveScene(arena.gameObject.scene);AssetDatabase.SaveAssets();Refresh();
        }
    }
}
