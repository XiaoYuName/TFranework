using System;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 回调参数的回调时机
    /// </summary>
    public enum ActionBehaviour
    {
        /// <summary>
        /// 一开始调用
        /// </summary>
        Star,
        /// <summary>
        /// 中途回调
        /// </summary>
        Mid,
        /// <summary>
        /// 结束时回调执行
        /// </summary>
        End,
    }

    public enum GameSettingType
    {
        /// <summary>
        /// 游戏设置
        /// </summary>
        [LabelText("游戏设置")]
        GameSettings = 0,
        /// <summary>
        /// 显示设置
        /// </summary>
        [LabelText("显示设置")]
        DisplaySettings = 1,
        /// <summary>
        /// 声音设置
        /// </summary>
        [LabelText("声音设置")]
        AudioSettings = 2,
        /// <summary>
        /// 其他设置
        /// </summary>
        [LabelText("其他设置")]
        OtherSettings = 3,
    }

    public enum PhotoLabelType
    {
        [LabelText("事件CG")]
        ActionCG = 0,
        [LabelText("HCG")]
        HCG = 1,
        [LabelText("照片")]
        Photograph = 2,
    }

    public enum ItemSortType
    {
        CreatTime = 0,
        Number =  1,
        Quality = 2,
    }

    
    public enum ShopMode
    {
        [LabelText("购买界面")]
        Buy = 0,
        [LabelText("出售界面")]
        Sell = 1,
    }

    public enum StateType
    {
        None = 0,
        Lock = 1,
        Unlock = 2,
    }

    public enum MinGameSceneType
    {
        /// <summary>
        /// 娃娃机游戏场景
        /// </summary>
        ClawMachineScene = 0,
        /// <summary>
        /// 展会准备场景
        /// </summary>
        ExhibitionPrepareScene = 1,
        /// <summary>
        /// 展会游戏场景
        /// </summary>
        ExhibitionGameScene = 2,
        
        /// <summary>
        /// 宝石切割小游戏
        /// </summary>
        GemSmartSlicerScene = 3,
    }

    /// <summary>
    /// 转场渐变遮罩的遮挡范围。对应 Project Settings 里两个专门的 Sorting Layer。
    /// </summary>
    public enum FadeLayer
    {
        /// <summary>
        /// SceneFade:只遮住场景,UI照常显示(小场景之间切换用)
        /// </summary>
        Scene = 0,
        /// <summary>
        /// UIFade:最顶层,连UI一起遮掉(进出小游戏、读档这种整体转场用)
        /// </summary>
        All = 1,
    }

    public enum OnLinePageType
    {
        /// <summary>
        /// 无
        /// </summary>
        [LabelText("Node")]
        None = 0,
        [LabelText("发布动态")]
        PostingUpdates = 2,
        [LabelText("粉丝互动")]
        PrivateMessage = 3,
        [LabelText("展会信息界面")]
        Exhibition = 4,
        [LabelText("服装准备")]
        Clothing = 5,
        [LabelText("展会曝光")]
        ExhibitionPromotion,
    }

    
    public enum BagType
    {
        [LabelText("空袋")]
        None = 0,
        [LabelText("纸袋")]
        PaperBag = 1,
        [LabelText("塑料袋")]
        PlasticBag = 2,
    }
}
