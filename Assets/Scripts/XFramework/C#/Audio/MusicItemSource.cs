using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PathologicalGames;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace XFramework
{
    public class MusicItemSource : GameBase
    {
        private AudioSource musicSource;

        private CancellationTokenSource _tokenSource;
        [ReadOnly,LabelText("播放结束回调")]
        public Action OnMusicPlayEnd;
        private void Awake()
        {
            musicSource = GetComponent<AudioSource>();
            musicSource.outputAudioMixerGroup = AudioManager.Instance.
                GetTypeMixerGroup(AudioMixerGroupType.MusicItem);
        }
        
        public void PlayMusic(AudioItemData audioItemData)
        {
            if (audioItemData == null)
            {
                OnMusicPlayEnd?.Invoke();
                return;
            }
            if (audioItemData.audioClip == null)
            {
                OnMusicPlayEnd?.Invoke();
                return;
            }
            if (_tokenSource != null)
            {
                _tokenSource.Cancel();
                _tokenSource = null;
            }
            _tokenSource = new CancellationTokenSource();
            musicSource.clip = audioItemData.audioClip;
            musicSource.volume = audioItemData.InitVolume;
            musicSource.pitch = UnityEngine.Random.Range(audioItemData.soundPitchMin, audioItemData.soundPitchMax);
            musicSource.Play();
            WaitMusic().Forget();
        }

        public void PlayMusic(AudioClip clip, float volume = 1f,Action OnMusicPlayEnd = null)
        {
            this.OnMusicPlayEnd = OnMusicPlayEnd;
            if (clip == null)
            {
                OnMusicPlayEnd?.Invoke();
                return;
            }
            if (_tokenSource != null)
            {
                _tokenSource.Cancel();
                _tokenSource = null;
            }
            _tokenSource = new CancellationTokenSource();
            musicSource.clip = clip;
            musicSource.volume = volume;
            musicSource.Play();
            WaitMusic().Forget();
        }

        private async UniTask WaitMusic()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(musicSource.clip.length),cancellationToken: _tokenSource.Token);
            OnMusicPlayEnd?.Invoke();
            musicSource.Stop();
            musicSource.clip = null;
            musicSource.volume = 0.5f;
            musicSource.pitch = 1f;
            _tokenSource = null;
            PoolManager.Pools["AudioManager"].Despawn(transform);
        }
    }
}

