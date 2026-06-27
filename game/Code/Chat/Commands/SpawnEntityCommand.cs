using Dxura.RP.Game.Entities;
using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class SpawnEntityCommand : ICommand
{
	public const int MaxQuantity = 100;

	public string Command => "spawnentity";
	public string Help => "/spawnentity list  |  /spawnentity <quantity> <equipment>  (shipments when quantity > 1; use market for entities)";
	public bool IsUsableWhileDead => false;
	public Permission[] RequiredPermissions => [Permission.CommandSpawnEntity];

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( !caller.IsValid() )
		{
			return false;
		}

		if ( args.Length < 1 )
		{
			caller.SendMessage( Help );
			return true;
		}

		if ( args[0].Equals( "list", StringComparison.OrdinalIgnoreCase ) )
		{
			var filter = args.Length > 1 ? string.Join( ' ', args[1..] ) : null;
			SendSpawnList( caller, filter );
			return true;
		}

		if ( !TryParseArguments( args, out var quantity, out var name ) )
		{
			caller.SendMessage( Help );
			return true;
		}

		if ( quantity <= 0 || quantity > MaxQuantity )
		{
			caller.Error( $"Quantity must be between 1 and {MaxQuantity}." );
			return true;
		}

		if ( TryResolveSpawnableEquipment( name, out var equipment, out var marketItemId ) )
		{
			if ( TrySpawnEquipment( caller, equipment!, quantity, marketItemId ) )
			{
				LogStaffSpawn( caller, equipment!.DisplayName(), quantity );
			}
			else
			{
				caller.Error( $"Failed to spawn equipment '{equipment!.DisplayName()}'." );
			}

			return true;
		}

		caller.Error( $"Unknown spawnable equipment '{name}'. Use the market for entities like printers." );
		SuggestMatches( caller, name );
		return true;
	}

	public static bool TrySpawnMarketItem( Player player, GameModeMarketItemDto marketItem )
	{
		if ( !GameModeMarketItems.CanAdminSpawn( player, marketItem ) )
		{
			return false;
		}

		var displayName = GameModeMarketItems.DisplayName( marketItem );
		if ( string.IsNullOrWhiteSpace( displayName ) )
		{
			return false;
		}

		var spawned = marketItem.Type switch
		{
			GameModeMarketItemType.Entity => GameModeMarketItems.ResolveEntity( marketItem ) is { } entity
				&& TrySpawnEntity( player, entity, 1 ),
			GameModeMarketItemType.Equipment => GameModeMarketItems.ResolveEquipment( marketItem ) is { } equipment
				&& TrySpawnEquipment( player, equipment, marketItem.Quantity, marketItem.Id ),
			_ => false
		};

		if ( !spawned )
		{
			return false;
		}

		Log.Info( $"Staff {player.SteamId} spawned market item '{displayName}' [{marketItem.Id}]" );
		_ = ServerApiClient.Audit( "StaffSpawnMarket", $"{player.SteamName} ({player.SteamId}) spawned {displayName} ({marketItem.Id})", player.SteamId );
		return true;
	}

	public static bool TrySpawnEntity( Player player, GameModeEntityDto entity, int quantity = 1 )
	{
		if ( !player.IsValid() || entity == null || !RankSystem.HasPermission( player.SteamId, Permission.CommandSpawnEntity ) )
		{
			return false;
		}

		var entityPrefabPath = entity.PrefabPath();
		if ( string.IsNullOrWhiteSpace( entityPrefabPath ) )
		{
			return false;
		}

		var entityPrefab = GameObject.GetPrefab( entityPrefabPath );
		if ( !entityPrefab.IsValid() )
		{
			return false;
		}

		quantity = Math.Clamp( quantity, 1, MaxQuantity );
		var spawnPosition = GameUtils.GetSpawnPosition( player.AimRay );
		var horizontalForward = new Vector3( player.AimRay.Forward.x, player.AimRay.Forward.y, 0 );
		if ( horizontalForward.Length > 0.01f )
		{
			horizontalForward = horizontalForward.Normal;
		}
		else
		{
			horizontalForward = Vector3.Forward;
		}

		var horizontalRight = Vector3.Cross( Vector3.Up, horizontalForward ).Normal;

		for ( var i = 0; i < quantity; i++ )
		{
			var entityToSpawn = entityPrefab.Clone();
			entityToSpawn.WorldPosition = spawnPosition
				+ horizontalRight * ( ( i % 5 ) - 2 ) * 20f
				+ horizontalForward * ( i / 5 ) * 20f;

			var baseEntityComponent = entityToSpawn.GetComponent<BaseEntity>();
			if ( baseEntityComponent != null )
			{
				baseEntityComponent.Identifier = entity.Identifier();
				baseEntityComponent.ConfigureGameModeEntityHost( entity );
			}

			GameUtils.ClearSpawnedOwnership( entityToSpawn );
			entityToSpawn.NetworkSpawn();
			GameManager.Instance.PurchaseSound?.Broadcast( entityToSpawn.WorldPosition, entityToSpawn );
		}

		return true;
	}

	public static bool TrySpawnEquipment( Player player, GameModeEquipmentDto equipment, int quantity = 1, Guid marketItemId = default )
	{
		if ( !player.IsValid() || equipment == null || !RankSystem.HasPermission( player.SteamId, Permission.CommandSpawnEntity ) )
		{
			return false;
		}

		if ( string.IsNullOrWhiteSpace( equipment.PrefabPath() ) )
		{
			return false;
		}

		quantity = Math.Clamp( quantity, 1, MaxQuantity );
		var spawnPosition = GameUtils.GetSpawnPosition( player.AimRay );

		if ( quantity > 1 )
		{
			var shipmentPrefab = GameObject.GetPrefab( GameModeMarketItems.ShipmentPrefabPath );
			if ( !shipmentPrefab.IsValid() )
			{
				return false;
			}

			var shipmentObject = shipmentPrefab.Clone();
			shipmentObject.WorldPosition = spawnPosition;

			var shipmentEntity = shipmentObject.GetComponent<ShipmentEntity>();
			var shipmentBaseEntity = shipmentObject.GetComponent<BaseEntity>();
			if ( !shipmentEntity.IsValid() || !shipmentBaseEntity.IsValid() )
			{
				shipmentObject.Destroy();
				return false;
			}

			shipmentBaseEntity.Identifier = equipment.Identifier();
			shipmentEntity.MarketItemId = marketItemId;
			shipmentEntity.ConfigureHost( equipment, quantity );

			GameUtils.ClearSpawnedOwnership( shipmentObject );
			shipmentObject.NetworkSpawn();
			GameManager.Instance.PurchaseSound?.Broadcast( shipmentObject.WorldPosition, shipmentObject );
		}
		else
		{
			var droppedEquipment = DroppedEquipment.CreateHost(
				equipment,
				spawnPosition,
				rotation: Rotation.FromYaw( player.Controller.EyeAngles.yaw + 90 ),
				marketItemId: marketItemId );
			GameManager.Instance.PurchaseSound?.Broadcast( droppedEquipment.WorldPosition, droppedEquipment.GameObject );
		}

		return true;
	}

	private static void LogStaffSpawn( Player player, string displayName, int quantity )
	{
		Log.Info( $"Staff {player.SteamId} spawned '{displayName}' x{quantity}" );
		_ = ServerApiClient.Audit( "StaffSpawnEntity", $"{player.SteamName} ({player.SteamId}) spawned {displayName} x{quantity}", player.SteamId );
	}

	private static void SendSpawnList( Player caller, string? filter )
	{
		var normalizedFilter = string.IsNullOrWhiteSpace( filter ) ? null : NormalizeName( filter );
		var listedEquipment = new HashSet<Guid>();
		var equipments = new List<string>();

		foreach ( var marketItem in GameModeMarketItems.All
			         .Where( item => item.Type == GameModeMarketItemType.Equipment && GameModeMarketItems.IsSpawnable( item ) )
			         .OrderBy( GameModeMarketItems.DisplayName ) )
		{
			var equipment = GameModeMarketItems.ResolveEquipment( marketItem );
			if ( equipment == null || !listedEquipment.Add( equipment.Id ) )
			{
				continue;
			}

			if ( !MatchesListFilter( equipment.Identifier(), equipment.DisplayName(), equipment.Name(), normalizedFilter ) )
			{
				continue;
			}

			equipments.Add( FormatListEntry( equipment.Identifier(), equipment.DisplayName() ) );
		}

		if ( equipments.Count == 0 )
		{
			caller.SendMessage( string.IsNullOrWhiteSpace( filter )
				? "No spawnable equipment. Use the market for entities like printers."
				: $"No spawnable equipment matching '{filter}'." );
			return;
		}

		caller.SendMessage( $"Equipment ({equipments.Count}): {string.Join( ", ", equipments )}" );
	}

	private static bool TryResolveSpawnableEquipment( string input, out GameModeEquipmentDto? equipment, out Guid marketItemId )
	{
		equipment = null;
		marketItemId = Guid.Empty;

		var marketItem = GameModeMarketItems.All
			.Where( item => item.Type == GameModeMarketItemType.Equipment && GameModeMarketItems.IsSpawnable( item ) )
			.FirstOrDefault( item =>
			{
				var candidate = GameModeMarketItems.ResolveEquipment( item );
				return candidate != null && MatchesName( candidate.Identifier(), candidate.DisplayName(), candidate.Name(), input );
			} );

		if ( marketItem == null )
		{
			return false;
		}

		equipment = GameModeMarketItems.ResolveEquipment( marketItem );
		marketItemId = marketItem.Id;
		return equipment != null;
	}

	private static bool MatchesName( string identifier, string displayName, string name, string input )
	{
		var normalizedInput = NormalizeName( input );
		return NormalizeName( identifier ) == normalizedInput
		       || NormalizeName( displayName ) == normalizedInput
		       || NormalizeName( name ) == normalizedInput
		       || NormalizeName( identifier ).Contains( normalizedInput )
		       || NormalizeName( displayName ).Contains( normalizedInput )
		       || NormalizeName( name ).Contains( normalizedInput );
	}

	private static bool MatchesListFilter( string identifier, string displayName, string name, string? normalizedFilter )
	{
		if ( normalizedFilter == null )
		{
			return true;
		}

		return NormalizeName( identifier ).Contains( normalizedFilter )
		       || NormalizeName( displayName ).Contains( normalizedFilter )
		       || NormalizeName( name ).Contains( normalizedFilter );
	}

	private static string FormatListEntry( string identifier, string displayName )
	{
		return string.Equals( identifier, displayName, StringComparison.OrdinalIgnoreCase )
			? identifier
			: $"{identifier} ({displayName})";
	}

	private static bool TryParseArguments( string[] args, out int quantity, out string name )
	{
		quantity = 1;
		name = string.Empty;

		if ( args.Length > 1 && int.TryParse( args[0], out var leadingQuantity ) )
		{
			quantity = leadingQuantity;
			name = string.Join( ' ', args[1..] );
			return !string.IsNullOrWhiteSpace( name );
		}

		if ( args.Length > 1 && int.TryParse( args[^1], out var trailingQuantity ) )
		{
			quantity = trailingQuantity;
			name = string.Join( ' ', args[..^1] );
			return !string.IsNullOrWhiteSpace( name );
		}

		name = string.Join( ' ', args );
		return !string.IsNullOrWhiteSpace( name );
	}

	private static void SuggestMatches( Player caller, string input )
	{
		var normalizedInput = NormalizeName( input );
		var suggestions = GameModeMarketItems.All
			.Where( item => item.Type == GameModeMarketItemType.Equipment && GameModeMarketItems.IsSpawnable( item ) )
			.Select( item => GameModeMarketItems.ResolveEquipment( item )?.Identifier() )
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.Where( name => NormalizeName( name! ).Contains( normalizedInput ) )
			.Take( 5 )
			.ToArray();

		if ( suggestions.Length == 0 )
		{
			return;
		}

		caller.SendMessage( $"Did you mean: {string.Join( ", ", suggestions )}" );
	}

	private static string NormalizeName( string value )
	{
		return new string( value
			.Where( c => c != ' ' && c != '_' && c != '-' )
			.Select( char.ToLowerInvariant )
			.ToArray() );
	}
}
