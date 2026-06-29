namespace Dxura.RP.Game;

public class DebugPlayerSpawner : Component, IGameEvents
{
	[Property] private string Name { get; set; } = "Joe Doe";
	[Property] private string JobIdentifier { get; set; }

	private bool _spawned;

	public void OnGameModeUpdated( GameModeDto? before, GameModeDto? after )
	{
		if ( after == null || _spawned )
		{
			return;
		}

		_spawned = true;

		var debugPlayer = GameNetworkManager.Instance.PlayerPrefab.Clone();
		if ( !debugPlayer.IsValid() )
		{
			return;
		}

		debugPlayer.Name = $"Debug Player {Name}";

		var player = debugPlayer.GetComponent<Player>();
		player.SteamId = Random.Shared.NextInt64( 69420197960265728, 69420297960265728 );
		player.SteamName = Name;
		player.IsDebugPlayer = true;
		player.Controller.Enabled = false;
		player.GetComponent<Rigidbody>().MotionEnabled = false;

		debugPlayer.NetworkSpawn( NetworkSpawnOptions.Default );
		debugPlayer.Network.DropOwnership();

		GameNetworkManager.Instance.Players.Add( player.SteamId, player );

		player.AssignJobForcedHost( GameModeJobs.FindByReference( JobIdentifier ) ?? GameModeJobs.Default );
		player.TeleportHost( new Transform( WorldPosition, WorldRotation ) );

		GameObject.Destroy();
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var model = Model.Load( "models/editor/spawnpoint.vmdl" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = Color.Black.WithAlpha( Gizmo.IsHovered || Gizmo.IsSelected ? 0.7f : 0.5f );
		var so = Gizmo.Draw.Model( model );
		if ( so is not null )
		{
			so.Flags.CastShadows = true;
		}
	}
}
