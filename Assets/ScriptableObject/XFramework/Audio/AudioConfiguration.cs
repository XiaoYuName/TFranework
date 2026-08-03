using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace XFramework
{
    [CreateAssetMenu(fileName = "AudioConfiguration", menuName = "Configs/Audio/AudioConfiguration")]
    public class AudioConfiguration : OdinScriptableObject<AudioItemData>
    {
        [BoxGroup("混音器"), LabelText("混音器"), AssetList(Path = "AddressableAssets/")]
        public AudioMixer XMixer;

        [BoxGroup("混音器"), LabelText("混合组列表"), TableList(CellPadding = 3)]
        public List<AudioMixerGroupInfo> MixerGroupList;

        [BoxGroup("混音器"), LabelText("快照列表"), TableList(CellPadding = 3)]
        public List<AudioMixerSnapshotsInfo> MixerSnapshotsList;

        [BoxGroup("混音器"), LabelText("音乐预制体"),
         FilePath(ParentFolder = "Assets/AddressableAssets/Remote",Extensions =  "prefab")]
        public string MusicSourcePath;

        public AudioMixerGroup GetByMixerGroup(AudioMixerGroupType  type)
        {
            var temp = MixerGroupList.FindLast(temp =>
                temp.Type == type);
            if (temp != null)
                return temp.Group;
            return null;
        }
    }

    [System.Serializable]
    public class AudioItemData : OdinDataItem<AudioItemData>
    {
        [BoxGroup("Audio"), LabelText("唯一音频ID"), HorizontalGroup("Audio/Row1")]
        public string audioID;

        [BoxGroup("Audio"), LabelText("音效类型"), HorizontalGroup("Audio/Row1")]
        public AudioType audioType;

        [BoxGroup("Audio/Clip"), HideLabel,
         AssetList(Path = "AddressableAssets"), PreviewField(60, alignment: ObjectFieldAlignment.Left)]
        [HorizontalGroup("Audio/Clip/Row2")]
        public AudioClip audioClip;

        [BoxGroup("Audio/Clip"), Range(0.1f, 1f), LabelText("初始音量大小"),
         HorizontalGroup("Audio/Clip/Row2"), VerticalGroup("Audio/Clip/Row2/Init")]
        public float InitVolume = 1;

        [BoxGroup("Audio/Clip"), Range(0.1f, 1f), LabelText("随机PitchMin"),
         HorizontalGroup("Audio/Clip/Row2"), VerticalGroup("Audio/Clip/Row2/Init")]
        public float soundPitchMin = 0.8f;

        [BoxGroup("Audio/Clip"), Range(0.1f, 1.2f), LabelText("随机PitchMax"),
         HorizontalGroup("Audio/Clip/Row2"), VerticalGroup("Audio/Clip/Row2/Init")]
        public float soundPitchMax = 1.2f;

        public override string GetID()
        {
            return audioID;
        }
    }


    [System.Serializable]
    public class AudioMixerGroupInfo
    {
        [BoxGroup("混音器"), LabelText("类型"), EnumPaging, HorizontalGroup("混音器/R")]
        public AudioMixerGroupType Type;

        [BoxGroup("混音器"), LabelText("混合器"), AssetList(Path = "AddressableAssets/")] [HorizontalGroup("混音器/R")]
        public AudioMixerGroup Group;
    }

    [System.Serializable]
    public class AudioMixerSnapshotsInfo
    {
        [BoxGroup("混音器"), LabelText("类型"), EnumPaging, HorizontalGroup("混音器/R")]
        public AudioSnapshotsType Type;

        [BoxGroup("混音器"), LabelText("混合器"), AssetList(Path = "AddressableAssets/")]
        public AudioMixerSnapshot Snapshot;
    }

    /// <summary>
    /// Audio混合器混合组类型
    /// </summary>
    public enum AudioMixerGroupType
    {
        /// <summary>
        /// 主混合器
        /// </summary>
        Master = 0,

        /// <summary>
        /// 环境音效主混合器
        /// </summary>
        AmbientMaster = 1,

        /// <summary>
        /// 环境音效混合器
        /// </summary>
        AmbientItem = 2,

        /// <summary>
        /// BGM主混合器
        /// </summary>
        BGMMaster = 3,

        /// <summary>
        /// BGM混合器
        /// </summary>
        BGMItem = 4,

        /// <summary>
        /// Music特效总混合器
        /// </summary>
        MusicMaster = 5,

        /// <summary>
        /// Music特效混合器
        /// </summary>
        MusicItem = 6,

        /// <summary>
        /// 人声总混合器
        /// </summary>
        HumanMaster = 7,

        /// <summary>
        /// 人声混合器
        /// </summary>
        HumanItem = 8,

        /// <summary>
        /// 视频音效混合器
        /// </summary>
        VideoMaster = 9,

        /// <summary>
        /// 视频混合器
        /// </summary>
        VideoItem = 10,
    }

    /// <summary>
    /// Audio 混合器快照类型
    /// </summary>
    public enum AudioSnapshotsType
    {
        /// <summary>
        /// 默认都有音效
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 高人声,低其他音效
        /// </summary>
        Human = 1,

        /// <summary>
        /// Video 视频音效
        /// </summary>
        Video = 2,

        /// <summary>
        /// 全部停止
        /// </summary>
        Stop = 3,
    }
}

