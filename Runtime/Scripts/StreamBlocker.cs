namespace UnityEngine.SceneManagement.Toolkit
{
	public abstract class StreamBlocker : MonoBehaviour
	{
		#region Variables

		protected bool m_blocking = true;

		#endregion

		#region Properties

		public bool blocking => m_blocking;

		#endregion

		#region Methods

		protected virtual void OnEnable()
		{
			this.WaitUntil(
				() => StreamManager.Exists,
				() => Register(StreamManager.Instance));
		}

		protected virtual void OnDisable()
		{
			if (StreamManager.Exists)
			{
				Unregister(StreamManager.Instance);
			}
		}

		protected virtual void Register(StreamManager manager)
		{
			StreamManager.Instance.Register(this);
		}

		protected virtual void Unregister(StreamManager manager)
		{
			StreamManager.Instance.Unregister(this);
		}

		#endregion
	}
}