using System.Text.Json;
using System.Threading.Tasks;
namespace Dxura.RP.Game;

public class DebugDupeSpawner : Component, IGameEvents
{
	[Property]
	private string DupeJson { get; set; } = "";

	[Property]
	private float? PlayerDistanceSpawn { get; set; } = 600f;

	private bool _isSpawning;

	public void OnSecondlyUpdate()
	{
		if ( !Player.Local.IsValid() )
		{
			return;
		}

		var isWithinDistance = !PlayerDistanceSpawn.HasValue || Player.Local.WorldPosition.Distance( WorldPosition ) < PlayerDistanceSpawn;
		if ( !isWithinDistance || _isSpawning )
		{
			return;
		}

		_isSpawning = true;
		_ = SpawnDupe();
	}

	private async Task SpawnDupe()
	{
		if ( !Networking.IsHost )
		{
			return;
		}
		if ( string.IsNullOrEmpty( DupeJson ) )
		{
			return;
		}

		var dupe = JsonSerializer.Deserialize<ConstructDupe>( DupeJson );

		if ( dupe == null )
		{
			return;
		}

		_ = Construct.Current.SpawnDupe( Player.Local, dupe, null, WorldPosition );
	}

	private Color IdentifierColor()
	{
		var hue = Math.Abs( (DupeJson ?? string.Empty).GetHashCode() ) % 360 / 360f;
		return new ColorHsv( hue * 360f, 0.8f, 0.9f );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var model = Model.Load( "models/editor/node_hint.vmdl_c" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = IdentifierColor().WithAlpha( Gizmo.IsHovered || Gizmo.IsSelected ? 0.9f : 0.6f );
		var so = Gizmo.Draw.Model( model );
		if ( so is not null )
		{
			so.Flags.CastShadows = true;
		}
	}
}
