namespace Dxura.RP.Game.Addons.Official.Volleyball;

using Equipments;

/// <summary>
/// Volleyball: grab with hands (attack1). Use (E) while aimed at the ball in reach bumps it — not while holding.
/// Very soft scripted bounces on hard impacts only.
/// </summary>
[Title( "Volleyball" )]
[Category( "Entities" )]
public sealed class VolleyballEntity : BaseEntity, Component.IPressable
{
	[Property] [Group( "Bump" )] [Range( 100f, 1200f )]
	public float AirBumpForce { get; set; } = 580f;

	[Property] [Group( "Bump" )] [Range( 0f, 1.5f )]
	public float UpwardBias { get; set; } = 0.42f;

	[Property] [Group( "Bump" )] [Range( 0.08f, 0.8f )]
	public float BumpCooldown { get; set; } = 0.22f;

	[Property] public SoundEvent? BumpSound { get; set; }

	[Property] [Group( "Bounce" )] [Range( 0.05f, 1.0f )]
	public float Bounciness { get; set; } = 0.12f;

	[Property] [Group( "Bounce" )] [Range( 0f, 1f )]
	public float FloorFriction { get; set; } = 0.72f;

	[Property] [Group( "Bounce" )] [Range( 0f, 1f )]
	public float WallFriction { get; set; } = 0.92f;

	[Property] [Group( "Bounce" )] [Range( 0.05f, 0.35f )]
	public float PhysicsBounceCooldown { get; set; } = 0.09f;

	[Property] [Group( "Bounce" )] [Range( 4f, 80f )]
	public float MinBounceSpeed { get; set; } = 24f;

	[Property] [Group( "Bounce" )] [Range( -1f, 0f )]
	public float MinBounceAngle { get; set; } = -0.45f;

	[Property] [Group( "Bounce" )] [Range( 0.15f, 1.2f )]
	public float TraceDistanceMultiplier { get; set; } = 0.55f;

	[Property] [Group( "Bounce" )] [Range( 1f, 90f )]
	public float FloorAngleThreshold { get; set; } = 78f;

	[Property] [Group( "Bounce" )] [Range( 1f, 25f )]
	public float RollingSpeedThreshold { get; set; } = 3f;

	[Property] [Group( "Bounce Sound" )]
	public SoundEvent? BounceSound { get; set; }

	[Property] [Group( "Bounce Sound" )] [Range( 0.01f, 1f )]
	public float MinBounceVolume { get; set; } = 0.03f;

	[Property] [Group( "Bounce Sound" )] [Range( 0.05f, 2f )]
	public float MaxBounceVolume { get; set; } = 1.4f;

	private TimeSince _lastPhysicsBounce = 10f;
	private float _cachedRadius;
	private readonly SceneTraceResult[] _traceResults = new SceneTraceResult[3];

	private float CurrentRadius => _cachedRadius * WorldScale.x;

	private float FloorDetectionThreshold => MathF.Cos( FloorAngleThreshold * MathF.PI / 180f );

	protected override void OnStart()
	{
		base.OnStart();

		_cachedRadius = Components.Get<SphereCollider>()?.Radius ?? 10f;

		if ( Rigidbody.IsValid() )
		{
			Rigidbody.Gravity = true;
			Rigidbody.MotionEnabled = true;
		}

		GameObject.Tags.Add( Constants.EntityTag );
		if ( !GameObject.Tags.Has( Constants.HandsInteractTag ) )
		{
			GameObject.Tags.Add( Constants.HandsInteractTag );
		}
	}

	public bool CanPress( IPressable.Event e )
	{
		return true;
	}

	public bool Press( IPressable.Event e )
	{
		_ = e;

		var hands = Player.Local.GetComponentInChildren<HandsEquipment>();
		if ( hands.IsValid() && hands.IsHolding( GameObject, true ) )
		{
			return false;
		}

		if ( !Player.Local.IsValid() )
		{
			return false;
		}

		if ( hands.IsValid() && hands.IsHolding( GameObject ) )
		{
			return false;
		}

		if ( Cooldown.Current.CheckAndStartCooldown( "volleyball:bump:air", BumpCooldown, true ) )
		{
			return false;
		}

		return TryBumpFromAir( Player.Local );
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( IsProxy || IsGrabbed() || !Rigidbody.IsValid() || Application.IsHeadless )
		{
			return;
		}

		var vel = Rigidbody.Velocity;
		var pos = WorldPosition;

		if ( vel.Length > 0.45f && _lastPhysicsBounce > PhysicsBounceCooldown )
		{
			var velDir = vel.Normal;
			var traceDist = CurrentRadius + vel.Length * Time.Delta * TraceDistanceMultiplier;
			var offset = CurrentRadius * 0.05f;

			_traceResults[0] = Scene.Trace.Ray( pos, pos + velDir * traceDist ).WithoutTags( Constants.PlayerTag, Constants.NoCollideTag ).IgnoreGameObject( GameObject ).Run();
			_traceResults[1] = Scene.Trace.Ray( pos + Vector3.Up * offset, pos + Vector3.Up * offset + velDir * traceDist ).WithoutTags( Constants.PlayerTag, Constants.NoCollideTag ).IgnoreGameObject( GameObject ).Run();
			_traceResults[2] = Scene.Trace.Ray( pos + Vector3.Down * offset, pos + Vector3.Down * offset + velDir * traceDist ).WithoutTags( Constants.PlayerTag, Constants.NoCollideTag ).IgnoreGameObject( GameObject ).Run();

			foreach ( var result in _traceResults )
			{
				if ( !result.Hit )
				{
					continue;
				}

				var speed = vel.Length;
				var normal = result.Normal;
				var dotProduct = Vector3.Dot( vel, normal );

				if ( dotProduct < MinBounceAngle && speed > MinBounceSpeed )
				{
					var impactEnergy = 0.5f * speed * speed;
					var isFloor = Vector3.Dot( normal, Vector3.Up ) > FloorDetectionThreshold;
					var friction = isFloor ? FloorFriction : WallFriction;
					var bounceEnergy = impactEnergy * Bounciness * (1f - friction);
					var bounceSpeed = (float)Math.Sqrt( Math.Max( 0f, 2f * bounceEnergy ) );
					var reflectedVelocity = (vel - 2 * dotProduct * normal).Normal * bounceSpeed;

					var angularVel = Rigidbody.AngularVelocity;
					if ( angularVel.Length > 0.5f && isFloor )
					{
						var spinDirection = Vector3.Cross( angularVel.Normal, Vector3.Up );
						reflectedVelocity += spinDirection * bounceSpeed * 0.12f;

						if ( speed > 18f )
						{
							Rigidbody.AngularVelocity = angularVel * 0.82f;
						}
					}

					if ( Rigidbody.IsValid() && Rigidbody.MotionEnabled )
					{
						Rigidbody.Velocity = reflectedVelocity;

						if ( !(angularVel.Length > 0.5f && isFloor) )
						{
							var horizontal = new Vector3( reflectedVelocity.x, reflectedVelocity.y, 0 );
							if ( horizontal.Length > RollingSpeedThreshold )
							{
								Rigidbody.AngularVelocity = Vector3.Cross( Vector3.Up, horizontal.Normal ) * (horizontal.Length / CurrentRadius);
							}
						}
					}

					_lastPhysicsBounce = 0f;

					var intensity = speed / 320f;
					var volume = Math.Clamp( intensity, MinBounceVolume, MaxBounceVolume );
					PlayBounceSoundHost( WorldPosition, volume, intensity );
					break;
				}
			}
		}

		if ( vel.Length > 0.12f && !IsGrabbed() )
		{
			var horizontal = new Vector3( vel.x, vel.y, 0 );
			if ( horizontal.Length > 1f && (Math.Abs( vel.z ) < 12f || vel.z > -6f) )
			{
				var currentAngularSpeed = Rigidbody.AngularVelocity.Length;
				var targetAngularVel = Vector3.Cross( Vector3.Up, horizontal.Normal ) * (horizontal.Length / CurrentRadius);

				if ( currentAngularSpeed < 22f )
				{
					Rigidbody.AngularVelocity = Vector3.Lerp( Rigidbody.AngularVelocity, targetAngularVel, Time.Delta * 5f );
				}
			}
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	private void BroadcastBounceSound( Vector3 position, float volume, float intensity )
	{
		if ( BounceSound?.IsValid() == true )
		{
			var handle = BounceSound.Play( position );
			if ( handle.IsValid() )
			{
				handle.Volume = volume;
				handle.Pitch = Math.Clamp( 0.82f + intensity * 0.35f, 0.82f, 1.18f );
			}
		}
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Unreliable )]
	private void PlayBounceSoundHost( Vector3 position, float volume, float intensity )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:volleyball:bounce", Config.Current.Game.ActionQuickCooldown ) )
		{
			return;
		}

		BroadcastBounceSound( position, volume, intensity );
	}

	private bool IsGrabbed()
	{
		return GameObject.Tags.Has( Constants.GrabbedTag );
	}

	private bool TryBumpFromAir( Player player )
	{
		if ( IsGrabbed() )
		{
			return false;
		}

		var tr = Scene.Trace.Ray( player.AimRay, Config.Current.Game.ReachDistance )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.UseHitboxes()
			.Run();

		if ( !tr.Hit || tr.GameObject.Root != GameObject.Root )
		{
			return false;
		}

		if ( !GameObject.Network.IsOwner && !GameManager.Instance.RequestOwnership( GameObject ) )
		{
			return false;
		}

		ApplyBump( player, AirBumpForce );
		BumpSound?.Broadcast( WorldPosition );
		return true;
	}

	private void ApplyBump( Player player, float force )
	{
		if ( !Rigidbody.IsValid() || !Rigidbody.MotionEnabled )
		{
			return;
		}

		var dir = (player.AimRay.Forward + Vector3.Up * UpwardBias).Normal;
		Rigidbody.Velocity = dir * force;
		Rigidbody.AngularVelocity = Vector3.Zero;
	}

	public override bool CanScale( Player player )
	{
		if ( !this.IsValid() || !player.IsValid() )
		{
			return false;
		}

		return player.SteamId == Owner && GameUtils.HasPermission( player.SteamId, GameObject );
	}
}
