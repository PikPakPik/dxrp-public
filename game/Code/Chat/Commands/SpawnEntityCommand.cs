using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class SpawnEntityCommand : ICommand
{
	private const int MaxQuantity = 10;

	public string Command => "spawnentity";
	public string Help => "/spawnentity <quantity> <name>  (equipment spawns as shipment when quantity > 1)";
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

		var entity = ResolveEntity( name );
		if ( entity != null )
		{
			if ( !GameManager.Instance.StaffSpawnEntity( caller, entity, quantity ) )
			{
				caller.Error( $"Failed to spawn entity '{entity.DisplayName()}'." );
			}

			return true;
		}

		var equipment = ResolveEquipment( name );
		if ( equipment != null )
		{
			if ( !GameManager.Instance.StaffSpawnEquipment( caller, equipment, quantity ) )
			{
				caller.Error( $"Failed to spawn equipment '{equipment.DisplayName()}'." );
			}

			return true;
		}

		caller.Error( $"Unknown entity or equipment '{name}'." );
		SuggestMatches( caller, name );
		return true;
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

	private static GameModeEntityDto? ResolveEntity( string input )
	{
		var byIdentifier = GameModeEntities.FindByIdentifier( input );
		if ( byIdentifier != null )
		{
			return byIdentifier;
		}

		return ResolveByName(
			GameModeEntities.All,
			input,
			entity => entity.Identifier(),
			entity => entity.DisplayName(),
			entity => entity.Name() );
	}

	private static GameModeEquipmentDto? ResolveEquipment( string input )
	{
		var byIdentifier = GameModeEquipments.FindByIdentifier( input );
		if ( byIdentifier != null )
		{
			return byIdentifier;
		}

		return ResolveByName(
			GameModeEquipments.All,
			input,
			equipment => equipment.Identifier(),
			equipment => equipment.DisplayName(),
			equipment => equipment.Name() );
	}

	private static T? ResolveByName<T>(
		IEnumerable<T> items,
		string input,
		Func<T, string> getIdentifier,
		Func<T, string> getDisplayName,
		Func<T, string> getName )
	{
		var normalizedInput = NormalizeName( input );
		var candidates = items.ToList();

		var exact = candidates.FirstOrDefault( item =>
			NormalizeName( getIdentifier( item ) ) == normalizedInput ||
			NormalizeName( getDisplayName( item ) ) == normalizedInput ||
			NormalizeName( getName( item ) ) == normalizedInput );
		if ( exact != null )
		{
			return exact;
		}

		return candidates.FirstOrDefault( item =>
			NormalizeName( getIdentifier( item ) ).Contains( normalizedInput ) ||
			NormalizeName( getDisplayName( item ) ).Contains( normalizedInput ) ||
			NormalizeName( getName( item ) ).Contains( normalizedInput ) );
	}

	private static void SuggestMatches( Player caller, string input )
	{
		var normalizedInput = NormalizeName( input );
		var suggestions = GameModeEntities.All
			.Select( entity => entity.Identifier() )
			.Concat( GameModeEntities.All.Select( entity => entity.DisplayName() ) )
			.Concat( GameModeEquipments.All.Select( equipment => equipment.Identifier() ) )
			.Concat( GameModeEquipments.All.Select( equipment => equipment.DisplayName() ) )
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.Where( name => NormalizeName( name ).Contains( normalizedInput ) )
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
