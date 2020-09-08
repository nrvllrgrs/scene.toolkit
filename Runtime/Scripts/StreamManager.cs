using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using NaughtyAttributes;

namespace UnityEngine.SceneManagement.Toolkit
{
	public class StreamManager : Singleton<StreamManager>
	{
		#region Enumerator

		public enum LoadStreamMode
		{
			Default,
			AutoLoad,
			ManualLoad
		};

		#endregion

		#region Variables

		[SerializeField]
		private bool m_autoLoad = false;

		private Coroutine m_thread;
		private List<AsyncOperation> m_operations;

		[SerializeField, ReadOnly]
		private List<string> m_loadedSceneNames = new List<string>();

		private HashSet<StreamBlocker> m_blockers = new HashSet<StreamBlocker>();

		private string m_activeSceneName;
		private bool m_isLoading, m_isReady;
		private float m_value;

		#endregion

		#region Events

		[Header("Events")]

		public UnityEvent onStarted = new UnityEvent();
		public UnityEvent onProgress = new UnityEvent();
		public UnityEvent onReady = new UnityEvent();
		public UnityEvent onCompleted = new UnityEvent();

		#endregion

		#region Properties

		public bool isLoading
		{
			get { return m_isLoading; }
			private set
			{
				if (value == isLoading)
					return;

				m_isLoading = value;
				isActivated = isReady = false;

				if (value)
				{
					onStarted.Invoke();
				}
				else
				{
					onCompleted.Invoke();
				}
			}
		}

		public float value
		{
			get { return m_value; }
			private set
			{
				if (value == this.value)
					return;

				m_value = value;
				onProgress.Invoke();
			}
		}

		public bool isReady
		{
			get { return m_isReady; }
			private set
			{
				if (value == isReady)
					return;

				m_isReady = value;

				if (value)
				{
					onReady.Invoke();
				}
			}
		}

		public bool isActivated { get; set; }

		#endregion

		#region Methods

		private void OnEnable()
		{
			SceneManager.sceneLoaded += SceneLoaded;
		}

		private void OnDisable()
		{
			SceneManager.sceneLoaded -= SceneLoaded;
		}

		private void SceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
		{
			if (!string.IsNullOrWhiteSpace(m_activeSceneName) && scene.name == m_activeSceneName)
			{
				SceneManager.SetActiveScene(scene);
			}
		}

		public void AddScene(string sceneName, bool activeScene = false)
		{
			if (!m_loadedSceneNames.Contains(sceneName))
			{
				m_loadedSceneNames.Add(sceneName);
			}
		}

		public void Load(string sceneName, LoadStreamMode loadStreamMode = LoadStreamMode.Default)
		{
			Load(new[] { sceneName }, loadStreamMode);
		}

		public void Load(IEnumerable<string> sceneNames, LoadStreamMode loadStreamMode = LoadStreamMode.Default)
		{
			if (isLoading)
			{
				Debug.LogErrorFormat("Cannot load! Loading in progress.");
				return;
			}

			m_activeSceneName = null;
			m_thread = StartCoroutine(LoadThread(sceneNames));
		}

		[Button("Reset")]
		public void Reset(LoadStreamMode loadStreamMode = LoadStreamMode.Default)
		{
			if (isLoading)
			{
				Debug.LogErrorFormat("Cannot reset! Loading in progress.");
				return;
			}

			m_activeSceneName = SceneManager.GetActiveScene().name;
			m_thread = StartCoroutine(LoadThread(m_loadedSceneNames, loadStreamMode, true));
		}

		protected virtual IEnumerator LoadThread(IEnumerable<string> sceneNames, LoadStreamMode loadStreamMode = LoadStreamMode.Default, bool reset = false)
		{
			isLoading = true;
			m_operations = new List<AsyncOperation>();

			// Wait until all blockers are finished
			while (m_blockers.Any(x => x.blocking))
			{
				yield return null;
			}

			// Find scene names to be unloaded
			var unloadedSceneNames = reset
				? m_loadedSceneNames
				// Skip any scenes included in next state
				: m_loadedSceneNames.Except(sceneNames);

			// Unload previous scenes
			foreach (var sceneName in unloadedSceneNames.Distinct())
			{
				m_operations.Add(SceneManager.UnloadSceneAsync(sceneName));
			}

			// Remove null operations
			m_operations = m_operations.Where(x => x != null).ToList();

			// Wait until all scenes are unloaded
			while (m_operations.Any(x => !x.isDone))
			{
				value = CalcValue();
				yield return null;
			}
			value = 0.5f;

			// Clear operations
			m_operations = new List<AsyncOperation>();

			// Find scene names to be loaded
			var loadedSceneNames = reset
				? sceneNames
				// Skip any scenes included in previous state
				: sceneNames.Except(m_loadedSceneNames);

			// Start loading scenes
			foreach (var sceneName in loadedSceneNames)
			{
				if (SceneManager.GetSceneByName(sceneName) != null)
				{
					var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
					if ((!m_autoLoad && loadStreamMode == LoadStreamMode.Default) || loadStreamMode == LoadStreamMode.ManualLoad)
					{
						operation.allowSceneActivation = false;
					}

					m_operations.Add(operation);
				}
			}

			// Wait until all scenes are loaded
			while (m_operations.Any(x => !x.isDone))
			{
				value = CalcValue() + 0.5f;
				if (((!m_autoLoad && loadStreamMode == LoadStreamMode.Default) || loadStreamMode == LoadStreamMode.ManualLoad) && m_operations.All(x => x.progress >= 0.9f))
				{
					isReady = true;
					if (isActivated)
					{
						m_operations.ForEach(x => x.allowSceneActivation = true);
					}
				}

				yield return null;
			}
			value = 1f;

			// Remember loaded scenes
			m_loadedSceneNames = sceneNames.ToList();
			isLoading = false;
		}

		private float CalcValue()
		{
			if (m_operations == null || !m_operations.Any())
				return 0f;

			return m_operations.Average(x => x.progress) * 0.5f;
		}

		public void Register(StreamBlocker blocker)
		{
			m_blockers.Add(blocker);
		}

		public void Unregister(StreamBlocker blocker)
		{
			m_blockers.Remove(blocker);
		}

		#endregion
	}
}