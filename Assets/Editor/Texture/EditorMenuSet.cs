/// <summary>
/// 编辑器工具的菜单路径常量，集中管理。
///
/// 同类工具（贴图 / 2D 资源处理）以前散落在 Tools/2D 与 Tools/Texture 两个子菜单下，
/// 根源是菜单根路径在每个文件里各自硬编码。这里把根路径收敛成一个常量，
/// 各工具用 <c>EditorMenu.Texture2D + "/xxx"</c> 拼出完整路径 ——
/// const 字符串相加仍是编译期常量，可直接用于 [MenuItem(...)] 特性，
/// 以后整组改名只需改这一处。
/// </summary>
public static class EditorMenuSet
{
    /// <summary>贴图 / 2D 资源处理工具（漂白、对齐、九宫格收缩、裁剪等）统一归到此子菜单。</summary>
    public const string Texture2D = "Tools/2D";
}
