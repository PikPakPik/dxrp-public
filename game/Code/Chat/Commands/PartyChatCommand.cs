using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

/// <summary>
/// Sends a message to the caller's party channel (<see cref="MessageType.PartyChat"/>).
/// Slash-command entry point for the Tab-selectable PARTY chat mode, matching the
/// framework's other channel commands.
/// </summary>
public class PartyChatCommand : ICommand
{
	public string Command => "partychat";

	public string[] Aliases => new[]
	{
		"pchat"
	};

	public string Help => Language.GetPhrase( "party.chat_help" );
	public bool IsUsableWhileRestricted => true;
	public bool IsUsableWhileDead => true;

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( !caller.IsValid() )
		{
			return false;
		}

		var system = PartySystem.Instance;
		if ( system is null )
		{
			caller.Error( Language.GetPhrase( "party.ui_unavailable" ) );
			return true;
		}

		var partyId = system.GetPartyId( caller.SteamId );
		if ( !partyId.HasValue )
		{
			caller.SendMessage( Language.GetPhrase( "party.chat_not_in_party" ) );
			return true;
		}

		var message = ExtractMessage( raw );
		if ( string.IsNullOrWhiteSpace( message ) )
		{
			caller.SendMessage( Language.GetPhrase( "party.chat_usage" ) );
			return true;
		}

		if ( Cooldown.Current.CheckAndStartCooldown( $"{caller.SteamId}:chat", Config.Current.Game.ChatCooldown ) )
		{
			caller.Error( "#generic.wait" );
			return true;
		}

		message = message.Truncate( Config.Current.Game.ChatMaxLength );
		message = GameManager.ModerateText( caller.SteamId, $"CHAT {MessageType.PartyChat}", message, true );

		var members = system.GetMembers( partyId.Value ).ToHashSet();
		var partyConnections = GameUtils.Players
			.Where( p => p.IsValid() && members.Contains( p.SteamId ) )
			.Select( p => p.Connection )
			.ToHashSet();

		using ( Rpc.FilterInclude( c => partyConnections.Contains( c ) ) )
		{
			Chat.Current.BroadcastPlayerChat( Guid.NewGuid(), caller.ConnectionId, message, MessageType.PartyChat );
		}

		return true;
	}

	private static string ExtractMessage( string raw )
	{
		var firstSpace = raw.IndexOf( ' ' );
		return firstSpace < 0 ? string.Empty : raw[(firstSpace + 1)..].Trim();
	}
}
