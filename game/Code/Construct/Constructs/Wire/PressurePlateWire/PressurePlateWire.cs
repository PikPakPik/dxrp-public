namespace Dxura.RP.Game.Wire;

[Title( "Pressure Plate" )]
[Category( "Wire" )]
[Icon( "square_foot" )]
public class PressurePlateWire() : BaseWireConstruct( ConstructType.PressurePlateWire ), IWireEvents
{
	private PressurePlateWireData _data = new();

	private bool _hasBeenTriggeredSinceLastWireTick;
	private bool _wasOccupied;
	private bool _isPlateOccupied;
	private float _totalMassOnPlate;
	private float _animatedMassOnPlate;
	private float _lastBroadcastMass;
	private int _objectCountOnPlate;
	private Vector3 _plateRestPosition;

	[Property] public GameObject PlateModel { get; set; } = null!;
	[Property] public ModelRenderer PlateRenderer { get; set; } = null!;
	[Property] public BoxCollider PlateCollider { get; set; } = null!;

	[WireOutput( "triggered" )]
	public bool Triggered { get; set; }

	[WireOutput( "trigger_count" )]
	public float TriggerCount { get; set; }

	[WireOutput( "trigger_mass" )]
	public float TriggerMass { get; set; }

	[WireOutput( "object_count" )]
	public float ObjectCount { get; set; }

	[WireInput( "reset_count" )]
	public bool ResetCount
	{
		set
		{
			if ( value )
			{
				TriggerCount = 0f;
			}
		}
		get => false;
	}

	public override string Name => "Pressure Plate";

	public override Vector3 GetPortPosition()
	{
		return GameObject.WorldPosition + WorldRotation.Backward * (_data.Length * 0.5f);
	}

	protected override void OnStart()
	{
		base.OnStart();
		UpdateMeshes();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( !GameManager.IsHeadless )
		{
			UpdatePlateAnimation();
		}

	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		
		if ( IsOwner )
		{
			CheckZone();
		}
		
	}

	private void CheckZone()
	{
		var objects = FindObjectsInZone();
		var occupied = objects.Count > 0;
		var totalMass = SumMass( objects );

		var stateChanged = occupied != _isPlateOccupied
		                   || objects.Count != _objectCountOnPlate
		                   || Math.Abs( totalMass - _totalMassOnPlate ) > 0.01f;

		if ( stateChanged )
		{
			if ( occupied && !_isPlateOccupied )
			{
				_hasBeenTriggeredSinceLastWireTick = true;
			}

			SyncZoneStateHost( occupied, totalMass, objects.Count );
		}

		_isPlateOccupied = occupied;
		_totalMassOnPlate = totalMass;
		_objectCountOnPlate = objects.Count;
		_animatedMassOnPlate = totalMass;
	}

	private List<GameObject> FindObjectsInZone()
	{
		var results = new List<GameObject>();
		var bounds = GetDetectionBounds();
		var hits = Scene.FindInPhysics( bounds );
		var processed = new HashSet<GameObject>();

		foreach ( var hit in hits )
		{
			var root = hit.Root;
			if ( !root.IsValid() || !processed.Add( root ) )
			{
				continue;
			}

			if ( root == GameObject.Root )
			{
				continue;
			}

			if ( !PassesFilter( root ) )
			{
				continue;
			}

			if ( !IsPointOnPlate( GetSamplePoint( root ) ) )
			{
				continue;
			}

			results.Add( root );
		}

		return results;
	}

	private static float SumMass( IEnumerable<GameObject> objects )
	{
		var total = 0f;
		foreach ( var obj in objects )
		{
			total += GetObjectMass( obj );
		}

		return total;
	}

	private BBox GetDetectionBounds()
	{
		var halfLength = _data.Length * 0.5f;
		var halfWidth = _data.Width * 0.5f;
		var plateTop = GetPlateTopLocalZ();
		var minLocal = new Vector3( -halfLength, -halfWidth, plateTop );
		var maxLocal = new Vector3( halfLength, halfWidth, plateTop + PressurePlateWireDefinition.DetectionHeight );

		var transform = GameObject.WorldTransform;
		var corners = new Vector3[]
		{
			transform.PointToWorld( new Vector3( minLocal.x, minLocal.y, minLocal.z ) ),
			transform.PointToWorld( new Vector3( maxLocal.x, minLocal.y, minLocal.z ) ),
			transform.PointToWorld( new Vector3( minLocal.x, maxLocal.y, minLocal.z ) ),
			transform.PointToWorld( new Vector3( maxLocal.x, maxLocal.y, minLocal.z ) ),
			transform.PointToWorld( new Vector3( minLocal.x, minLocal.y, maxLocal.z ) ),
			transform.PointToWorld( new Vector3( maxLocal.x, minLocal.y, maxLocal.z ) ),
			transform.PointToWorld( new Vector3( minLocal.x, maxLocal.y, maxLocal.z ) ),
			transform.PointToWorld( new Vector3( maxLocal.x, maxLocal.y, maxLocal.z ) )
		};

		return BBox.FromPoints( corners );
	}

	private float GetPlateTopLocalZ()
	{
		var plateCenterZ = PlateModel.IsValid()
			? PlateModel.LocalPosition.z
			: _plateRestPosition.z;

		return plateCenterZ + _data.Depth * 0.5f;
	}

	private bool IsPointOnPlate( Vector3 worldPoint )
	{
		var localPoint = GameObject.WorldTransform.PointToLocal( worldPoint );
		var halfLength = _data.Length * 0.5f;
		var halfWidth = _data.Width * 0.5f;
		var plateTop = GetPlateTopLocalZ();

		if ( Math.Abs( localPoint.x ) > halfLength || Math.Abs( localPoint.y ) > halfWidth )
		{
			return false;
		}

		const float toleranceBelow = 1f;
		return localPoint.z >= plateTop - toleranceBelow
		       && localPoint.z <= plateTop + PressurePlateWireDefinition.DetectionHeight;
	}

	private Vector3 GetSamplePoint( GameObject root )
	{
		var player = root.GetComponent<Player>();
		if ( player.IsValid() && player.Controller.IsValid() && player.Controller.FeetCollider.IsValid() )
		{
			return player.Controller.FeetCollider.WorldPosition;
		}

		BBox? combinedBounds = null;
		foreach ( var collider in root.GetComponentsInChildren<Collider>( false ) )
		{
			if ( !collider.IsValid() )
			{
				continue;
			}

			combinedBounds = combinedBounds.HasValue
				? combinedBounds.Value.AddBBox( collider.GetWorldBounds() )
				: collider.GetWorldBounds();
		}

		if ( !combinedBounds.HasValue )
		{
			return root.WorldPosition;
		}

		var bounds = combinedBounds.Value;

		var transform = GameObject.WorldTransform;
		var localCenter = transform.PointToLocal( bounds.Center );
		var localMin = transform.PointToLocal( bounds.Mins );
		var localMax = transform.PointToLocal( bounds.Maxs );
		var bottomLocalZ = MathF.Min( localMin.z, localMax.z );

		return transform.PointToWorld( new Vector3( localCenter.x, localCenter.y, bottomLocalZ ) );
	}

	private bool PassesFilter( GameObject root )
	{
		return _data.FilterType switch
		{
			TriggerFilterType.PlayerOnly => root.Tags.Has( Constants.PlayerTag ),
			TriggerFilterType.EntityOnly => root.Tags.Has( Constants.EntityTag ),
			TriggerFilterType.ConstructOnly => root.Tags.Has( Constants.ConstructTag ),
			_ => true
		};
	}

	[Rpc.Host( NetFlags.Unreliable )]
	private void SyncZoneStateHost( bool occupied, float totalMass, int objectCount )
	{
		if ( Rpc.CallerId != NetworkOwner )
		{
			return;
		}

		_isPlateOccupied = occupied;
		_totalMassOnPlate = totalMass;
		_objectCountOnPlate = objectCount;
		_animatedMassOnPlate = totalMass;
	}

	public void OnWireTick()
	{
		var wasTriggered = Triggered;
		var isCurrentlyTriggered = _isPlateOccupied;
		var hasBeenTriggered = _hasBeenTriggeredSinceLastWireTick || isCurrentlyTriggered;

		if ( !wasTriggered && hasBeenTriggered )
		{
			TriggerCount++;
		}

		if ( isCurrentlyTriggered )
		{
			TriggerMass = _totalMassOnPlate;
			ObjectCount = _objectCountOnPlate;
		}
		else
		{
			TriggerMass = 0f;
			ObjectCount = 0f;
		}

		// Update Triggered after mass/count so wired listeners see the correct values.
		if ( hasBeenTriggered != wasTriggered )
		{
			Triggered = hasBeenTriggered;
		}

		if ( isCurrentlyTriggered != _wasOccupied
		     || (isCurrentlyTriggered && Math.Abs( _totalMassOnPlate - _lastBroadcastMass ) > 0.5f)
		     || (!isCurrentlyTriggered && _lastBroadcastMass > 0f) )
		{
			_wasOccupied = isCurrentlyTriggered;
			_lastBroadcastMass = _totalMassOnPlate;
			BroadcastPlateState( _totalMassOnPlate );
		}

		_hasBeenTriggeredSinceLastWireTick = false;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void BroadcastPlateState( float totalMass )
	{
		_animatedMassOnPlate = totalMass;
	}

	private void UpdatePlateAnimation()
	{
		if ( !PlateModel.IsValid() || !PlateRenderer.IsValid() || GameObject.Tags.Has( Constants.OccludeTag ) )
		{
			return;
		}

		var maxPress = PressurePlateWireDefinition.GetMaxPressDepth( _data.Depth );
		var pressDepth = PressurePlateWireDefinition.GetPressDepthFromMass( _animatedMassOnPlate, _data.Depth );
		var targetPosition = _plateRestPosition + new Vector3( 0, 0, -pressDepth );
		var lerpSpeed = 14f * Time.Delta;

		PlateModel.LocalPosition = Vector3.Lerp( PlateModel.LocalPosition, targetPosition, lerpSpeed );

		var pressFactor = maxPress > 0f ? pressDepth / maxPress : 0f;
		var targetPlateColor = Color.Lerp(
			PressurePlateWireDefinition.RestPlateColor,
			PressurePlateWireDefinition.PressedPlateColor,
			pressFactor
		);
		
		PlateRenderer.Tint = Color.Lerp( PlateRenderer.Tint, targetPlateColor, lerpSpeed );
	}

	private static float GetObjectMass( GameObject obj )
	{
		var rigidbody = obj.GetComponent<Rigidbody>();
		return rigidbody.IsValid() ? rigidbody.Mass : 0f;
	}

	protected override void OnDataChanged( IConstructData oldData, IConstructData newData )
	{
		_data = newData as PressurePlateWireData ?? new PressurePlateWireData();
		UpdateMeshes();
	}

	private void UpdateMeshes()
	{
		_plateRestPosition = new Vector3( 0, 0, _data.Depth * 0.5f - PressurePlateWireDefinition.FlatSpawnBuffer );

		if ( PlateRenderer.IsValid() )
		{
			var plateMesh = CreateBoxMesh( _data.Width, _data.Length, _data.Depth );
			PlateRenderer.Model = Model.Builder.AddMesh( plateMesh ).Create();
			PlateRenderer.Tint = PressurePlateWireDefinition.RestPlateColor;
		}

		if ( PlateCollider.IsValid() )
		{
			PlateCollider.Scale = new Vector3( _data.Length, _data.Width, _data.Depth );
		}

		if ( PlateModel.IsValid() )
		{
			PlateModel.LocalPosition = _plateRestPosition;
		}
	}

	private static Mesh CreateBoxMesh( float width, float height, float thickness )
	{
		var halfWidth = width * 0.5f;
		var halfHeight = height * 0.5f;
		var halfThickness = thickness * 0.5f;

		var vertices = new Vertex[]
		{
			new() { Position = new Vector3( -halfHeight, -halfWidth, halfThickness ), Normal = Vector3.Forward },
			new() { Position = new Vector3( halfHeight, -halfWidth, halfThickness ), Normal = Vector3.Forward },
			new() { Position = new Vector3( halfHeight, halfWidth, halfThickness ), Normal = Vector3.Forward },
			new() { Position = new Vector3( -halfHeight, halfWidth, halfThickness ), Normal = Vector3.Forward },
			new() { Position = new Vector3( -halfHeight, -halfWidth, -halfThickness ), Normal = Vector3.Backward },
			new() { Position = new Vector3( halfHeight, -halfWidth, -halfThickness ), Normal = Vector3.Backward },
			new() { Position = new Vector3( halfHeight, halfWidth, -halfThickness ), Normal = Vector3.Backward },
			new() { Position = new Vector3( -halfHeight, halfWidth, -halfThickness ), Normal = Vector3.Backward }
		};

		var indices = new[]
		{
			0, 1, 2, 0, 2, 3,
			5, 4, 7, 5, 7, 6,
			4, 0, 3, 4, 3, 7,
			1, 5, 6, 1, 6, 2,
			3, 2, 6, 3, 6, 7,
			4, 5, 1, 4, 1, 0
		};

		var material = Material.Load( "materials/default.vmat" );
		var mesh = new Mesh( material );
		mesh.CreateVertexBuffer<Vertex>( vertices.Length, Vertex.Layout, vertices );
		mesh.CreateIndexBuffer( indices.Length, indices );
		mesh.Bounds = new BBox(
			new Vector3( -halfHeight, -halfWidth, -halfThickness ),
			new Vector3( halfHeight, halfWidth, halfThickness )
		);

		return mesh;
	}
}
