using Sandbox;
using System;

namespace Dxura.RP.Game;

public sealed class Elevator : Component
{
	[Property] public float Speed { get; set; } = 10f;
	[Property] public float WaitPeriod { get; set; } = 3f;
	[Property] public Vector3 TopPosition { get; set; }

	private Vector3 _startPosition;
	private Vector3 _targetPosition;
	private TimeSince _idleTime = 0;
	private Collider? _collider;

	protected override void OnStart()
	{
		_startPosition = WorldPosition;
		_targetPosition = TopPosition;
		_collider = GameObject.GetComponentInChildren<Collider>();
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		if ( _idleTime <= WaitPeriod )
		{
			return;
		}

		// Check if we've reached the target position (or are very close)
		if ( Vector3.DistanceBetween( WorldPosition, _targetPosition ) < 0.5f )
		{
			// Reached target, now wait
			WorldPosition = _targetPosition; // Snap to exact position
			_idleTime = 0;

			// Toggle between top and bottom positions
			_targetPosition = _targetPosition == _startPosition ? TopPosition : _startPosition;

			return;
		}

		// Move at constant speed regardless of distance
		var moveDirection = (_targetPosition - WorldPosition).Normal;
		var distanceToMove = Math.Min( Speed * Time.Delta, Vector3.DistanceBetween( WorldPosition, _targetPosition ) );
		WorldPosition += moveDirection * distanceToMove;

		KillCrushedPlayers( moveDirection );
	}

	private void KillCrushedPlayers( Vector3 moveDirection )
	{
		if ( moveDirection.z >= 0f || !_collider.IsValid() )
		{
			return;
		}

		var players = Scene.FindInPhysics( _collider.GetWorldBounds() )
			.Select( gameObject => gameObject.Root )
			.Where( gameObject => gameObject.Tags.Has( Constants.PlayerTag ) && gameObject.WorldPosition.z < WorldPosition.z )
			.Distinct();

		foreach ( var gameObject in players )
		{
			var player = gameObject.GetComponent<Player>();
			if ( !player.IsValid() || !player.Controller.IsValid() || !player.Controller.IsOnGround )
			{
				continue;
			}

			player.HealthComponent.IsGodMode = false;
			player.HealthComponent.TakeDamageHost( new DamageInfo( this, float.MaxValue, this, player.WorldPosition ) );
		}
	}
}
