using Dxura.RP.Shared;

namespace Dxura.RP.Game.UI;

public static class JobPreviewClothing
{
	public static void ApplyPlayer( SkinnedModelRenderer renderer, Player player )
	{
		var clothing = new ClothingContainer();
		var avatarData = player.Network.Owner?.GetUserData( "avatar" );

		if ( !string.IsNullOrWhiteSpace( avatarData ) )
		{
			clothing.Deserialize( avatarData );
		}

		if ( player.Job.IsValid() )
		{
			clothing.AddRange( player.Job.GetClothingEntries() );
		}

		clothing.Apply( renderer );
	}

	public static void ApplyJob( SkinnedModelRenderer renderer, GameModeJobDto job, Player? avatarSource = null )
	{
		var clothing = new ClothingContainer();
		var source = avatarSource ?? (Player.Local.IsValid() ? Player.Local : null);

		if ( source is { IsValid: true } )
		{
			var avatarData = source.Network.Owner?.GetUserData( "avatar" );
			if ( !string.IsNullOrWhiteSpace( avatarData ) )
			{
				clothing.Deserialize( avatarData );
			}
		}

		clothing.AddRange( job.GetClothingEntries() );
		clothing.Apply( renderer );
	}
}
