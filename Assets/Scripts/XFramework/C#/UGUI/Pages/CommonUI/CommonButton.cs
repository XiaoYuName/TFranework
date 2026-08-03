using System;
using Coffee.UIEffects;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

public class CommonButton : AGVButton
{
    private UIEffect _uiEffect;
    private Tweener _scaleTweener;
    
    [LabelText("缩放比例")]
    public float TweenerScale = 1.1f;

    private void Start()
    {
        _uiEffect = GetComponent<UIEffect>();
    }


    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        _uiEffect.edgeMode = EdgeMode.Plain;
        _scaleTweener?.Kill();
        _scaleTweener = transform.DOScale(TweenerScale, 0.1f).SetEase(Ease.OutQuad);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        _uiEffect.edgeMode = EdgeMode.None;
        _scaleTweener?.Kill();
        _scaleTweener = transform.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutQuad);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _uiEffect != null)
        {
            _uiEffect.edgeMode = EdgeMode.None;
            _scaleTweener = transform.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutQuad);
        }
    }
}
