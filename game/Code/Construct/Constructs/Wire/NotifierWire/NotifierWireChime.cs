namespace Dxura.RP.Game.Wire;

public enum NotifierWireChime
{
	None,
	Magic,
	Bell,
	Gling1,
	Gling2,
	Gling3
}

public static class NotifierWireChimeExtensions
{
	public static string? GetSound( this NotifierWireChime chime )
	{
		return chime switch
		{
			NotifierWireChime.Magic => "magic",
			NotifierWireChime.Bell => "bell",
			NotifierWireChime.Gling1 => "gling1",
			NotifierWireChime.Gling2 => "gling2",
			NotifierWireChime.Gling3 => "gling3",
			_ => null
		};
	}
}
