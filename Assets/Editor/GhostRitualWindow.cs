using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GhostRitualWindow : EditorWindow
{
    private const string GhostFolder = "Assets/Ghosts";
    private const string RitualFolder = "Assets/Ghosts/Rituals";
    private const int NewRitualSteps = 4;

    private Vector2 _detailScroll;
    private SerializedObject _ghostObject;
    private GhostType[] _ghosts = new GhostType[0];
    private Vector2 _listScroll;
    private string _newName = "";
    private SerializedObject _ritualObject;
    private ExorcismRitualData[] _rituals = new ExorcismRitualData[0];
    private string _search = "";
    private GhostType _selected;
    private string _testPhrase = "";

    [MenuItem("Tenebrosus/Ghosts And Rituals")]
    private static void Open()
    {
        GetWindow<GhostRitualWindow>("Ghosts & Rituals");
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnProjectChange()
    {
        Refresh();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawDetails();
        EditorGUILayout.EndHorizontal();
    }

    private void Refresh()
    {
        _ghosts = Load<GhostType>().OrderBy(ghost => ghost.name).ToArray();
        _rituals = Load<ExorcismRitualData>();
        Select(_ghosts.Contains(_selected) ? _selected : _ghosts.FirstOrDefault());
    }

    private void Select(GhostType ghost)
    {
        _selected = ghost;
        _ghostObject = ghost != null ? new SerializedObject(ghost) : null;
        ExorcismRitualData ritual = RitualFor(ghost);
        _ritualObject = ritual != null ? new SerializedObject(ritual) : null;
        GUI.FocusControl(null);
    }

    private ExorcismRitualData RitualFor(GhostType ghost)
    {
        return ghost == null ? null : _rituals.FirstOrDefault(ritual => ritual.GhostType == ghost);
    }

    private void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(230f));
        _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (GhostType ghost in _ghosts)
        {
            string label = string.IsNullOrEmpty(ghost.GhostName) ? ghost.name : ghost.GhostName;
            if (_search.Length > 0 && label.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (RitualFor(ghost) == null) label += "   (no ritual)";
            else if (_ghosts.Any(other => other != ghost && other.Evidence == ghost.Evidence)) label += "   (same evidence)";
            bool selected = ghost == _selected;
            if (!GUILayout.Toggle(selected, label, "Button") || selected) continue;
            Select(ghost);
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("New ghost", EditorStyles.boldLabel);
        _newName = EditorGUILayout.TextField(_newName);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newName)))
            if (GUILayout.Button("Create ghost + ritual"))
            {
                CreateGhost(_newName.Trim());
                GUIUtility.ExitGUI();
            }
        EditorGUILayout.Space();
        if (GUILayout.Button("Sync scene lists")) SyncScene();
        if (GUILayout.Button("Refresh"))
        {
            Refresh();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawDetails()
    {
        EditorGUILayout.BeginVertical();
        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
        if (_selected == null)
        {
            EditorGUILayout.HelpBox("No ghost types found. Create one on the left.", MessageType.Info);
        }
        else
        {
            DrawGhost();
            EditorGUILayout.Space(12f);
            DrawRitual();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawGhost()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Ghost", EditorStyles.largeLabel);
        if (GUILayout.Button("Select asset", GUILayout.Width(100f))) Ping(_selected);
        EditorGUILayout.EndHorizontal();
        DrawFields(_ghostObject);

        int evidenceCount = EvidenceNames.All.Count(_selected.Has);
        if (evidenceCount != 3)
            EditorGUILayout.HelpBox($"This ghost has {evidenceCount} evidence types. The others have 3.", MessageType.Warning);
        GhostType twin = _ghosts.FirstOrDefault(other => other != _selected && other.Evidence == _selected.Evidence);
        if (twin != null)
            EditorGUILayout.HelpBox($"Same evidence as {twin.GhostName}. Players cannot tell them apart.", MessageType.Warning);
    }

    private void DrawRitual()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Exorcism ritual", EditorStyles.largeLabel);
        if (_ritualObject != null && GUILayout.Button("Select asset", GUILayout.Width(100f)))
            Ping(_ritualObject.targetObject);
        EditorGUILayout.EndHorizontal();

        if (_ritualObject == null)
        {
            EditorGUILayout.HelpBox("This ghost has no ritual and cannot be banished.", MessageType.Error);
            if (!GUILayout.Button("Create ritual")) return;
            CreateRitual(_selected);
            GUIUtility.ExitGUI();
        }

        DrawFields(_ritualObject);
        ExorcismRitualData ritual = (ExorcismRitualData)_ritualObject.targetObject;
        if (ritual.Steps.Any(step => step.Effects == null))
            EditorGUILayout.HelpBox("Every step needs an effect profile.", MessageType.Error);
        if (ritual.Steps.Any(step => string.IsNullOrWhiteSpace(step.Phrase)))
            EditorGUILayout.HelpBox("Some steps have no Latin phrase.", MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Phrase tester", EditorStyles.boldLabel);
        _testPhrase = EditorGUILayout.TextField("What the mic heard", _testPhrase);
        if (string.IsNullOrWhiteSpace(_testPhrase)) return;
        for (int i = 0; i < ritual.Steps.Length; i++)
        {
            RitualStep step = ritual.Steps[i];
            float score = PhraseMatcher.Score(_testPhrase, step.Phrase);
            string verdict = score >= step.MatchThreshold ? "accepted" : "rejected";
            EditorGUILayout.LabelField($"{i + 1}. {step.Phrase}", $"{score:0.00} / {step.MatchThreshold:0.00}  {verdict}");
        }
    }

    private static void DrawFields(SerializedObject serialized)
    {
        serialized.Update();
        SerializedProperty property = serialized.GetIterator();
        property.NextVisible(true);
        while (property.NextVisible(false))
            EditorGUILayout.PropertyField(property, true);
        serialized.ApplyModifiedProperties();
    }

    private void CreateGhost(string ghostName)
    {
        GhostType ghost = CreateInstance<GhostType>();
        SerializedObject serialized = new(ghost);
        serialized.FindProperty("ghostName").stringValue = ghostName;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(ghost, AssetDatabase.GenerateUniqueAssetPath($"{GhostFolder}/{FileName(ghostName)}.asset"));
        CreateRitual(ghost);
        _newName = "";
        Refresh();
        Select(ghost);
    }

    private void CreateRitual(GhostType ghost)
    {
        ExorcismRitualData ritual = CreateInstance<ExorcismRitualData>();
        RitualEffectProfile profile = Load<RitualEffectProfile>().FirstOrDefault();
        SerializedObject serialized = new(ritual);
        serialized.FindProperty("ghostType").objectReferenceValue = ghost;
        SerializedProperty steps = serialized.FindProperty("steps");
        steps.arraySize = NewRitualSteps;
        for (int i = 0; i < NewRitualSteps; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            step.FindPropertyRelative("matchThreshold").floatValue = 0.6f;
            step.FindPropertyRelative("timeLimit").floatValue = 30f;
            step.FindPropertyRelative("effects").objectReferenceValue = profile;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        string ghostName = string.IsNullOrEmpty(ghost.GhostName) ? ghost.name : ghost.GhostName;
        AssetDatabase.CreateAsset(ritual,
            AssetDatabase.GenerateUniqueAssetPath($"{RitualFolder}/{FileName(ghostName)} Ritual.asset"));
        AssetDatabase.SaveAssets();
        Refresh();
    }

    private void SyncScene()
    {
        Refresh();
        foreach (GhostController controller in FindObjectsByType<GhostController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetArray(controller, "possibleTypes", _ghosts);
        foreach (RitualController controller in FindObjectsByType<RitualController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetArray(controller, "rituals", _rituals);
        BookPage[] pages = Load<IntroductionPage>().OrderBy(page => page.name)
            .Concat<BookPage>(_ghosts.Select(RitualFor).Where(ritual => ritual != null))
            .ToArray();
        foreach (ExorcismBook book in FindObjectsByType<ExorcismBook>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetArray(book, "pages", pages);
        foreach (Journal journal in FindObjectsByType<Journal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SyncJournal(journal);
    }

    private void SyncJournal(Journal journal)
    {
        SerializedObject serialized = new(journal);
        SerializedProperty entries = serialized.FindProperty("ghostEntries");
        GhostEntry[] existing = Enumerable.Range(0, entries.arraySize)
            .Select(i => (GhostEntry)entries.GetArrayElementAtIndex(i).objectReferenceValue)
            .Where(entry => entry != null)
            .ToArray();
        GhostEntry template = existing.LastOrDefault();
        if (template == null) return;
        foreach (GhostType ghost in _ghosts.Where(ghost => existing.All(entry => entry.GhostType != ghost)))
        {
            GameObject copy = Instantiate(template.gameObject, template.transform.parent);
            copy.name = $"Entry {ghost.name}";
            Undo.RegisterCreatedObjectUndo(copy, "Add journal entry");
            GhostEntry entry = copy.GetComponent<GhostEntry>();
            SerializedObject entryObject = new(entry);
            entryObject.FindProperty("ghostType").objectReferenceValue = ghost;
            entryObject.ApplyModifiedProperties();
            entries.arraySize++;
            entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = entry;
        }

        serialized.ApplyModifiedProperties();
    }

    private static void SetArray(Object target, string field, Object[] values)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedProperties();
    }

    private static string FileName(string text)
    {
        return string.Concat(text.Split(Path.GetInvalidFileNameChars()));
    }

    private static void Ping(Object target)
    {
        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }

    private static T[] Load<T>() where T : Object
    {
        return AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null)
            .ToArray();
    }
}
