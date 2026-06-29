namespace Dxura.RP.Game.Equipments;

public class CanEquipment : InputWeaponComponent, IInputHints
{
	[Property] [Group( "Effects" )]
	public required SoundEvent JiggleSoundEvent { get; set; }
	[Property] [Group( "Effects" )]
	public required SoundEvent TauntSoundEvent { get; set; }

	IEnumerable<(string Action, string Label)> IInputHints.GetInputHints()
	{
		yield return ("attack1", "#input.can.shake");
		yield return ("attack2", "#input.can.taunt");
	}

	protected override void OnInputDown()
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "can", Config.Current.Game.ActionQuickCooldown ) )
		{
			return;
		}

		var taunt = Input.Down( "Attack2" );

		if ( taunt && !Cooldown.Current.CheckAndStartCooldown( "hobo:taunt", Config.Current.Game.HoboTauntCooldown, true ) )
		{
			TauntSoundEvent.Broadcast( Player.Local.WorldPosition, Player.Local.GameObject );
		}
		else
		{
			JiggleSoundEvent.Broadcast( Player.Local.WorldPosition, Player.Local.GameObject );
		}
	}
}
