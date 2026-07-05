namespace Dxura.RP.Shared;

// Dispatches a staff/purchase-issued command to run against a specific player, bypassing the normal
// player-facing chat command's cooldown/permission checks (ManualActionActionHandler invokes the command
// directly via Chat.TryGetCommand). Targeted at the player's current server only, not broadcast to every
// server the tenant owns - a command with side effects (e.g. granting currency) must not run once per
// server the player isn't even on.
public class ManualActionActionDto : BaseServerActionDto
{
	public required long PlayerId { get; set; }
	public required string CommandName { get; set; }
	public List<string> Args { get; set; } = [];
}
