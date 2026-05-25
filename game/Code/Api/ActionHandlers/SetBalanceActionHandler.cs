using Dxura.RP.Shared;
namespace Dxura.RP.Game;

public class SetBalanceActionHandler : ActionHandler<SetBalanceActionDto>
{
	protected override void Execute( SetBalanceActionDto action )
	{
		var player = GameUtils.GetPlayerById( action.PlayerId );
		if ( !player.IsValid() )
		{
			return;
		}

		_ = player.SetBankBalanceHost( action.Balance );
		Log.Info( $"Set balance for {player.DisplayName} ({player.SteamId}) to ${action.Balance}" );
	}
}
