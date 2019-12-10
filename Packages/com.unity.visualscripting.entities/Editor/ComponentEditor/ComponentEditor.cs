using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Callbacks;
using UnityEditor.CodeViewer;
using UnityEditor.Experimental.GraphView;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Blackboard = UnityEditor.Experimental.GraphView.Blackboard;
using UICreationHelper = Packages.VisualScripting.Editor.UICreationHelper;

namespace UnityEditor.VisualScripting.ComponentEditor
{
    public class ComponentEditor : EditorWindow
    {
        class DoCreateComponent : EndNameEditAction
        {
            public StructType StructType;

            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var modelName = Path.GetFileNameWithoutExtension(pathName);
                var fileModel = new FileModel(FileModel.MakeCompilationUnit().SyntaxTree, FileModel.ParseOptions.DisallowMultipleStructs);
                fileModel.Structs.Add(new StructModel(modelName, StructType));
                var code = fileModel.Generate();

                File.WriteAllText(pathName, code);

                GetWindow<ComponentEditor>().LoadUnimportedAsset(pathName, code);
            }
        }

        class GhostGraphView : GraphView
        {
            readonly ComponentEditor m_ComponentEditor;

            public GhostGraphView(ComponentEditor componentEditor)
            {
                m_ComponentEditor = componentEditor;
            }

            public override EventPropagation DeleteSelection()
            {
                m_ComponentEditor.DeleteSelection();
                return EventPropagation.Stop;
            }
        }

        class TypeSearcherAdapter : SearcherAdapter
        {
            public override bool HasDetailsPanel => false;

            public TypeSearcherAdapter(string title)
                : base(title) {}
        }

        class PropertyRow : VisualElement
        {
            public PropertyRow(string label, VisualElement fieldType)
            {
                AddToClassList("propertyRow");
                Add(new Label { text = label });
                Add(fieldType);
            }
        }

        class PickTypeSearcherItem : SearcherItem
        {
            public Type Type { get; }
            public bool Shared { get; }

            public PickTypeSearcherItem(Type type, bool shared)
                : base(type.FriendlyName())
            {
                Type = type;
                Shared = shared;
            }
        }

        static readonly Dictionary<string, Type> s_TypeCache = new Dictionary<string, Type>();
        static IReadOnlyCollection<SearcherItem> TypeItems;

        static void InitializeTypeItemsAndCache()
        {
            if (TypeItems != null) return;

            SearcherItem MakeItem(Type t, bool isShared)
            {
                s_TypeCache[t.FullName] = t;
                return new PickTypeSearcherItem(t, isShared);
            }

            IEnumerable<SearcherItem> MakeItems(bool isShared, IEnumerable<Type> types) => types.Select(t => MakeItem(t, isShared));

            var l = new List<SearcherItem>();
            SearcherItem common = new SearcherItem("Standard Types");
            SearcherItem shared = new SearcherItem("Types available in Shared Components only");
            l.Add(common);
            l.Add(shared);

            foreach (var searcherItem in MakeItems(false, new[]
            {
                typeof(int),
                typeof(bool),
                typeof(float),
                typeof(float2),
                typeof(float3),
                typeof(float4),
                typeof(quaternion),
                typeof(Entity),
                typeof(Color),
            }))
                common.AddChild(searcherItem);

            common.AddChild(MakeItem(typeof(GameObject), false));

            foreach (var searcherItem in MakeItems(true,
                TypeCache.GetTypesDerivedFrom<Component>().Where(EcsStencil.IsValidGameObjectComponentType)))
                shared.AddChild(searcherItem);
            TypeItems = l;
        }

        static GUIContent[] s_StructTypeOptions = Enum.GetValues(typeof(StructType)).Cast<StructType>()
            .Where(x => x != StructType.Unknown)
            .Select(v => new GUIContent(v.ToString())).ToArray();

        [MenuItem("Window/Component Editor")]
        static void Open()
        {
            GetWindow<ComponentEditor>().Show();
        }

        [OnOpenAsset(-1)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId);

            // Hack. will do for now
            if (obj is MonoScript ms)
            {
                var path = AssetDatabase.GetAssetPath(ms);
                var scriptText = File.ReadAllText(path); // script.text;
                var fileModel = FileModel.Parse(scriptText, FileModel.ParseOptions.DisallowMultipleStructs);
                if (fileModel != null && fileModel.Structs.Any())
                {
                    var w = GetWindow<ComponentEditor>();
                    w.LoadFromPath(path);

                    return true;
                }
            }

            return false;
        }

        [MenuItem("Assets/Create/Visual Script/New Component")]
        static void NewComponent() => CreateNewStruct(StructType.Component);

        [MenuItem("Assets/Create/Visual Script/New Shared Component")]
        static void NewSharedComponent() => CreateNewStruct(StructType.SharedComponent);

        [MenuItem("Assets/Create/Visual Script/New Buffer Element")]
        static void NewBufferElement() => CreateNewStruct(StructType.BufferElement);

        static void CreateNewStruct(StructType structType)
        {
            string uniqueFilePath = VseUtility.GetUniqueAssetPathNameInActiveFolder("Component.cs");
            string modelName = Path.GetFileName(uniqueFilePath);

            var endAction = CreateInstance<DoCreateComponent>();
            endAction.StructType = structType;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, endAction, modelName, null, null);
        }

        [SerializeField]
        string m_CurrentPath;
        HashSet<FieldModel> m_ExpandedRows = new HashSet<FieldModel>();
        FileModel m_CurrentModel;
        bool m_Dirty;
        GhostGraphView m_GhostGraphView;
        Blackboard m_Blackboard;
        Button m_SaveButton;
        BlackboardSection m_InfoSection;
        BlackboardSection m_StructSection;
        BlackboardSection m_FieldsSection;
        TextField m_StructNameField;
        Button m_PickStructTypeButton;
        Label m_StructTypeLabel;
        VisualElement m_BlackboardContentContainer;

        void OnEnable()
        {
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "ComponentEditor.uss"));

            rootVisualElement.AddToClassList("root");
            var root = rootVisualElement;

            m_GhostGraphView = new GhostGraphView(this);

            m_Blackboard = new Blackboard(m_GhostGraphView) { windowed = true };
            m_BlackboardContentContainer = m_Blackboard.Q("unity-content-container");

            // need wait for layout to avoid default 200px width value
            root.RegisterCallback<GeometryChangedEvent>(e =>
            {
                if (!m_Blackboard.scrollable)
                {
                    m_Blackboard.scrollable = true;
                    var sv = m_Blackboard.Q<ScrollView>();
#if UNITY_2020_1_OR_NEWER
                    sv.RemoveFromClassList(ScrollView.hContentVariantUssClassName);
                    sv.RemoveFromClassList(ScrollView.hViewportVariantUssClassName);
#else
                    sv.RemoveFromClassList(ScrollView.horizontalVariantUssClassName);
#endif
                    sv.RemoveFromClassList(ScrollView.scrollVariantUssClassName);
                }

                if (m_BlackboardContentContainer != null)
                    m_BlackboardContentContainer.style.width = e.newRect.width;
            });

            m_Blackboard.editTextRequested = EditTextRequested;
            m_Blackboard.addItemRequested = AddItemRequested;
            root.Add(m_Blackboard);

            m_Blackboard.Add(m_InfoSection = new BlackboardSection() { name = "infoSection", title = "No component selected" });
            m_InfoSection.Add(new Label("Open an existing component or create a new one.") { name = "noSelectionLabel" });
            m_Blackboard.Add(m_StructSection = new BlackboardSection() { name = "structSection", title = "Struct" });
            m_Blackboard.Add(m_FieldsSection = new BlackboardSection() { name = "fieldsSection", title = "Fields" });

            m_StructNameField = new TextField("Name");
            m_StructNameField.RegisterValueChangedCallback(e =>
            {
                if (CurrentStruct.Name != e.newValue)
                {
                    CurrentStruct.Name = e.newValue;
                    SetModelDirty();
                }
            });

            m_StructSection.Add(m_StructNameField);

            m_PickStructTypeButton = new Button(() => PickStructType(CurrentStruct, SetStructType));
            var structTypeRow = new VisualElement { name = "structTypeRow" };
            structTypeRow.Add(new Label("Type"));
            structTypeRow.Add(m_PickStructTypeButton);
            m_StructSection.Add(structTypeRow);
            m_StructSection.Add(m_StructTypeLabel = new Label { name = "structTypeLabel" });

            m_FieldsSection.Q("sectionHeader").Add(new Button(() =>
            {
                FieldModel newField = CurrentStruct.Add(typeof(int), MakeUniqueFieldName("newField"));
                m_FieldsSection.Add(MakeFieldRow(newField));
                SetModelDirty();
            }) { text = "+" });

            var bottomToolbar = new VisualElement { name = "bottomToolbar" };
            bottomToolbar.Add(m_SaveButton = new Button(Save) { text = "Save" });
            root.Add(bottomToolbar);

            // resume after domain reload
            if (!string.IsNullOrEmpty(m_CurrentPath) && File.Exists(m_CurrentPath))
                LoadFromPath(m_CurrentPath);
            else
                Unload();
        }

        StructModel CurrentStruct => m_CurrentModel?.Structs?.SingleOrDefault();

        internal static Dictionary<string, Type> ComponentTypeCache
        {
            get
            {
                InitializeTypeItemsAndCache();
                return s_TypeCache;
            }
        }

        void Unload()
        {
            m_CurrentPath = null;
            rootVisualElement.EnableInClassList("hidden", true);
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is MonoScript script)
                LoadFromPath(AssetDatabase.GetAssetPath(script));
        }

        void LoadUnimportedAsset(string pathName, string content)
        {
            Load(FileModel.Parse(content, FileModel.ParseOptions.DisallowMultipleStructs), pathName);

            // need to be able to save straight away and trigger a domain reload
            SetModelDirty();
        }

        void LoadFromPath(string path)
        {
            var scriptText = File.ReadAllText(path); // script.text;
            var fileModel = FileModel.Parse(scriptText, FileModel.ParseOptions.DisallowMultipleStructs);
            Load(fileModel, path);
        }

        void Load(FileModel fileModel, string path)
        {
            if (String.IsNullOrEmpty(path))
                throw new InvalidOperationException("The path provided is invalid");

            rootVisualElement.EnableInClassList("hidden", false);

            m_ExpandedRows.Clear();
            if (m_CurrentModel == fileModel)
                m_FieldsSection.Query<BlackboardRow>().ForEach(row =>
                {
                    if (row.expanded)
                        m_ExpandedRows.Add((FieldModel)row.userData);
                });
            m_CurrentModel = fileModel;
            m_CurrentPath = path;

            m_FieldsSection.Clear();

            if (fileModel == null)
                return;

            ShowCode();
            var structModel = fileModel.Structs.SingleOrDefault();
            if (structModel == null)
                return;
            CreateStructView(structModel);
            m_Blackboard.title = structModel.Name;
            m_Blackboard.subTitle = structModel.Type.ToString();
        }

        void CreateStructView(StructModel structModel)
        {
            m_StructNameField.value = structModel.Name;
            m_PickStructTypeButton.text = structModel.Type.ToString();
            if (CurrentStruct != null)
                m_StructTypeLabel.text = MakeStructTypeTooltip(CurrentStruct.Type);

            m_FieldsSection.Clear();
            foreach (var fieldModel in structModel.Fields)
            {
                var fieldRow = MakeFieldRow(fieldModel);
                m_FieldsSection.Add(fieldRow);
            }
        }

        static void PickStructType(StructModel structModel, Action<StructModel, StructType> action)
        {
            EditorUtility.DisplayCustomMenu(
                new Rect(Event.current.mousePosition, Vector2.one),
                s_StructTypeOptions,
                UnsafeUtility.EnumToInt(structModel.Type),
                (data, options, selected) =>
                {
                    StructType type = (StructType)selected;
                    action(structModel, type);
                },
                null);
        }

        void SetStructType(StructModel structModel, StructType type)
        {
            structModel.Type = type;
            SetModelDirty();
            Reload();
            m_StructTypeLabel.text = MakeStructTypeTooltip(type);
        }

        string MakeStructTypeTooltip(StructType currentStructType)
        {
            switch (currentStructType)
            {
                case StructType.Unknown:
                    break;
                case StructType.Component:
                    return "Components are appropriate for data that varies between entities, such as storing a World position. They cannot contain references to assets.";
                case StructType.SharedComponent:
                    return "Shared Components can contain references to assets. They're also useful when many entities have something in common: many entities with the same prefab reference in a shared component will actually reference the same instance of shared component.";
                case StructType.BufferElement:
                    return "Buffer Elements are similar to components, but can be present multiple times on the same entity";
                case StructType.Event:
                    return "Events are Buffer Elements that can be sent and received.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentStructType), currentStructType, null);
            }

            return "";
        }

        void DeleteSelection()
        {
            foreach (var blfield in m_Blackboard.selection.OfType<BlackboardField>())
            {
                FieldModel field = (FieldModel)blfield.userData;
                field.RemoveFromStruct();
            }
        }

        string MakeUniqueFieldName(string newName)
        {
            int i = 2;
            string tentativeName = newName;
            while (true)
            {
                if (!CurrentStruct.Fields.Any(f => f.Name == tentativeName))
                    return tentativeName;
                tentativeName = $"{newName}{i++}";
            }
        }

        void Reload()
        {
            Load(m_CurrentModel, m_CurrentPath);
        }

        static void AddItemRequested(Blackboard obj)
        {
            EditorUtility.DisplayCustomMenu(new Rect(Event.current.mousePosition, Vector2.one),
                s_StructTypeOptions,
                -1,
                (data, options, selected) => CreateNewStruct((StructType)selected),
                null);
        }

        void EditTextRequested(Blackboard arg1, VisualElement arg2, string newName)
        {
            var blackboardField = (BlackboardField)arg2;
            var field = (FieldModel)blackboardField.userData;
            blackboardField.text = newName;
            field.Name = newName;
            SetModelDirty();
        }

        VisualElement MakeFieldRow(FieldModel fieldModel)
        {
            var field = new BlackboardField(null, fieldModel.Name, fieldModel.Type?.FriendlyName() ?? "<unknown>");

            field.userData = fieldModel;
            var propertyView = new VisualElement();

            var fieldRow = new BlackboardRow(field, propertyView) { userData = fieldModel };

            fieldRow.expanded = m_ExpandedRows.Contains(fieldModel);

            field.Add(new Button(() => DeleteField(fieldRow, fieldModel)) { name = "deleteComponentIcon" });

            var fieldType = new Button() { text = fieldModel.Type?.FriendlyName() ?? "<unknown>" };
            fieldType.clickable.clicked += () =>
            {
                InitializeTypeItemsAndCache(); // delay init as the type cache seems to have a weird timing issue otherwise
                SearcherWindow.Show(this, new Searcher.Searcher(new SearcherDatabase(TypeItems), new TypeSearcherAdapter("Pick a type")), item =>
                {
                    PickTypeSearcherItem titem = item as PickTypeSearcherItem;

                    if (titem == null)
                        return true;
                    fieldModel.Type = titem.Type;
                    fieldType.text = titem.Type.FriendlyName();

                    if (titem.Shared)
                        SetStructType(CurrentStruct, StructType.SharedComponent);

                    SetModelDirty();

                    return true;
                }, Event.current.mousePosition, null);
            };
            propertyView.Add(new PropertyRow("Type", fieldType));

            var toggle = new Toggle() { value = fieldModel.HideInInspector };
            toggle.RegisterValueChangedCallback(e =>
            {
                fieldModel.HideInInspector = e.newValue;
                SetModelDirty();
            });
            propertyView.Add(new PropertyRow("Hide in inspector", toggle));

            // TODO ugly
            if (!fieldModel.Type.IsValueType && fieldModel.Type != typeof(GameObject))
                propertyView.Add(new Label("This is a reference type and requires the component to be a shared component"));
            return fieldRow;
        }

        void DeleteField(BlackboardRow field, FieldModel fieldModel)
        {
            fieldModel.RemoveFromStruct();
            field.RemoveFromHierarchy();
            SetModelDirty();
        }

        void Update()
        {
            var expectedSaveLabel = m_Dirty ? "Save*" : "Save";
            if (expectedSaveLabel != m_SaveButton.text)
                m_SaveButton.text = expectedSaveLabel;
        }

        void ShowCode()
        {
            CodeViewerWindow.SetDocument(CodeViewerWindow.SplitCode(m_CurrentModel.Generate()));
        }

        void Save()
        {
            if (!m_Dirty)
                return;
            m_Dirty = false;

            var folder = Path.GetDirectoryName(m_CurrentPath);
            Assert.IsNotNull(folder);
            var newName = CurrentStruct.ProxyName() + ".cs";
            var newPath = Path.Combine(folder, newName);
            if (newPath != m_CurrentPath)
            {
                if (File.Exists(m_CurrentPath))
                {
                    AssetDatabase.RenameAsset(m_CurrentPath, newName);
                    File.Delete(m_CurrentPath);
                }

                m_CurrentPath = newPath;
            }

            File.WriteAllText(m_CurrentPath, m_CurrentModel.Generate());
            AssetDatabase.Refresh();
        }

        void SetModelDirty()
        {
            m_Dirty = true;
            GetWindow(typeof(CodeViewerWindow), false, null, false);
            ShowCode();
        }
    }
}
