using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using XFramework;
using Debug = UnityEngine.Debug;

public class ConfigTools : OdinEditorWindow
{
    private const string GenClientBatRelativePath = "ExcelTool/LubanTools/DataTables/gen_client.bat";
    private const string DefaultXlsxFolderRelativePath = "ExcelTool/LubanTools/DataTables/Datas";
    private const int GenClientTimeoutMs = 300000;

    [TitleGroup("配置生成工具")]
    [BoxGroup("配置生成工具/一键流程", ShowLabel = false)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("执行顺序")]
    [PropertyOrder(-20)]
    private string GenerateFlow => "gen_client.bat  ->  AssetDatabase.Refresh  ->  AssetKeys  ->  LubanManager.Generated.cs";

    [BoxGroup("配置生成工具/一键流程")]
    [HorizontalGroup("配置生成工具/一键流程/Actions")]
    [Button("一键生成配置", ButtonSizes.Large)]
    [GUIColor(0.35f, 0.85f, 0.45f)]
    [PropertyOrder(-10)]
    private void GenerateAllConfigs()
    {
        try
        {
            EditorUtility.DisplayProgressBar("一键生成配置", "执行 gen_client.bat...", 0.1f);
            if (!RunGenClientBat())
            {
                Debug.LogError("一键生成配置失败：gen_client.bat 执行失败。");
                return;
            }

            EditorUtility.DisplayProgressBar("一键生成配置", "刷新 AssetDatabase...", 0.3f);
            AssetDatabase.Refresh();

            EditorUtility.DisplayProgressBar("一键生成配置", "生成 Addressable AssetKeys...", 0.55f);
            if (!AddressableKeyGeneratorOdinWindow.GenerateWithDefaultSettings())
            {
                Debug.LogError("一键生成配置失败：Addressable AssetKeys 生成失败。");
                return;
            }

            EditorUtility.DisplayProgressBar("一键生成配置", "生成 LubanManager.Generated.cs...", 0.8f);
            if (!LubanManagerGeneratorWindow.GenerateWithDefaultConfig())
            {
                Debug.LogError("一键生成配置失败：LubanManager.Generated.cs 生成失败。");
                return;
            }

            EditorUtility.DisplayProgressBar("一键生成配置", "完成刷新...", 0.95f);
            AssetDatabase.Refresh();
            RefreshExcelInfo();

            Debug.Log("一键生成配置完成：已依次执行 gen_client.bat、生成 AssetKeys 与 LubanManager.Generated.cs。");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [BoxGroup("配置生成工具/一键流程")]
    [HorizontalGroup("配置生成工具/一键流程/Actions")]
    [Button("刷新列表", ButtonSizes.Large)]
    [GUIColor(0.45f, 0.7f, 1f)]
    [PropertyOrder(-9)]
    private void RefreshExcelInfo()
    {
        excelFiles.Clear();
        string absoluteXlsxFolder = GetAbsoluteProjectPath(xlsxFolder);

        if (!Directory.Exists(absoluteXlsxFolder))
        {
            return;
        }

        excelFiles = Directory.GetFiles(absoluteXlsxFolder)
            .Where(IsExcelFile)
            .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .Select(path => new ExcelFileInfo(path))
            .ToList();
    }

    [BoxGroup("Excel 文件")]
    [FolderPath(RequireExistingPath = true)]
    [LabelText("Excel 目录")]
    [OnValueChanged(nameof(OnXlsxFolderChanged))]
    [InfoBox("当前 Excel 目录不存在。", InfoMessageType.Warning, nameof(IsXlsxFolderInvalid))]
    [PropertyOrder(0)]
    [SerializeField]
    private string xlsxFolder;

    [BoxGroup("Excel 文件")]
    [HorizontalGroup("Excel 文件/Summary")]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("文件数量")]
    [PropertyOrder(1)]
    private int ExcelFileCount => excelFiles.Count;

    [BoxGroup("Excel 文件")]
    [HorizontalGroup("Excel 文件/Summary")]
    [Button("打开目录", ButtonSizes.Medium)]
    [EnableIf(nameof(HasValidXlsxFolder))]
    [PropertyOrder(2)]
    private void OpenXlsxFolder()
    {
        EditorUtility.RevealInFinder(GetAbsoluteProjectPath(xlsxFolder));
    }

    [BoxGroup("Excel 文件")]
    [TableList(IsReadOnly = true, AlwaysExpanded = true)]
    [HideLabel]
    [PropertyOrder(3)]
    [SerializeField]
    private List<ExcelFileInfo> excelFiles = new List<ExcelFileInfo>();

    protected override void OnEnable()
    {
        base.OnEnable();
        titleContent = new GUIContent("ConfigTools");
        minSize = new Vector2(820, 520);
        ReadPath();
        RefreshExcelInfo();
    }

    [MenuItem("Tools/LuaConfig _F6")]
    private static void Init()
    {
        var window = GetWindow<ConfigTools>();
        window.titleContent = new GUIContent("ConfigTools");
        window.minSize = new Vector2(820, 520);
        window.Show();
    }

    private void OnXlsxFolderChanged()
    {
        xlsxFolder = ToProjectRelativePath(xlsxFolder);
        SavePath();
        RefreshExcelInfo();
    }

    private bool RunGenClientBat()
    {
        string projectRoot = GetProjectRoot();
        if (string.IsNullOrEmpty(projectRoot))
        {
            Debug.LogError("无法获取 Unity 项目根目录。");
            return false;
        }

        string batPath = Path.Combine(projectRoot, GenClientBatRelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!File.Exists(batPath))
        {
            Debug.LogError($"gen_client.bat 不存在: {batPath}");
            return false;
        }

        string workingDirectory = Path.GetDirectoryName(batPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{batPath}\"\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        using (Process process = new Process())
        {
            StringBuilder outputBuilder = new StringBuilder();
            StringBuilder errorBuilder = new StringBuilder();

            process.StartInfo = startInfo;
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    outputBuilder.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    errorBuilder.AppendLine(args.Data);
                }
            };

            if (!process.Start())
            {
                Debug.LogError("gen_client.bat 进程启动失败。");
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // gen_client.bat 末尾有 pause，写入回车避免后台执行卡住。
            process.StandardInput.WriteLine();
            process.StandardInput.Close();

            if (!process.WaitForExit(GenClientTimeoutMs))
            {
                process.Kill();
                Debug.LogError($"gen_client.bat 执行超时（{GenClientTimeoutMs / 1000} 秒）。\n{outputBuilder}\n{errorBuilder}");
                return false;
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Debug.LogError($"gen_client.bat 执行失败，ExitCode: {process.ExitCode}\n{outputBuilder}\n{errorBuilder}");
                return false;
            }

            string output = outputBuilder.ToString();
            string error = errorBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(output))
            {
                Debug.Log($"gen_client.bat 执行完成:\n{output}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning($"gen_client.bat 输出警告:\n{error}");
            }
        }

        return true;
    }

    private bool IsXlsxFolderInvalid()
    {
        return !HasValidXlsxFolder();
    }

    private bool HasValidXlsxFolder()
    {
        return !string.IsNullOrEmpty(xlsxFolder) && Directory.Exists(GetAbsoluteProjectPath(xlsxFolder));
    }

    private void SavePath()
    {
        EditorPrefs.SetString("xlsxFolder_" + PlayerSettings.applicationIdentifier, xlsxFolder);
    }

    private void ReadPath()
    {
        xlsxFolder = EditorPrefs.GetString("xlsxFolder_" + PlayerSettings.applicationIdentifier);
        if (string.IsNullOrEmpty(xlsxFolder))
        {
            xlsxFolder = DefaultXlsxFolderRelativePath;
        }

        xlsxFolder = ToProjectRelativePath(xlsxFolder);
    }

    private static string GetProjectRoot()
    {
        return Directory.GetParent(Application.dataPath)?.FullName;
    }

    private static bool IsExcelFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xls", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
    }

    private static string GetAbsoluteProjectPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalizedPath = NormalizePath(path);
        if (Path.IsPathRooted(normalizedPath))
        {
            return normalizedPath;
        }

        string projectRoot = GetProjectRoot();
        return string.IsNullOrEmpty(projectRoot)
            ? normalizedPath
            : NormalizePath(Path.Combine(projectRoot, normalizedPath));
    }

    private static string ToProjectRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalizedPath = NormalizePath(path);
        string projectRoot = NormalizePath(GetProjectRoot());
        if (string.IsNullOrEmpty(projectRoot) || !Path.IsPathRooted(normalizedPath))
        {
            return normalizedPath;
        }

        string absolutePath = NormalizePath(Path.GetFullPath(normalizedPath));
        string absoluteRoot = NormalizePath(Path.GetFullPath(projectRoot)).TrimEnd('/');

        if (absolutePath.Equals(absoluteRoot, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string rootPrefix = absoluteRoot + "/";
        return absolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            ? absolutePath.Substring(rootPrefix.Length)
            : absolutePath;
    }

    [Serializable]
    private class ExcelFileInfo
    {
        [TableColumnWidth(220)]
        [ReadOnly]
        [LabelText("表名")]
        public string Name;

        [ReadOnly]
        [LabelText("路径")]
        public string Path;

        [HideInInspector]
        private readonly string absolutePath;

        public ExcelFileInfo(string path)
        {
            absolutePath = NormalizePath(path);
            Path = ToProjectRelativePath(path);
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        [TableColumnWidth(90, false)]
        [Button("打开", ButtonSizes.Small)]
        private void Open()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = absolutePath,
                UseShellExecute = true
            });
        }
    }
}
