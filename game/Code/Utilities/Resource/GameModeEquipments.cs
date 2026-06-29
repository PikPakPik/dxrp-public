using Dxura.RP.Shared;

namespace Dxura.RP.Game;

public static class GameModeEquipments
{
	public static IReadOnlyList<GameModeEquipmentDto> All => Config.Current.GameMode.Equipments;

	public static GameModeEquipmentDto? FindById( Guid? contentId )
	{
		if ( !contentId.HasValue )
		{
			return null;
		}

		return All.FirstOrDefault( x => x.GameModeAddonContentId == contentId.Value );
	}

	public static GameModeEquipmentDto? FindByDtoId( Guid? dtoId )
	{
		if ( !dtoId.HasValue )
		{
			return null;
		}

		return All.FirstOrDefault( x => x.Id == dtoId.Value );
	}

	public static bool IsTool( GameModeEquipmentDto? equipment )
	{
		return equipment?.GameModeAddonContentId == Constants.ToolEquipmentId;
	}

	public static GameModeEquipmentDto? FindByPrefabPath( string prefabPath )
	{
		if ( string.IsNullOrWhiteSpace( prefabPath ) )
		{
			return null;
		}

		return All.FirstOrDefault( x => string.Equals( x.PrefabPath(), prefabPath, StringComparison.OrdinalIgnoreCase ) );
	}
}
