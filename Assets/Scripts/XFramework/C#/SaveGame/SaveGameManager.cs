using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 游戏存储管理器
    /// </summary>
    public class SaveGameManager : MonoSingleton<SaveGameManager>,IGameInitialized
    {
        #region Config
        /// <summary>
        /// 缓存存储Json文件的位置
        /// </summary>
        private static string JsonSavePath;

        /// <summary>
        /// 存档序列化设置
        /// </summary>
        private JsonSerializerSettings settings;

        /// <summary>
        /// 存档子目录名（保持原值，勿改，否则旧存档会失效）
        /// </summary>
        private const string SaveFolderName = "GameSaveData";

        /// <summary>
        /// 存档目录绝对路径
        /// </summary>
        private static string SaveDir => Path.Combine(JsonSavePath, SaveFolderName);

        /// <summary>
        /// 某个用户存档文件路径
        /// </summary>
        private static string GetUserPath(int userID) => Path.Combine(SaveDir, $"User{userID}.scriptable");

        /// <summary>
        /// 用户列表文件路径
        /// </summary>
        private static string UsersPath => Path.Combine(SaveDir, "Logic.scriptable");

        /// <summary>
        /// 确保存档目录存在
        /// </summary>
        private static void EnsureSaveDir()
        {
            if (!Directory.Exists(SaveDir))
                Directory.CreateDirectory(SaveDir);
        }
        #endregion

        #region User
        
        private List<ISaveable> iSaveables = new();
        [SerializeReference] 
        private GameSaveData curGameSaveData;
        

        #endregion

        #region  注册存档

        /// <summary>
        /// 注册函数将自身要存储的信息注册到ISaveablesList中
        /// </summary>
        /// <param name="saveable"></param>
        public void RegisterSaveable(ISaveable saveable)
        {
            if (!iSaveables.Contains(saveable))
            {
                iSaveables.Add(saveable);
            }
        }
        public void RemoveSaveable(ISaveable saveable)
        {
            iSaveables.Remove(saveable);
        }

        #endregion
        
        #region 保存用户数据

        public void Save()
        {
            //Save(Users[0]);
        }

        #endregion


        /// <summary>
        /// 初始化脚本函数
        /// </summary>
        /// <returns></returns>
        public async UniTask Initialized()
        {
            settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
            };
            JsonSavePath = Application.persistentDataPath;
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 释放脚本函数
        /// </summary>
        public async UniTask Release()
        {
            await UniTask.CompletedTask;
        }
    }
}

