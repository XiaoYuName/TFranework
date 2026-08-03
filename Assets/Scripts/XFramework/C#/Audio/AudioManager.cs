using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PathologicalGames;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace XFramework
{
    /// <summary>
    /// 音效总管理器
    /// </summary>
    public class AudioManager : MonoOdinSingleton<AudioManager>,IGameInitialized
    {
        #region IGameInitialized

        [BoxGroup("Initialized"),LabelText("配置路径"),FilePath]
        public string ConfigPath = "Assets/AddressableAssets/Configs/UIPage/PageConfiguration.asset";
        [BoxGroup("Initialized"),ShowInInspector,LabelText("配置表数据"),ReadOnly]
        private AudioConfiguration _audioConfiguration;
        
        public async UniTask Initialized()
        {
            _audioConfiguration = await AssetsManager.Instance.
                    LoadAssetsUniTask<AudioConfiguration>(ConfigPath);
            InitializedComplete();
            InitializedSnapshot();
            await InitializedMusicSource();
        }

        private void InitializedComplete()
        {
            XMixer = _audioConfiguration.XMixer;
            bgmSource = transform.Find("BgmSource").GetComponent<AudioSource>();
            ambientSource = transform.Find("AmbientSource").GetComponent<AudioSource>();
            humanSource = transform.Find("HumanSource").GetComponent<AudioSource>();
            videoSource = transform.Find("VideoSource").GetComponent<AudioSource>();
            
            bgmSource.outputAudioMixerGroup =  _audioConfiguration.GetByMixerGroup(AudioMixerGroupType.BGMItem);
            ambientSource.outputAudioMixerGroup =  _audioConfiguration.GetByMixerGroup(AudioMixerGroupType.AmbientItem);
            humanSource.outputAudioMixerGroup =  _audioConfiguration.GetByMixerGroup(AudioMixerGroupType.HumanItem);
            videoSource.outputAudioMixerGroup =  _audioConfiguration.GetByMixerGroup(AudioMixerGroupType.VideoItem);
            
            SetAudioVolume(AudioMixerGroupType.Master,PlayerPrefs.GetFloat("MasterVolume", 0.5f));
            SetAudioVolume(AudioMixerGroupType.BGMItem,PlayerPrefs.GetFloat("BGMItemVolume", 0.5f));
            SetAudioVolume(AudioMixerGroupType.MusicItem,PlayerPrefs.GetFloat("MusicItemVolume", 0.5f));
            SetAudioVolume(AudioMixerGroupType.HumanItem,PlayerPrefs.GetFloat("HumanItemVolume", 0.5f));
        }

        private async UniTask InitializedMusicSource()
        {
            var source =
                 await AssetsManager.Instance.LoadAssetsUniTask<GameObject>(_audioConfiguration.MusicSourcePath);
            MusicSource = source.GetComponent<AudioSource>();

            GameObject newRoot = new GameObject("AudioPoolRoot");
            newRoot.transform.SetParent(transform);
            newRoot.transform.localPosition = Vector3.zero;
            
            audioSpawnPool = PoolManager.Pools.Create("AudioManager",newRoot);
            PrefabPool audioPool = new PrefabPool(MusicSource.transform)
            {
                preloadAmount = 20,
            };
            audioSpawnPool.CreatePrefabPool(audioPool);
        }

        private void InitializedSnapshot()
        {
            foreach (var item in _audioConfiguration.MixerSnapshotsList)
            {
                if (!snapshots.ContainsKey(item.Type))
                {
                    snapshots.Add(item.Type, item.Snapshot);
                }
                else
                {
                    Debug.LogWarning("重复快照,将被跳过!");
                }
            }
        }

        public UniTask Release()
        {
            return UniTask.CompletedTask;
        }

        #endregion

        #region Complete

        [ReadOnly,ShowInInspector,BoxGroup("Sources"),LabelText("背景播放器")]
        private AudioSource bgmSource;
        
        [ReadOnly,ShowInInspector,BoxGroup("Sources"),LabelText("环境播放器")]
        private AudioSource ambientSource;
        
        [ReadOnly,ShowInInspector,BoxGroup("Sources"),LabelText("人声播放器")]
        private AudioSource humanSource;
        
        [ReadOnly,ShowInInspector,BoxGroup("Sources"),LabelText("UI播放器")]
        private AudioSource videoSource;
        
        [ReadOnly,ShowInInspector,BoxGroup("Sources"),LabelText("音效播放器")]
        private AudioSource MusicSource;
        
        private SpawnPool audioSpawnPool;

        [BoxGroup("Snapshot"),ShowInInspector,ReadOnly,LabelText("快照列表")]
        private Dictionary<AudioSnapshotsType, AudioMixerSnapshot> snapshots = new Dictionary<AudioSnapshotsType, AudioMixerSnapshot>();

        [BoxGroup("Snapshot"),ShowInInspector,LabelText("快照过度时间"),ReadOnly]
        private const float snapshotTimer = 3f;
        
        [BoxGroup("混音器"),ShowInInspector,LabelText("混音器"),ReadOnly]
        private AudioMixer XMixer;
        
        #endregion
        
        #region Play

        /// <summary>
        /// 播放音频
        /// </summary>
        /// <param name="audioID">audio配置表ID</param>
        /// <param name="transitionTime">过度时间</param>
        public void PlayAudio(string audioID, float transitionTime = snapshotTimer)
        {
            AudioItemData itemData = _audioConfiguration.GetDataByID(audioID);
            switch (itemData.audioType)
            {
                case AudioType.BGM:
                    PlayBGM(itemData, transitionTime);
                    break;
                case AudioType.Ambient:
                    PlayAmbient(itemData,transitionTime);
                    break;
                case AudioType.Human:
                    PlayHuman(itemData,transitionTime);
                    break;
                case AudioType.Music:
                    PlayMusic(itemData,transitionTime);
                    break;
                case AudioType.Video:
                    PlayVideo(itemData,transitionTime);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        ///  播放音频
        /// </summary>
        /// <param name="audioPath"> 音频路径</param>
        /// <param name="audioType">音频类型</param>
        /// <param name="transitionTime">过度时间</param>
        public void PlayAudio(string audioPath,AudioType audioType,float transitionTime = snapshotTimer)
        {
            if (string.IsNullOrEmpty(audioPath)) return;
            switch (audioType)
            {
                case AudioType.BGM:
                    PlayBGM(audioPath, transitionTime);
                    break;
                case AudioType.Ambient:
                    PlayAmbient(audioPath, transitionTime);
                    break;
                case AudioType.Human:
                    PlayHuman(audioPath, transitionTime);
                    break;
                case AudioType.Music:
                    PlayMusic(audioPath, transitionTime);
                    break;
                case AudioType.Video:
                    PlayVideo(audioPath, transitionTime);
                    break;
            }
        }
        
        /// <summary>
        ///  播放音频
        /// </summary>
        /// <param name="clip">音频Clip</param>
        /// <param name="audioType">音频类型</param>
        /// <param name="transitionTime">过度时间</param>
        public void PlayAudio(AudioClip clip,AudioType audioType,float transitionTime = snapshotTimer)
        {
            if (clip == null) return;
            switch (audioType)
            {
                case AudioType.BGM:
                    PlayBGM(clip, transitionTime);
                    break;
                case AudioType.Ambient:
                    PlayAmbient(clip, transitionTime);
                    break;
                case AudioType.Human:
                    PlayHuman(clip, transitionTime);
                    break;
                case AudioType.Music:
                    PlayMusic(clip, transitionTime);
                    break;
                case AudioType.Video:
                    PlayVideo(clip, transitionTime);
                    break;
            }
        }
        
        public void StopAudio(AudioType audioType)
        {
            
        }

        #endregion

        #region BGM
        
        /// <summary>
        /// 播放BGM
        /// </summary>
        /// <param name="itemData">配置数据</param>
        /// <param name="transitionTime">过度时间</param>
        private void PlayBGM(AudioItemData itemData, float transitionTime = snapshotTimer)
        {
            if (itemData == null) return;
            if (itemData.audioClip == null) return;
            PlayBGM(itemData.audioClip, transitionTime);
        }

        /// <summary>
        ///  播放BGM
        /// </summary>
        /// <param name="audioPath">音频路径</param>
        /// <param name="transitionTime">过度时间</param>
        public void PlayBGM(string audioPath, float transitionTime = snapshotTimer)
        {
            if (string.IsNullOrEmpty(audioPath)) return;
            AudioClip clip = AssetsManager.Instance.LoadAssets<AudioClip>(audioPath);
            if (clip == null) return;
            PlayBGM(clip, transitionTime);
        }
        
        /// <summary>
        ///  播放BGM
        /// </summary>
        /// <param name="clip">音频文件</param>
        /// <param name="transitionTime">过度时间</param>
        public void PlayBGM(AudioClip clip, float transitionTime = snapshotTimer)
        {
            if (clip == null) return;
            if (bgmSource.isActiveAndEnabled)
            {
                bgmSource.clip = clip;
                bgmSource.loop = true;
                bgmSource.Play();
            }
            if (snapshots.ContainsKey(AudioSnapshotsType.Normal))
            {
                snapshots[AudioSnapshotsType.Normal].TransitionTo(transitionTime);
            }
        }

        /// <summary>
        /// 暂停BGM
        /// </summary>
        public void PauseBGM()
        {
            bgmSource.Pause();
        }

        /// <summary>
        /// 恢复播放BGM
        /// </summary>
        public void ResumeBGM()
        {
            bgmSource.UnPause();
        }

        /// <summary>
        /// 停止播放BGM音效
        /// </summary>
        public void StopBGM()
        {
            bgmSource.Stop();
        }

        #endregion

        #region Ambient

        /// <summary>
        /// 播放环境音
        /// </summary>
        /// <param name="itemData"> 配置数据</param>
        /// <param name="transitionTime"> 过度时间</param>
        private void PlayAmbient(AudioItemData itemData, float transitionTime = snapshotTimer)
        {
            if (itemData == null) return;
            if (itemData.audioClip == null) return;
            PlayAmbient(itemData.audioClip, transitionTime);
        }
        
        /// <summary>
        ///  播放环境音
        /// </summary>
        /// <param name="audioPath">音频路径</param>
        /// <param name="transitionTime">过度时间</param>
        private void PlayAmbient(string audioPath, float transitionTime = snapshotTimer)
        {
            if (string.IsNullOrEmpty(audioPath)) return;
            AudioClip clip = AssetsManager.Instance.LoadAssets<AudioClip>(audioPath);
            if (clip == null) return;
            PlayAmbient(clip, transitionTime);
        }
        
        /// <summary>
        ///  播放环境音
        /// </summary>
        /// <param name="clip"> 音频Clip</param>
        /// <param name="transitionTime"> 过度时间</param>
        private void PlayAmbient(AudioClip clip, float transitionTime = snapshotTimer)
        {
            if (clip == null) return;
            if (ambientSource.isActiveAndEnabled)
            {
                ambientSource.clip = clip;
                ambientSource.loop = true;
                ambientSource.Play();
            }
            // if (snapshots.ContainsKey(AudioSnapshotsType.Normal))
            // {
            //     snapshots[AudioSnapshotsType.Normal].TransitionTo(SnapshotTimer);
            // }
        }

        /// <summary>
        /// 停止播放环境音
        /// </summary>
        public void StopAmbient()
        {
            ambientSource.Stop();
        }

        #endregion
        
        #region Human
        /// <summary>
        ///  播放人声
        /// </summary>
        /// <param name="itemData">配置数据</param>
        /// <param name="transitionTime">过度时间</param>
        private void PlayHuman(AudioItemData itemData, float transitionTime = snapshotTimer)
        {
            if (itemData == null) return;
            if (itemData.audioClip == null) return;
            PlayHuman(itemData.audioClip, transitionTime);
        }
        
        /// <summary>
        ///  播放人声
        /// </summary>
        /// <param name="audioPath">音频路径</param>
        /// <param name="transitionTime">过度时间</param>
        private void PlayHuman(string audioPath, float transitionTime = snapshotTimer)
        {
            if (string.IsNullOrEmpty(audioPath)) return;
            AudioClip clip = AssetsManager.Instance.LoadAssets<AudioClip>(audioPath);
            if (clip == null) return;
            PlayHuman(clip, transitionTime);
        }
        
        /// <summary>
        /// 播放人声
        /// </summary>
        /// <param name="clip"> 音频Clip</param>
        /// <param name="transitionTime"> 过度时间</param>
        private void PlayHuman(AudioClip clip, float transitionTime = snapshotTimer)
        {
            if (clip == null) return;
            if (humanSource.isActiveAndEnabled)
            {
                humanSource.clip = clip;
                humanSource.Play();
            }
            if (snapshots.ContainsKey(AudioSnapshotsType.Human))
            {
                snapshots[AudioSnapshotsType.Human].TransitionTo(transitionTime);
            }
        }
        
        public void StopHuman()
        {
            humanSource.Stop();
        }
        #endregion

        #region Video
        
        /// <summary>
        /// 播放视频
        /// </summary>
        /// <param name="itemData"> 配置数据</param>
        /// <param name="transitionTime"> 过度时间</param>
        private void PlayVideo(AudioItemData itemData, float transitionTime = snapshotTimer)
        {
            if (itemData == null) return;
            if (itemData.audioClip == null) return;
            PlayVideo(itemData.audioClip, transitionTime);
        }
        
        /// <summary>
        /// 播放视频
        /// </summary>
        /// <param name="audioPath">音频路径</param>
        /// <param name="transitionTime">过度时间</param>
        private void PlayVideo(string audioPath, float transitionTime = snapshotTimer)
        {
            if (string.IsNullOrEmpty(audioPath)) return;
            AudioClip clip = AssetsManager.Instance.LoadAssets<AudioClip>(audioPath);
            if (clip == null) return;
            PlayVideo(clip, transitionTime);
        }
        
        /// <summary>
        /// 播放视频
        /// </summary>
        /// <param name="clip">音频文件</param>
        /// <param name="transitionTime"> 过度时间</param>
        private void PlayVideo(AudioClip clip, float transitionTime = snapshotTimer)
        {
            if (clip == null) return;
            if (videoSource.isActiveAndEnabled)
            {
                videoSource.clip = clip;
                videoSource.Play();
            }

            if (snapshots.ContainsKey(AudioSnapshotsType.Video))
            {
                snapshots[AudioSnapshotsType.Video].TransitionTo(transitionTime);
            }

        }
        
        public void StopVideo()
        {
            videoSource.Stop();
        }

        #endregion

        #region Music
        
        private void PlayMusic(AudioItemData itemData,float transitionTime = snapshotTimer)
        {
            if (itemData == null) return;
            if (itemData.audioClip == null) return;
            PlayMusic(itemData.audioClip);
        }
        
        private void PlayMusic(string audioPath,float transitionTime = snapshotTimer)
        {
            if (string.IsNullOrEmpty(audioPath)) return;
            AudioClip clip = AssetsManager.Instance.LoadAssets<AudioClip>(audioPath);
            if (clip == null) return;
            PlayMusic(clip);
        }
        
        public void PlayMusic(AudioClip clip,float volume = 1f,Action OnMusicPlayEnd = null)
        { 
            if (clip == null) return;
            Transform temp = audioSpawnPool.Spawn(MusicSource.transform);
            MusicItemSource  musicItemSource = temp.GetComponent<MusicItemSource>();
            musicItemSource.PlayMusic(clip,volume, OnMusicPlayEnd);
        }

        #endregion

        #region Functions

        /// <summary>
        /// 设置音频音量
        /// </summary>
        /// <param name="audioType">音频组类型</param>
        /// <param name="volume">音频组值</param>
        public void SetAudioVolume(AudioMixerGroupType audioType, float volume)
        {
            switch (audioType)
            {
                case AudioMixerGroupType.Master:
                    PlayerPrefs.SetFloat("MasterVolume", volume);
                    SetMixerVolume("MasterVolume", volume);
                    break;
                case AudioMixerGroupType.AmbientMaster:
                    PlayerPrefs.SetFloat("AmbientMasterVolume", volume);
                    SetMixerVolume("AmbientMasterVolume", volume);
                    break;
                case AudioMixerGroupType.AmbientItem:
                    PlayerPrefs.SetFloat("AmbientItemVolume", volume);
                    SetMixerVolume("AmbientItemVolume", volume);
                    break;
                case AudioMixerGroupType.BGMMaster:
                    PlayerPrefs.SetFloat("BGMasterVolume", volume);
                    SetMixerVolume("BGMasterVolume", volume);
                    break;
                case AudioMixerGroupType.BGMItem:
                    PlayerPrefs.SetFloat("BGMItemVolume", volume);
                    SetMixerVolume("BGMItemVolume", volume);
                    break;
                case AudioMixerGroupType.MusicMaster:
                    PlayerPrefs.SetFloat("MusicMasterVolume", volume);
                    SetMixerVolume("MusicMasterVolume", volume);
                    break;
                case AudioMixerGroupType.MusicItem:
                    PlayerPrefs.SetFloat("MusicItemVolume", volume);
                    SetMixerVolume("MusicItemVolume", volume);
                    break;
                case AudioMixerGroupType.HumanMaster:
                    PlayerPrefs.SetFloat("HumanMasterVolume", volume);
                    SetMixerVolume("HumanMasterVolume", volume);
                    break;
                case AudioMixerGroupType.HumanItem:
                    PlayerPrefs.SetFloat("HumanItemVolume", volume);
                    SetMixerVolume("HumanItemVolume", volume);
                    break;
                case AudioMixerGroupType.VideoMaster:
                    PlayerPrefs.SetFloat("VideoMasterVolume", volume);
                    SetMixerVolume("VideoMasterVolume", volume);
                    break;
                case AudioMixerGroupType.VideoItem:
                    PlayerPrefs.SetFloat("VideoItemVolume", volume);
                    SetMixerVolume("VideoItemVolume", volume);
                    break;
                default:
                    break;
            }
        }
        
        /// <summary>
        /// 设置混合器音量
        /// </summary>
        private void SetMixerVolume(string mixerName, float value)
        {
            XMixer.SetFloat(mixerName, ConvertMixerVolume(value));
        }
        
        /// <summary>
        /// 将0~1的音效转换为音阶值
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        private float ConvertMixerVolume(float amount)
        {
            return (amount * 100 - 80);
        }

        #endregion

        #region Tools

        /// <summary>
        /// 获取对应类型的混合器组件
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public AudioMixerGroup GetTypeMixerGroup(AudioMixerGroupType type)
        {
            return _audioConfiguration.GetByMixerGroup(type);
        }

        #endregion
        
    }
}

