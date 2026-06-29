namespace Dxura.RP.Game;

/// <summary>
///     Disables ladder climb triggers while the ladder has the grabbed tag.
/// </summary>
public class LadderClimbGuard : Component
{
	private Collider? _climbTrigger;
	private bool _climbBlocked;

	protected override void OnStart()
	{
		// Repair ladders that lost their tag from older guard versions.
		GameObject.Tags.Set( Constants.LadderTag, true );
		RefreshClimbTrigger();
		UpdateClimbState( GameObject.Tags.Has( Constants.GrabbedTag ) );
	}

	protected override void OnFixedUpdate()
	{
		UpdateClimbState( GameObject.Tags.Has( Constants.GrabbedTag ) );
	}

	private void UpdateClimbState( bool blocked )
	{
		if ( blocked == _climbBlocked )
		{
			return;
		}

		_climbBlocked = blocked;

		if ( !_climbTrigger.IsValid() )
		{
			RefreshClimbTrigger();
		}

		if ( _climbTrigger.IsValid() )
		{
			_climbTrigger.Enabled = !blocked;
		}
	}

	private void RefreshClimbTrigger()
	{
		_climbTrigger = GameObject.GetComponentsInChildren<Collider>().FirstOrDefault( c => c.IsTrigger );
	}
}
