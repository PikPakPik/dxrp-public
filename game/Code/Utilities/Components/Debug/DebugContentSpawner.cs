using Dxura.RP.Game.Entities;
namespace Dxura.RP.Game;

public class DebugContentSpawner : Component, IGameEvents
{
	[Property] private Guid ContentId { get; set; }

	private bool _spawned;

	protected override void OnStart()
	{
		if ( GameModeEntities.All.Count > 0 || GameModeEquipments.All.Count > 0 )
		{
			TrySpawn();
		}
	}

	public void OnGameModeUpdated( GameModeDto? before, GameModeDto? after )
	{
		if ( after != null )
		{
			TrySpawn();
		}
	}

	private void TrySpawn()
	{
		if ( _spawned )
		{
			return;
		}

		var entity = GameModeEntities.FindById( ContentId );
		if ( entity != null )
		{
			_spawned = true;
			SpawnEntity( entity );
			GameObject.Destroy();
			return;
		}

		var equipment = GameModeEquipments.FindById( ContentId );
		if ( equipment != null )
		{
			_spawned = true;
			var shipmentPrefab = GameObject.GetPrefab( GameModeMarketItems.ShipmentPrefabPath );
			if ( shipmentPrefab.IsValid() )
			{
				var shipmentObject = shipmentPrefab.Clone();
				shipmentObject.WorldPosition = WorldPosition;
				shipmentObject.WorldRotation = WorldRotation;
				var shipmentEntity = shipmentObject.GetComponent<ShipmentEntity>();
				var shipmentBaseEntity = shipmentObject.GetComponent<BaseEntity>();
				if ( shipmentEntity.IsValid() && shipmentBaseEntity.IsValid() )
				{
					shipmentEntity.ConfigureHost( equipment, 100 );
					shipmentObject.NetworkSpawn();
				}
				else
				{
					shipmentObject.Destroy();
				}
			}
			GameObject.Destroy();
			return;
		}

		Log.Warning( $"DebugContentSpawner: nothing found for contentId '{ContentId}'" );
	}

	private void SpawnEntity( GameModeEntityDto entity )
	{
		var prefabPath = entity.PrefabPath();
		if ( string.IsNullOrWhiteSpace( prefabPath ) )
		{
			return;
		}

		var prefab = GameObject.GetPrefab( prefabPath );
		if ( !prefab.IsValid() )
		{
			return;
		}

		var spawned = prefab.Clone();
		spawned.WorldPosition = WorldPosition;
		spawned.WorldRotation = WorldRotation;

		var baseEntity = spawned.GetComponent<BaseEntity>();
		if ( baseEntity != null )
		{
			baseEntity.ConfigureGameModeEntityHost( entity );
		}

		spawned.NetworkSpawn();
	}

	private Color IdentifierColor()
	{
		var hue = Math.Abs( ContentId.GetHashCode() ) % 360 / 360f;
		return new ColorHsv( hue * 360f, 0.8f, 0.9f );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var model = Model.Load( "models/editor/cordon_helper.vmdl_c" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = IdentifierColor().WithAlpha( Gizmo.IsHovered || Gizmo.IsSelected ? 0.9f : 0.6f );
		var so = Gizmo.Draw.Model( model );
		if ( so is not null )
		{
			so.Flags.CastShadows = true;
		}
	}
}
