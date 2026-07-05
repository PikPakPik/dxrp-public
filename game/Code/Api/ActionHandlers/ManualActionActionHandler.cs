using Dxura.RP.Shared;
namespace Dxura.RP.Game;

public class ManualActionActionHandler : ActionHandler<ManualActionActionDto>
{
	protected override void Execute( ManualActionActionDto action )
	{
		var player = GameUtils.GetPlayerById( action.PlayerId );
		if ( !player.IsValid() )
		{
			return;
		}

		if ( !Chat.Current.TryGetCommand( action.CommandName, out var command ) || command == null )
		{
			Log.Warning( $"Manual action dispatch failed - unknown command '{action.CommandName}'" );
			return;
		}

		// Bypasses the normal player-facing cooldown/permission checks (ExecuteCommandHost/HandlePlayerCommands) -
		// intentional, since this is staff/purchase-issued via the portal Products tab or a Stripe purchase, not
		// something a player typed themselves.
		var raw = $"/{action.CommandName} {string.Join( ' ', action.Args )}".TrimEnd();
		command.ExecuteHost( player, action.Args.ToArray(), raw );
	}
}
