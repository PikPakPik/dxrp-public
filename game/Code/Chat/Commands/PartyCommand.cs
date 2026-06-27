using System.Globalization;
using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

/// <summary>
/// Player-facing party command. Runs host-side mutations through <see cref="PartySystem"/>.
/// Bare <c>/party</c> currently reports party info; it is reserved to open the management UI
/// in a later step.
/// </summary>
public class PartyCommand : ICommand
{
	public string Command => "party";
	public string Help => Language.GetPhrase( "party.command_help" );

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		var system = PartySystem.Instance;
		if ( system is null )
		{
			caller.Error( Language.GetPhrase( "party.ui_unavailable" ) );
			return true;
		}

		if ( args.Length == 0 )
		{
			// Reserved for the management UI later; report info in the meantime.
			system.HostInfo( caller );
			return true;
		}

		switch ( args[0].ToLowerInvariant() )
		{
			case "invite":
				if ( !TryResolveTarget( caller, args, out var inviteTarget ) )
				{
					return true;
				}

				system.HostInvite( caller, inviteTarget! );
				return true;

			case "kick":
				if ( !TryResolveTarget( caller, args, out var kickTarget ) )
				{
					return true;
				}

				system.HostKick( caller, kickTarget! );
				return true;

			case "accept":
				system.HostAccept( caller );
				return true;

			case "leave":
				system.HostLeave( caller );
				return true;

			case "disband":
				system.HostDisband( caller );
				return true;

			case "info":
				system.HostInfo( caller );
				return true;

			case "color":
				if ( args.Length < 2 )
				{
					caller.SendMessage( Language.GetPhrase( "party.color_usage" ) );
					return true;
				}

				if ( !TryParseHexColor( args[1], out var color ) )
				{
					caller.Error( Language.GetPhrase( "party.color_invalid" ) );
					return true;
				}

				system.HostSetColor( caller, color );
				return true;

			default:
				caller.SendMessage( Language.GetPhrase( "party.command_usage" ) );
				return true;
		}
	}

	private static bool TryResolveTarget( Player caller, string[] args, out Player? target )
	{
		target = null;

		if ( args.Length < 2 )
		{
			caller.SendMessage( Language.GetPhrase( "party.command_usage" ) );
			return false;
		}

		target = CommandHelper.ResolvePlayer( caller, args[1] );
		return target.IsValid();
	}

	/// <summary>
	/// Parses a 6-digit hex color (with or without a leading '#') into a packed 0xRRGGBB value.
	/// </summary>
	private static bool TryParseHexColor( string input, out uint packed )
	{
		packed = 0;

		if ( string.IsNullOrWhiteSpace( input ) )
		{
			return false;
		}

		var hex = input.Trim().TrimStart( '#' );
		if ( hex.Length != 6 )
		{
			return false;
		}

		return uint.TryParse( hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packed );
	}
}
