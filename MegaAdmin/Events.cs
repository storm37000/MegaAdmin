namespace MegaAdmin
{
	public interface IEventServerPreStart
	{
		void OnServerPreStart();
	}

	public interface IEventServerStart
	{
		void OnServerStart();
	}

	public interface IEventServerStop
	{
		void OnServerStop();
	}

	public interface IEventRoundEnd
	{
		void OnRoundEnd();
	}

	public interface IEventRoundStart
	{
		void OnRoundStart();
	}

	public interface IEventMatchStart
	{
		void OnMatchStart();
	}

	public interface IEventCrash
	{
		void OnCrash();
	}

	public interface IEventServerFull
	{
		void OnServerFull();
	}

	public interface IEventPlayerConnect
	{
		void OnPlayerConnect(string name);
	}

	public interface IEventPlayerDisconnect
	{
		void OnPlayerDisconnect(string name);
	}

	public interface IEventAdminAction
	{
		void OnAdminAction(string message);
	}

	public interface IEventConfigReload
	{
		void OnConfigReload();
	}
}
