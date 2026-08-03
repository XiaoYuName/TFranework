using Cysharp.Threading.Tasks;
using DamageNumbersPro;
using PathologicalGames;
using UnityEngine;
using XFramework;

namespace XFramework
{
    public class EffectsManager : MonoSingleton<EffectsManager>,IGameInitialized
    {
        #region DamageNumber

        /// <summary>
        /// 金币数
        /// </summary>
        public DamageNumberGUI coinDamageNumberGUI { get; private set; }

        /// <summary>
        /// 粉丝数
        /// </summary>
        public DamageNumberGUI fenDamageNumberGUI  { get; private set; }

        #endregion
        
        #region Initialized

        /// <summary>
        /// 初始化脚本函数
        /// </summary>
        /// <returns></returns>
        public async UniTask Initialized()
        {
            var coinPrefab =
                await AssetsManager.Instance.LoadAssetsUniTask<GameObject>(
                    AssetKeys.CoinNumberTexUGUIPath);

            coinDamageNumberGUI = coinPrefab.GetComponent<DamageNumberGUI>();

            var fenPrefab =
                await AssetsManager.Instance.LoadAssetsUniTask<GameObject>(
                    AssetKeys.FenNumberTexUGUIPath);

            fenDamageNumberGUI = fenPrefab.GetComponent<DamageNumberGUI>();
        }

        public async UniTask Release()
        {
            AssetsManager.Instance.FreeAsset(AssetKeys.CoinNumberTexUGUIPath);
            AssetsManager.Instance.FreeAsset(AssetKeys.FenNumberTexUGUIPath);
            await UniTask.CompletedTask;
        }

        #endregion
        

    }
}

