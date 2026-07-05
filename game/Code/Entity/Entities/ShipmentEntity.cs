using Dxura.RP.Game.Equipments;
using Sandbox.Diagnostics;

namespace Dxura.RP.Game.Entities;

public class ShipmentEntity : BaseEntity, IWireUsable, Component.IPressable
{
	private const float DepositRadius = 32f;

	[Property]
	[ReadOnly]
	[Sync( SyncFlags.FromHost )]
	public Guid MarketItemId { get; set; }

	[Property]
	[Change( nameof( OnQuantityChange ) )]
	[Sync( SyncFlags.FromHost )]
	public int Quantity { get; private set; }

	[Property]
	[Sync( SyncFlags.FromHost )]
	public Guid EquipmentId { get; set; }

	[Property]
	[Sync( SyncFlags.FromHost )]
	public int MaxQuantity { get; set; } = 10;

	[Property] public required GameObject EquipmentPreview { get; set; }
	[Property] public required ModelRenderer EquipmentRenderer { get; set; }

	[Property] public required TextRenderer TypeText { get; set; }
	[Property] public required TextRenderer QuantityText { get; set; }

	private float _totalAnimationTime;
	private Vector3 _originalPreviewPosition;
	private bool _previewPositionSaved;

	public override bool DestroyOnJobChange => false;
	public override bool AllowOwnershipTransfer => true;

	public override string DisplayName => $"{TypeText.Text} ({QuantityText.Text})";

	private bool _occluded;

	protected override void OnStart()
	{
		base.OnStart();

		UpdateState();

		// Save the original position of the preview
		_originalPreviewPosition = EquipmentPreview.LocalPosition;
		_previewPositionSaved = true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( !_occluded && _previewPositionSaved && !GameManager.IsHeadless )
		{
			AnimatePreview();
		}
	}

	public override void OnOcclusionChanged( bool occlude )
	{
		base.OnOcclusionChanged( occlude );

		_occluded = occlude;
	}

	private void UpdateState()
	{
		if ( !EquipmentRenderer.IsValid() || !TypeText.IsValid() || !QuantityText.IsValid() )
		{
			return;
		}

		if ( Networking.IsHost && Quantity <= 0 )
		{
			Quantity = MaxQuantity;
		}

		var equipment = GameModeEquipments.FindById( EquipmentId );
		if ( equipment == null )
		{
			return;
		}

		EquipmentRenderer.Model = equipment.GetWorldModel();
		EquipmentRenderer.WorldScale = 1.1f;
		TypeText.Text = equipment.DisplayName();
		QuantityText.Text = $"{Quantity}/{MaxQuantity}";
	}

	public void ConfigureHost( GameModeEquipmentDto equipment, int quantity )
	{
		ConfigureHost( equipment, quantity, quantity );
	}

	public void ConfigureHost( GameModeEquipmentDto equipment, int maxQuantity, int quantity )
	{
		Assert.True( Networking.IsHost );

		EquipmentId = equipment.GameModeAddonContentId;
		MaxQuantity = Math.Max( 1, maxQuantity );
		Quantity = Math.Clamp( quantity, 1, MaxQuantity );
		UpdateState();
	}

	public bool CanDeposit( DroppedEquipment droppedEquipment )
	{
		return droppedEquipment.IsValid() &&
		       Quantity < MaxQuantity &&
		       droppedEquipment.EquipmentId == EquipmentId;
	}

	public bool TryDepositHost( DroppedEquipment droppedEquipment )
	{
		Assert.True( Networking.IsHost );

		if ( !CanDeposit( droppedEquipment ) )
		{
			return false;
		}

		Quantity++;
		droppedEquipment.GameObject.Destroy();
		return true;
	}

	[Rpc.Host]
	public void DepositNearbyDropsHost()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:shipment:deposit", Config.Current.Game.ShipmentUseCooldown ) )
		{
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() || !HasDepositLineOfSight( player ) )
		{
			return;
		}

		foreach ( var droppedEquipment in FindNearbyDepositDrops() )
		{
			if ( !TryDepositHost( droppedEquipment ) )
			{
				continue;
			}

			if ( Quantity >= MaxQuantity )
			{
				return;
			}
		}
	}

	public static int CreateFromDropsHost( IReadOnlyList<DroppedEquipment> drops, Player? owner )
	{
		Assert.True( Networking.IsHost );

		var validDrops = drops
			.Where( drop => drop.IsValid() && drop.EquipmentId != Guid.Empty )
			.ToList();

		if ( validDrops.Count < 2 )
		{
			return 0;
		}

		var equipmentId = validDrops[0].EquipmentId;
		if ( validDrops.Any( drop => drop.EquipmentId != equipmentId ) )
		{
			return 0;
		}

		var equipment = validDrops[0].Resource ?? GameModeEquipments.FindById( equipmentId );
		if ( equipment == null )
		{
			return 0;
		}

		var marketItem = GameModeMarketItems.FindShipmentMarketItem( equipment );
		var maxQuantity = Math.Max( 1, marketItem?.Quantity ?? 10 );
		var marketItemId = validDrops.Select( drop => drop.MarketItemId ).FirstOrDefault( id => id != Guid.Empty );
		if ( marketItemId == Guid.Empty && marketItem != null )
		{
			marketItemId = marketItem.Id;
		}

		var createdQuantity = 0;
		while ( validDrops.Count > 0 )
		{
			var quantity = Math.Min( maxQuantity, validDrops.Count );
			if ( createdQuantity == 0 && quantity < 2 )
			{
				break;
			}

			var shipmentDrops = validDrops.Take( quantity ).ToList();
			var position = shipmentDrops.Aggregate( Vector3.Zero, ( sum, drop ) => sum + drop.WorldPosition ) / shipmentDrops.Count;

			if ( !TryCreateShipmentHost( equipment, marketItemId, maxQuantity, quantity, position, owner ) )
			{
				break;
			}

			foreach ( var drop in shipmentDrops )
			{
				drop.GameObject.Destroy();
			}

			createdQuantity += shipmentDrops.Count;
			validDrops.RemoveRange( 0, shipmentDrops.Count );
		}

		return createdQuantity;
	}

	public bool Press( IPressable.Event e )
	{
		// Prevent using while rotating in hands
		var hands = Player.Local.GetComponentInChildren<HandsEquipment>();
		if ( hands.IsValid() && hands.IsHolding( GameObject, true ) )
		{
			return false;
		}

		if ( Cooldown.Current.CheckAndStartCooldown( "shipment:use", Config.Current.Game.ShipmentUseCooldown, true ) )
		{
			return false;
		}

		UseHost();

		return true;
	}

	public void OnWireUse( long owner, Vector3 userPosition )
	{
		InternalUse();
	}

	[Rpc.Host]
	private void UseHost()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:shipment:use", Config.Current.Game.ShipmentUseCooldown ) )
		{
			return;
		}

		// LOS check to prevent remote toggling
		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
		{
			return;
		}

		var tr = Scene.Trace.Ray( player.AimRay, Config.Current.Game.ReachDistance )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.UseHitboxes()
			.Run();

		if ( !tr.Hit || tr.GameObject.Root != GameObject.Root )
		{
			return;
		}

		InternalUse();
	}

	private void InternalUse()
	{
		var equipment = GameModeEquipments.FindById( EquipmentId );
		if ( equipment == null )
		{
			return;
		}

		Quantity--;
		DroppedEquipment.CreateHost( equipment, EquipmentPreview.WorldPosition,
			EquipmentPreview.WorldRotation, marketItemId: MarketItemId );

		if ( Quantity == 0 )
		{
			GameObject.Destroy();
		}
	}

	protected override void OnDestroyed()
	{
		Assert.True( Networking.IsHost );

		// Drop everything on destroy 
		var equipment = GameModeEquipments.FindById( EquipmentId );
		if ( equipment != null )
		{
			for ( var x = 0; x < Quantity; x++ )
			{
				DroppedEquipment.CreateHost( equipment, EquipmentPreview.WorldPosition,
					EquipmentPreview.WorldRotation, marketItemId: MarketItemId );
			}
		}

		base.OnDestroyed();
	}

	private void OnQuantityChange( int oldValue, int newValue )
	{
		if ( QuantityText.IsValid() )
		{
			QuantityText.Text = $"{Quantity}/{MaxQuantity}";
		}
	}

	private bool HasDepositLineOfSight( Player player )
	{
		var tr = Scene.Trace.Ray( player.AimRay, Config.Current.Game.ReachDistance )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( Constants.TraceIgnoreTags )
			.UseHitboxes()
			.Run();

		return tr.Hit && tr.GameObject.Root == GameObject.Root;
	}

	private List<DroppedEquipment> FindNearbyDepositDrops()
	{
		if ( Quantity >= MaxQuantity || EquipmentId == Guid.Empty )
		{
			return [];
		}

		var bounds = new BBox(
			WorldPosition - Vector3.One * DepositRadius,
			WorldPosition + Vector3.One * DepositRadius );

		return Scene.FindInPhysics( bounds )
			.Select( gameObject => gameObject.Root.GetComponent<DroppedEquipment>() )
			.Where( CanDeposit )
			.GroupBy( drop => drop.GameObject )
			.Select( group => group.First() )
			.OrderBy( drop => drop.WorldPosition.DistanceSquared( WorldPosition ) )
			.Take( MaxQuantity - Quantity )
			.ToList();
	}

	private static bool TryCreateShipmentHost( GameModeEquipmentDto equipment, Guid marketItemId, int maxQuantity,
		int quantity, Vector3 position, Player? owner )
	{
		var shipmentPrefab = GameObject.GetPrefab( GameModeMarketItems.ShipmentPrefabPath );
		if ( !shipmentPrefab.IsValid() )
		{
			return false;
		}

		var shipmentObject = shipmentPrefab.Clone();
		shipmentObject.WorldPosition = position;

		var shipmentEntity = shipmentObject.GetComponent<ShipmentEntity>();
		var shipmentBaseEntity = shipmentObject.GetComponent<BaseEntity>();
		if ( !shipmentEntity.IsValid() || !shipmentBaseEntity.IsValid() )
		{
			shipmentObject.Destroy();
			return false;
		}

		shipmentEntity.MarketItemId = marketItemId;
		shipmentEntity.ConfigureHost( equipment, maxQuantity, quantity );

		if ( owner.IsValid() )
		{
			GameUtils.AssignSpawnedOwnership( shipmentObject, owner );
			shipmentObject.NetworkSpawn( owner.Connection );
		}
		else
		{
			shipmentObject.NetworkSpawn();
		}

		GameManager.Instance.PurchaseSound?.Broadcast( shipmentObject.WorldPosition, shipmentObject );
		return true;
	}

	private void AnimatePreview()
	{
		// Increment total time
		_totalAnimationTime = (_totalAnimationTime + Time.Delta) % 360f;
		
		// Base values for animation
		const float bobHeight = 2f;
		const float bobSpeed = 2.0f;
		const float rotationSpeed = 45.0f;

		// Calculate vertical position using a sine wave
		var verticalOffset = MathF.Sin( _totalAnimationTime * bobSpeed ) * bobHeight;

		// Update position based on original position
		EquipmentPreview.LocalPosition = new Vector3(
			_originalPreviewPosition.x,
			_originalPreviewPosition.y,
			_originalPreviewPosition.z + verticalOffset
		);

		// Rotate around Y axis
		EquipmentPreview.LocalRotation = Rotation.FromYaw( _totalAnimationTime * rotationSpeed );
	}
}
