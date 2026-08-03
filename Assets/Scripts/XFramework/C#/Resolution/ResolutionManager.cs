using System;
using System.Collections;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;


public class ResolutionManager : MonoSingleton<ResolutionManager>,IGameInitialized
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    private const int GWL_STYLE = -16;

    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;

    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_FRAMECHANGED = 0x0020;

#endif
    
    [BoxGroup("屏幕设置"),LabelText("当前屏幕设置")]
    public WindowType SelectedWindowType { get; private set; }
    [BoxGroup("屏幕设置"),LabelText("当前屏幕分辨率索引")]
    public int SelectedWindowResolutionIndex { get; private set; }

    /// <summary>
    /// 初始化脚本函数
    /// </summary>
    /// <returns></returns>
    public async UniTask Initialized()
    {
        var windowType = PlayerPrefs.GetInt("WindowType", (int)0);
        SelectedWindowType = (WindowType)windowType;
        SelectedWindowResolutionIndex = PlayerPrefs.GetInt("WindowResolutionIndex", Screen.resolutions.Length -1);
        Debug.Log($"保存本地的窗口模式: {SelectedWindowType} 保存到本地的分辨率 : {Screen.resolutions[SelectedWindowResolutionIndex]}");
        
        ChangeWindowMode(SelectedWindowType, SelectedWindowResolutionIndex);
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// 释放脚本函数
    /// </summary>
    public async UniTask Release()
    {
        await UniTask.CompletedTask;
    }

    public void ChangeWindowMode(WindowType mode, int resolutionIndex)
    {
        SelectedWindowType = mode;
        SelectedWindowResolutionIndex = resolutionIndex;
        PlayerPrefs.SetInt("WindowType", (int)mode);
        PlayerPrefs.SetInt("WindowResolutionIndex", resolutionIndex);
        Debug.Log($"保存本地的窗口模式: {SelectedWindowType} 保存到本地的分辨率 : {Screen.resolutions[SelectedWindowResolutionIndex]}");
        ChangeWindowMode(SelectedWindowType,Screen.resolutions[SelectedWindowResolutionIndex].width,
            Screen.resolutions[SelectedWindowResolutionIndex].height);
    }

    private void ChangeWindowMode(WindowType mode, int width, int height)
    {
        switch (mode)
        {
            case WindowType.Fullscreen:
                Screen.SetResolution(width, height, true);
                break;

            case WindowType.Borderless:
                StartCoroutine(SetBorderless(width, height));
                break;

            case WindowType.Windowed:
                StartCoroutine(SetWindowed(width, height));
                break;
        }
    }

    private IEnumerator SetBorderless(int width, int height)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr hwnd = GetActiveWindow();
        GetWindowPosition(hwnd, out int currentX, out int currentY);
#endif

        Screen.SetResolution(width, height, false);

        yield return null;
        yield return null;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        hwnd = GetActiveWindow();

        int style = GetWindowLong(hwnd, GWL_STYLE);

        style &= ~WS_CAPTION;
        style &= ~WS_THICKFRAME;
        style &= ~WS_MINIMIZEBOX;
        style &= ~WS_MAXIMIZEBOX;
        style &= ~WS_SYSMENU;

        SetWindowLong(hwnd, GWL_STYLE, style);

        SetWindowPos(
            hwnd,
            HWND_TOP,
            currentX,
            currentY,
            width,
            height,
            SWP_SHOWWINDOW | SWP_FRAMECHANGED);
#endif
    }

    private IEnumerator SetWindowed(int width, int height)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr hwnd = GetActiveWindow();
        GetWindowPosition(hwnd, out int currentX, out int currentY);
#endif

        Screen.SetResolution(width, height, false);

        yield return null;
        yield return null;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        hwnd = GetActiveWindow();

        int style = GetWindowLong(hwnd, GWL_STYLE);

        style |= WS_CAPTION;
        style |= WS_SYSMENU;
        style |= WS_MINIMIZEBOX;

        // 禁用拖拽边框缩放
        style &= ~WS_THICKFRAME;
        style &= ~WS_MAXIMIZEBOX;

        SetWindowLong(hwnd, GWL_STYLE, style);

        SetWindowPos(
            hwnd,
            HWND_TOP,
            currentX,
            currentY,
            width,
            height,
            SWP_SHOWWINDOW | SWP_FRAMECHANGED);
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private void GetWindowPosition(IntPtr hwnd, out int x, out int y)
    {
        x = 0;
        y = 0;

        if (GetWindowRect(hwnd, out RECT rect))
        {
            x = rect.Left;
            y = rect.Top;
        }
    }
#endif
}

public enum WindowType
{
    Fullscreen ,
    Borderless,
    Windowed,
}