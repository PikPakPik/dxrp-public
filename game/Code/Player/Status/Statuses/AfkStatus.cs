namespace Dxura.RP.Game.Statuses;

public class AfkStatus : BaseStatus
{
	public override string Id => Constants.AfkStatus;
	public override string Name => "AFK";
	public override string? MaterialIcon => "pause";
	public override Color Color => Color.FromRgb( 0xFFC107 );

	public override void OnAddedServer( Player player )
	{
		player.AfkSince = 0f;
	}

	public override void OnRemovedServer( Player player )
	{
		player.AfkSince = null;
	}
}
