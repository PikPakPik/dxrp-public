namespace Dxura.RP.Game.Addons.Official.Volleyball;

[AddonService]
public sealed class VolleyballService : Component
{
	protected override void OnStart()
	{
		Log.Info( "Volleyball Addon Loaded" );
	}
}
