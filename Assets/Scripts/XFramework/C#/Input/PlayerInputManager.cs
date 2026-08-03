using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using XFramework;

public class PlayerInputManager : MonoSingleton<PlayerInputManager>,IGameInitialized
{
    private PlayerInputActions input;
    
    public event Action OnSpace; 
    
    public event Action OnEsc;
    
    public event Action OnClick;
    public event Action OnLeftMouseDown;
    public event Action OnLeftMouseUp;
    public event Action OnRightClick;
    public event Action OnMiddleClick;

    public bool IsMouseLeftDown => input.Game.Click.IsPressed();

    /// <summary>方向：左（A / ←）</summary>
    public event Action OnLeft;

    /// <summary>方向：右（D / →）</summary>
    public event Action OnRight;

    /// <summary>方向：上（W / ↑）</summary>
    public event Action OnUp;

    /// <summary>方向：下（S / ↓，暂无使用，预留）</summary>
    public event Action OnDown;

    /// <summary>
    /// 初始化脚本函数
    /// </summary>
    /// <returns></returns>
    public async UniTask Initialized()
    {
        input = new ();
        input.Game.Enable();
        input.Game.Space.performed += OnSpaceInvoke;
        input.Game.Esc.performed += OnEscInvoke;
        input.Game.Click.performed += OnClickInvoke;
        input.Game.Click.started += OnLeftMouseDownInvoke;
        input.Game.Click.canceled += OnLeftMouseUpInvoke;
        input.Game.RightClick.performed += OnRightClickInvoke;
        input.Game.MiddleClick.performed += OnMiddleClickInvoke;
        input.Game.Left.performed += OnLeftInvoke;
        input.Game.Right.performed += OnRightInvoke;
        input.Game.Up.performed += OnUpInvoke;
        input.Game.Down.performed += OnDownInvoke;
        await UniTask.CompletedTask;
    }

    void OnSpaceInvoke(InputAction.CallbackContext context)
    {
        OnSpace?.Invoke();
    }

    void OnClickInvoke(InputAction.CallbackContext context)
    {
        OnClick?.Invoke();
    }
    void OnLeftMouseDownInvoke(InputAction.CallbackContext context)
    {
        OnLeftMouseDown?.Invoke();
    }
    void OnLeftMouseUpInvoke(InputAction.CallbackContext context)
    {
        OnLeftMouseUp?.Invoke();
    }
    void OnRightClickInvoke(InputAction.CallbackContext context)
    {
        OnRightClick?.Invoke();
    }
    void OnMiddleClickInvoke(InputAction.CallbackContext context)
    {
        OnMiddleClick?.Invoke();
    }
    void OnLeftInvoke(InputAction.CallbackContext context)
    {
        OnLeft?.Invoke();
    }
    void OnRightInvoke(InputAction.CallbackContext context)
    {
        OnRight?.Invoke();
    }
    void OnUpInvoke(InputAction.CallbackContext context)
    {
        OnUp?.Invoke();
    }
    void OnDownInvoke(InputAction.CallbackContext context)
    {
        OnDown?.Invoke();
    }
    void OnEscInvoke(InputAction.CallbackContext context)
    {
        OnEsc?.Invoke();
    }

    /// <summary>
    /// 释放脚本函数
    /// </summary>
    public async UniTask Release()
    {
        input.Game.Space.performed -= OnSpaceInvoke;
        input.Game.Esc.performed -= OnEscInvoke;
        input.Game.Click.performed -= OnClickInvoke;
        input.Game.Click.started -= OnLeftMouseDownInvoke;
        input.Game.Click.canceled -= OnLeftMouseUpInvoke;
        input.Game.RightClick.performed -= OnRightClickInvoke;
        input.Game.MiddleClick.performed -= OnMiddleClickInvoke;
        input.Game.Left.performed -= OnLeftInvoke;
        input.Game.Right.performed -= OnRightInvoke;
        input.Game.Up.performed -= OnUpInvoke;
        input.Game.Down.performed -= OnDownInvoke;
        input.Dispose();
        input = null;
        await UniTask.CompletedTask;
    }
}
