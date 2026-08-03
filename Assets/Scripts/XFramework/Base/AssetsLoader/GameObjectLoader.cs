using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XFramework
{
    /// <summary>
    /// Unity 实例化GameObject 资源管理类
    /// </summary>
    public class GameObjectLoader: BaseLoader
    {
        /// <summary>
        /// 资源缓存列表
        /// </summary>
        private Stack<GameObject> caches = new Stack<GameObject>();

        /// <summary>
        /// 正在使用的列表
        /// </summary>
        private HashSet<GameObject> references = new HashSet<GameObject>();

        public GameObject prefab;

        public GameObjectLoader(string Key) : base(Key)
        {
            prefab = null;
        }

        public GameObjectLoader(GameObject prefab,string Key) : base(Key)
        {
            this.prefab = prefab;
            
        }

        public GameObject Instantiate(Transform parent)
        {
            GameObject obj = null;
            if (caches.Count > 0)
            {
                obj = caches.Pop();
                obj.transform.SetParent(parent, false);
                obj.SetActive(true);
            }
            else
            {
                obj = InstantiatePrefab(parent);
                obj.name = AssetName;
            }
            this.references.Add(obj);
            return obj;
        }

        public GameObject Instantiate()
        {
            if (caches.Count > 0)
            {
                var Obj = caches.Pop();
                this.references.Add(Obj);
                Obj.SetActive(true);
                return Obj;
            }
            
            if (this.prefab != null)
            {
                var obj = InstantiatePrefab();
                obj.name = AssetName;
                obj.SetActive(true);
                references.Add(obj);
                return obj;
            }
            else
            {
                this.prefab = base.Load<GameObject>();
                var obj = InstantiatePrefab();
                obj.SetActive(true);
                obj.name = AssetName;
                // 这里原来会立刻 base.Release():句柄一放,真机上 bundle 就可能被卸载,
                // 而 prefab 字段还在被后续实例化引用。现在句柄一直持有到真正释放时再放。
                references.Add(obj);
                return obj;
            }
        }

        public void InstantiateAsync(LoadCallBack<GameObject> Call)
        {
            if (caches.Count > 0)
            {
                var Obj = caches.Pop();
                this.references.Add(Obj);
                Obj.SetActive(true);
                Call?.Invoke(Obj);
                return;
            }
            
            if (prefab != null)
            {
                var obj = InstantiatePrefab();
                obj.name = AssetName;
                obj.SetActive(true);
                references.Add(obj);
                Call?.Invoke(obj);
                return;
            }

            base.LoadAsync<GameObject>((obj) =>
            {
                this.prefab = obj;
                var OBJ = InstantiatePrefab();
                OBJ.SetActive(true);
                OBJ.name = AssetName;
                // 同 Instantiate():句柄留到真正释放时再放,并且要记进 references
                references.Add(OBJ);
                Call?.Invoke(OBJ);
            });
        }

        public void Free(GameObject obj)
        {
            this.caches.Push(obj);
            this.references.Remove(obj);
            obj.transform.SetParent(AssetsManager.Instance.PoolRoot);
            obj.SetActive(false);
        }

        /// <summary>
        /// 彻底释放一个实例:直接Destroy掉,不回缓存池。
        /// 这个Loader再没有在用的实例时,缓存池里的备用实例也一并销毁,并把Addressables引用卸掉。
        /// </summary>
        /// <returns>true 表示这个Loader已经整个释放完,调用方应该把它从对象池字典里移除</returns>
        public bool ReleaseInstance(GameObject obj)
        {
            this.references.Remove(obj);
            if (obj != null)
            {
                Object.Destroy(obj);
            }

            if (this.references.Count > 0)
            {
                return false;
            }

            Release();
            return true;
        }

        /// <summary>
        /// 清掉缓存池;没有实例还在使用时,连Addressables引用一起卸掉。
        /// </summary>
        public override void Release()
        {
            DestroyCaches();
            if (this.references.Count <= 0)
            {
                this.prefab = null;
                base.Release();
            }
        }

        /// <summary>
        /// 销毁缓存池里的全部实例。必须把Stack弹空,
        /// 原来只是遍历Destroy没有清空,之后 Instantiate 会Pop出已销毁的对象。
        /// </summary>
        private void DestroyCaches()
        {
            while (this.caches.Count > 0)
            {
                GameObject cached = this.caches.Pop();
                if (cached != null)
                {
                    Object.Destroy(cached);
                }
            }
        }

        private GameObject InstantiatePrefab(Transform parent = null)
        {
#if UNITY_EDITOR
            if (AssetsManager.Instance.UseLocalAssetDatabase && this.prefab != null && AssetDatabase.Contains(this.prefab))
            {
                return PrefabUtility.InstantiatePrefab(this.prefab, parent) as GameObject;
            }
#endif
            return parent == null
                ? Object.Instantiate(this.prefab)
                : Object.Instantiate(this.prefab, parent);
        }
    }
}
