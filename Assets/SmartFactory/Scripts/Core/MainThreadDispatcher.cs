using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace SmartFactory.Core
{
    /// <summary>
    /// 백그라운드 스레드에서 Unity 메인 스레드로 작업을 전달합니다.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        static MainThreadDispatcher s_instance;
        readonly ConcurrentQueue<Action> m_queue = new ConcurrentQueue<Action>();

        /// <summary>
        /// 싱글톤 인스턴스를 보장합니다.
        /// </summary>
        public static MainThreadDispatcher Instance
        {
            get
            {
                if (s_instance != null)
                {
                    return s_instance;
                }

                var go = new GameObject(nameof(MainThreadDispatcher));
                s_instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
                return s_instance;
            }
        }

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            while (m_queue.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        /// <summary>
        /// 메인 스레드에서 액션을 실행합니다.
        /// </summary>
        /// <param name="action">실행할 액션.</param>
        public void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            m_queue.Enqueue(action);
        }
    }
}
