using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using XFramework;

public class AGVButton : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,IPointerClickHandler
{
    #region UnityEvent
    
    [LabelText("鼠标移入事件"),FoldoutGroup("事件")]
    public UnityEvent OnEnter;

    [LabelText("鼠标移出事件"),FoldoutGroup("事件")]
    public UnityEvent OnExit;
    
    [LabelText("鼠标点击事件"),FoldoutGroup("事件")]
    public UnityEvent OnClick;

    [LabelText("鼠标抬起事件"),FoldoutGroup("事件")]
    public UnityEvent OnPointUp;

    [LabelText("鼠标按下事件"),FoldoutGroup("事件")]
    public UnityEvent OnPointDown;

    #endregion

    #region DoTween
    [FoldoutGroup("Tween动画"),LabelText("鼠标移入")]
    public DOTweenAnimation OnPointEnterTween;

    [FoldoutGroup("Tween动画"),LabelText("鼠标移出")]
    public DOTweenAnimation OnPointExitTween;
    
    [FoldoutGroup("Tween动画"),LabelText("鼠标点击")]
    public DOTweenAnimation OnPointClickTween;
    
    [FoldoutGroup("Tween动画"),LabelText("鼠标按下")]
    public DOTweenAnimation OnPointClickDownTween;
    
    [FoldoutGroup("Tween动画"),LabelText("鼠标抬起")]
    public DOTweenAnimation OnPointClickUpTween;
    
    #endregion

    #region Animation
    [FoldoutGroup("动画"),LabelText("动画控制器")]
    public Animator animator;
    [FoldoutGroup("动画"),LabelText("鼠标移入")]
    public AnimationClip OnPointEnter;
    [FoldoutGroup("动画"),LabelText("鼠标移出")]
    public AnimationClip OnPointExit;
    [FoldoutGroup("动画"),LabelText("鼠标点击")]
    public AnimationClip OnPointClick;
    [FoldoutGroup("动画"),LabelText("鼠标按下")]
    public AnimationClip OnPointClickDown;
    [FoldoutGroup("动画"),LabelText("鼠标抬起")]
    public AnimationClip OnPointClickUp;
    
    #endregion

    #region AudioClip
    [FoldoutGroup("音频"),LabelText("是否有移入音频")]
    public bool isPointEnterClip;
    [ShowIf("isPointEnterClip"),LabelText("鼠标移入音频"),FoldoutGroup("音频"),AssetList]
    public AudioClip OnPointEnterClip;
    
    [FoldoutGroup("音频"),LabelText("是否有移出音频")]
    public bool isPointExitClip;
    [ShowIf("isPointExitClip"),LabelText("鼠标移出音频"),FoldoutGroup("音频"),AssetList]
    public AudioClip OnPointExitClip;
    
    [FoldoutGroup("音频"),LabelText("是否有点击音频")]
    public bool isPointClickClip;
    [ShowIf("isPointClickClip"),LabelText("鼠标点击音频"),FoldoutGroup("音频"),AssetList]
    public AudioClip OnPointClickClip;
    
    [FoldoutGroup("音频"),LabelText("是否有按下音频")]
    public bool isPointClickDownClip;
    [ShowIf("isPointClickDownClip"),LabelText("鼠标按下音频"),FoldoutGroup("音频"),AssetList]
    public AudioClip OnPointClickDownClip;
    
    [FoldoutGroup("音频"),LabelText("是否有抬起音频")]
    public bool isPointClickUpClip;
    [ShowIf("isPointClickUpClip"),LabelText("鼠标抬起音频"),FoldoutGroup("音频"),AssetList]
    public AudioClip OnPointClickUpClip;

    #endregion


    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        OnEnter?.Invoke();
        if (isPointEnterClip)
        {
            AudioManager.Instance.PlayAudio(OnPointEnterClip,AudioType.Music);
        }

        OnPointEnterTween?.DOPlay();
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        OnExit?.Invoke();
        if (isPointExitClip)
        {
            AudioManager.Instance.PlayAudio(OnPointExitClip,AudioType.Music);
        }
        
        OnPointExitTween?.DOPlay();
    }

    public virtual void OnPointerDown(PointerEventData eventData)
    {
        OnPointDown?.Invoke();
        if (isPointClickDownClip)
        {
            AudioManager.Instance.PlayAudio(OnPointClickDownClip,AudioType.Music);
        }
        
        OnPointClickDownTween?.DOPlay();
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
        OnPointUp?.Invoke();
        if (isPointClickUpClip)
        {
            AudioManager.Instance.PlayAudio(OnPointClickUpClip,AudioType.Music);
        }
        
        OnPointClickUpTween?.DOPlay();
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
        if (isPointClickClip)
        {
            AudioManager.Instance.PlayAudio(OnPointClickClip,AudioType.Music);
        }
        
        OnPointClickTween?.DOPlay();
    }
}
