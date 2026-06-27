namespace Dxura.RP.Game;

public partial class Player
{
	/// <summary>
	///     When the player became AFK. Synced from host; null when not AFK.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public TimeSince? AfkSince { get; set; }

	public float AfkDuration => AfkSince?.Relative ?? 0f;

	public string GetAfkDurationText() => TimeUtils.Format( AfkDuration, TimeDisplayFormat.Duration );
}
