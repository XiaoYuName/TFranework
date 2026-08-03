using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Localization;
#endif

#if UNITY_LOCALIZATION
using UnityEngine.Localization;
#endif

[Serializable]
[InlineProperty]
[HideLabel]
public class LocalSelectedData
{
#if UNITY_EDITOR
    [HorizontalGroup("LocalRow", Width = 0.35f)]
    [LabelText("表")]
    [ValueDropdown(nameof(GetLocalTables))]
    [OnValueChanged(nameof(OnTableChanged))]
#endif
    public string Table;

#if UNITY_EDITOR
    [HorizontalGroup("LocalRow", Width = 0.65f)]
    [LabelText("Key")]
    [EnableIf(nameof(HasTable))]
    [LocalizationKeySelector(nameof(Table))]
    [ValidateInput(nameof(IsKeyValid), "当前表中不存在这个 Key")]
#endif
    public string Value;

#if UNITY_EDITOR
    [ShowInInspector]
    [ReadOnly]
    [LabelText("文本预览")]
    [MultiLineProperty(3)]
    [ShowIf(nameof(HasValue))]
    private string PreviewText => GetPreviewText();
#endif

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Table) &&
               !string.IsNullOrEmpty(Value);
    }

#if UNITY_LOCALIZATION
    public LocalizedString ToLocalizedString()
    {
        return new LocalizedString(Table, Value);
    }
#endif

    public override string ToString()
    
    
    {
        if (string.IsNullOrEmpty(Table) || string.IsNullOrEmpty(Value))
            return "Null";

        return $"{Table}/{Value}";
    }

#if UNITY_EDITOR
    private bool HasTable()
    {
        return !string.IsNullOrEmpty(Table);
    }

    private bool HasValue()
    {
        return !string.IsNullOrEmpty(Table) &&
               !string.IsNullOrEmpty(Value);
    }

    private void OnTableChanged()
    {
        Value = null;
    }

    private IEnumerable GetLocalTables()
    {
        return LocalizationEditorSettings
            .GetStringTableCollections()
            .Where(c => c != null)
            .Select(c => c.TableCollectionName)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    private bool IsKeyValid(string key)
    {
        if (string.IsNullOrEmpty(Table) || string.IsNullOrEmpty(key))
            return true;

        var collection = LocalizationEditorSettings.GetStringTableCollection(Table);

        if (collection == null || collection.SharedData == null)
            return false;

        return collection.SharedData.Entries.Any(e => e.Key == key);
    }

    private string GetPreviewText()
    {
        if (string.IsNullOrEmpty(Table) || string.IsNullOrEmpty(Value))
            return string.Empty;

        var collection = LocalizationEditorSettings.GetStringTableCollection(Table);

        if (collection == null)
            return "未找到本地化表";

        // 多语言表顺序不固定，FirstOrDefault 可能拿到非中文表；优先选简体中文表。
        var tables = collection.StringTables;
        var table = tables.FirstOrDefault(t => t.LocaleIdentifier.Code.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault(t => t.LocaleIdentifier.Code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault();

        if (table == null)
            return "当前表没有语言内容";

        var entry = table.GetEntry(Value);

        if (entry == null)
            return "未找到 Key 对应文本";

        return entry.LocalizedValue;
    }

    [Button("打开本地化表")]
    [ShowIf(nameof(HasTable))]
    private void OpenLocalizationTable()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(Table);

        if (collection != null)
        {
            Selection.activeObject = collection;
            EditorGUIUtility.PingObject(collection);
        }
    }

    [Button("清空")]
    [ShowIf(nameof(HasValue))]
    private void Clear()
    {
        Value = null;
    }
#endif
}