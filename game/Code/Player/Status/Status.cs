namespace Dxura.RP.Game;

public partial class Status : GameObjectSystem<Status>
{
	private readonly Dictionary<string, TypeDescription> _statusTypes = new();
	private readonly Dictionary<string, IStatus> _statusInstances = new();

	private readonly Dictionary<long, List<IStatus>> _activeStatuses = new();

	public Status( Scene scene ) : base( scene )
	{
		RegisterStatuses();

	}

	private void RegisterStatuses()
	{
		_statusTypes.Clear();

		// Load all status types from TypeLibrary
		var statuses = TypeLibrary.GetTypes<IStatus>();
		foreach ( var statusType in statuses.Where( d => !d.IsAbstract && d.TargetType != null ) )
		{
			// Create a temporary instance to get the ID
			if ( TypeLibrary.Create<IStatus>( statusType.TargetType ) is {} instance )
			{
				_statusTypes[instance.Id] = statusType;
			}
		}

		Log.Info( $"Registered {_statusTypes.Count} statuses" );
	}

	public IEnumerable<IStatus> GetAllStatuses()
	{
		var allStatuses = new List<IStatus>();
		foreach ( var (status, type) in _statusTypes )
		{
			allStatuses.Add( GetCachedInstance( status )! );
		}

		return allStatuses;
	}

	public float ModifyDamageTaken( Player player )
	{
		if ( !_activeStatuses.TryGetValue( player.SteamId, out var statuses ) )
		{
			return 1f;
		}

		var multiplier = 1f;
		foreach ( var status in statuses )
		{
			multiplier *= status.ModifyDamageTaken( player );
		}

		return multiplier;
	}

	public bool HasStatus( long steamId, string statusId )
	{
		return _activeStatuses.TryGetValue( steamId, out var statuses ) && statuses.Any( x => x.Id == statusId );
	}

	public IStatus? GetActionBlocker( Player player, StatusAction action )
	{
		if ( !player.IsValid() )
		{
			return null;
		}

		foreach ( var statusId in player.Statuses.Keys )
		{
			var status = GetCachedInstance( statusId );
			if ( status?.BlocksAction( player, action ) == true )
			{
				return status;
			}
		}

		return null;
	}

	public string? GetActionBlockMessage( Player player, StatusAction action )
	{
		return GetActionBlocker( player, action )?.GetActionBlockMessage( player, action );
	}

	public bool IsActionBlocked( Player player, StatusAction action )
	{
		return GetActionBlocker( player, action ) != null;
	}

	public IStatus? GetCommandBlocker( Player player, ICommand command )
	{
		if ( !player.IsValid() )
		{
			return null;
		}

		foreach ( var statusId in player.Statuses.Keys )
		{
			var status = GetCachedInstance( statusId );
			if ( status?.BlocksCommand( player, command ) == true )
			{
				return status;
			}
		}

		return null;
	}

	public string? GetCommandBlockMessage( Player player, ICommand command )
	{
		return GetCommandBlocker( player, command )?.GetCommandBlockMessage( player, command );
	}

	public bool IsCommandBlocked( Player player, ICommand command )
	{
		return GetCommandBlocker( player, command ) != null;
	}

	public string ModifyChat( Player player, string message, MessageType messageType )
	{
		if ( !_activeStatuses.TryGetValue( player.SteamId, out var statuses ) )
		{
			return message;
		}

		foreach ( var status in statuses )
		{
			message = status.ModifyChat( player, message, messageType );
		}

		return message;
	}

	public IStatus? GetCachedInstance( string status )
	{
		if ( _statusInstances.TryGetValue( status, out var value ) )
		{
			return value;
		}

		if ( !_statusTypes.TryGetValue( status, out var typeDescription ) )
		{
			return null;
		}

		var instance = TypeLibrary.Create<IStatus>( typeDescription.TargetType );
		_statusInstances[status] = instance;

		return instance;
	}
}
