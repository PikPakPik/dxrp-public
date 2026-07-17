namespace Dxura.RP.Game.Statuses;

public class FreezeStatus : BaseStatus
{
	public override string Id => Constants.FreezeStatus;
	public override string Name => "#generic.frozen";
	public override string? MaterialIcon => "ac_unit";
	public override Color Color => Color.FromRgb( 0x00BCD4 );

	public override bool RemoveOnDeath => true;
	public override bool ShowOnNameplate => true;

	public override bool BlocksAction( Player player, StatusAction action )
	{
		return action == StatusAction.Emote;
	}

	public override bool BlocksCommand( Player player, ICommand command )
	{
		return !command.IsUsableWhileFrozen;
	}

	public override string? GetCommandBlockMessage( Player player, ICommand command )
	{
		return "#command.frozen";
	}

	public override void OnAddedServer( Player player )
	{
		// Holster current weapon
		player.Holster();
	}

	public override void OnAddedOwner( Player player )
	{
		player.CantSwitch = true;

		ApplyFreeze( player );
	}

	public override void OnRemovedOwner( Player player )
	{
		player.CantSwitch = false;

		// Restore base speeds
		player.Controller.WalkSpeed = GameConfig.WalkSpeed;
		player.Controller.RunSpeed = GameConfig.RunSpeed;
		player.Controller.DuckedSpeed = GameConfig.DuckedSpeed;
		player.Controller.JumpSpeed = GameConfig.JumpSpeed;
	}

	public override void OnUpdateOwner( Player player )
	{
		ApplyFreeze( player );
	}

	public override void OnAddedBroadcast( Player player )
	{
		if ( player.AnimationHelper.IsValid() )
		{
			player.AnimationHelper.HoldTypePose = 3;
		}
	}

	public override void OnRemovedBroadcast( Player player )
	{
		if ( player.AnimationHelper.IsValid() )
		{
			player.AnimationHelper.HoldTypePose = 0;
		}
	}

	private static void ApplyFreeze( Player player )
	{
		player.CantSwitch = true;

		player.Controller.WalkSpeed = 0f;
		player.Controller.RunSpeed = 0f;
		player.Controller.DuckedSpeed = 0f;
		player.Controller.JumpSpeed = 0f;
		player.Controller.WishVelocity = Vector3.Zero;
		player.Controller.GroundVelocity = Vector3.Zero;

		if ( player.Controller.Body.IsValid() )
		{
			player.Controller.Body.Velocity = Vector3.Zero;
		}
	}
}
