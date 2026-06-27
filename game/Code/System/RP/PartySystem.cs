using Dxura.RP.Shared;

namespace Dxura.RP.Game;

/// <summary>
/// Operator-tunable settings for the Party system. Exposed as a "System" content type
/// config override (e.g. MaxPartySize / PreventPartyDamage) in the web portal.
/// </summary>
public class PartySystemConfig
{
	/// <summary>Maximum members allowed in a single party, leader included.</summary>
	public int MaxPartySize { get; init; } = 4;

	/// <summary>When true, party members cannot damage each other (used in a later step).</summary>
	public bool PreventPartyDamage { get; init; } = false;
}

/// <summary>
/// Standalone, host-authoritative, session-only party system. Parties are on-the-fly squads
/// and are intentionally separate from the persistent <see cref="FactionSystem"/>: nothing here
/// reads, writes, or depends on factions.
///
/// State is stored as flat normalized maps (rather than nested lists) so it replicates cleanly
/// over <see cref="SyncFlags.FromHost"/>. Clients read this synced state; only the host mutates it.
/// </summary>
public sealed class PartySystem : SingletonComponent<PartySystem>, Component.INetworkListener
{
	/// <summary>
	/// Runtime config. Populated with defaults today; portal "System" config-override plumbing
	/// is wired separately. Read-only at runtime.
	/// </summary>
	public PartySystemConfig Settings { get; private set; } = new();

	// ── Synced authoritative state (host → clients) ───────────────────────────────────────────
	// steamId → partyId
	[Sync( SyncFlags.FromHost )] public NetDictionary<long, Guid> MemberParty { get; set; } = new();
	// partyId → leader steamId
	[Sync( SyncFlags.FromHost )] public NetDictionary<Guid, long> PartyLeader { get; set; } = new();
	// steamId → join sequence (lower = older; used for deterministic leader transfer)
	[Sync( SyncFlags.FromHost )] public NetDictionary<long, int> MemberOrder { get; set; } = new();
	// invited target steamId → partyId they were invited to (one pending invite per player)
	[Sync( SyncFlags.FromHost )] public NetDictionary<long, Guid> PendingInvites { get; set; } = new();

	// Host-only monotonic counter for member join order.
	private int _nextOrder = 1;

	// ── Read helpers (synced data; safe on host and client) ───────────────────────────────────
	public bool IsInParty( long steamId ) => MemberParty.ContainsKey( steamId );

	public Guid? GetPartyId( long steamId ) =>
		MemberParty.TryGetValue( steamId, out var id ) ? id : null;

	public IEnumerable<long> GetMembers( Guid partyId ) =>
		MemberParty.Where( kv => kv.Value == partyId ).Select( kv => kv.Key );

	public int GetPartySize( Guid partyId ) => MemberParty.Count( kv => kv.Value == partyId );

	public long GetLeader( Guid partyId ) =>
		PartyLeader.TryGetValue( partyId, out var leader ) ? leader : 0;

	public bool IsLeader( long steamId )
	{
		var partyId = GetPartyId( steamId );
		return partyId.HasValue && GetLeader( partyId.Value ) == steamId;
	}

	public bool AreInSameParty( long a, long b )
	{
		var pa = GetPartyId( a );
		var pb = GetPartyId( b );
		return pa.HasValue && pb.HasValue && pa.Value == pb.Value;
	}

	private int OrderOf( long steamId ) => MemberOrder.TryGetValue( steamId, out var o ) ? o : int.MaxValue;

	/// <summary>
	/// Builds a client-readable view of a party for UI/HUD. This is display state only —
	/// <see cref="PartySystem"/> remains the single authority.
	/// </summary>
	public PartyRoom? GetRoomView( Guid partyId )
	{
		if ( !PartyLeader.TryGetValue( partyId, out var leader ) )
		{
			return null;
		}

		var members = GetMembers( partyId )
			.OrderBy( OrderOf )
			.Select( id =>
			{
				var player = GameUtils.GetPlayerById( id );
				var name = player.IsValid() ? player.DisplayName : id.ToString();
				return new PartyMember( id, name, id == leader );
			} )
			.ToList();

		return new PartyRoom { Id = partyId, LeaderSteamId = leader, Members = members };
	}

	// ── Host mutations (invoked from PartyCommand.ExecuteHost, which already runs on the host) ──

	/// <summary>Invite <paramref name="target"/> to the caller's party, auto-creating one if needed.</summary>
	public void HostInvite( Player caller, Player target )
	{
		if ( !Networking.IsHost || caller is null || target is null )
		{
			return;
		}

		if ( caller == target )
		{
			caller.Error( Language.GetPhrase( "party.invite_self_error" ) );
			return;
		}

		if ( IsInParty( target.SteamId ) )
		{
			caller.Error( string.Format( Language.GetPhrase( "party.target_in_party" ), target.DisplayName ) );
			return;
		}

		var partyId = GetPartyId( caller.SteamId );
		if ( !partyId.HasValue )
		{
			partyId = CreatePartyForLeader( caller );
		}
		else if ( !IsLeader( caller.SteamId ) )
		{
			caller.Error( Language.GetPhrase( "party.not_leader" ) );
			return;
		}

		if ( GetPartySize( partyId.Value ) >= Settings.MaxPartySize )
		{
			caller.Error( Language.GetPhrase( "party.party_full" ) );
			return;
		}

		PendingInvites[target.SteamId] = partyId.Value;

		caller.Success( string.Format( Language.GetPhrase( "party.invite_sent" ), target.DisplayName ) );
		target.Info( string.Format( Language.GetPhrase( "party.invite_received" ), caller.DisplayName ) );
	}

	/// <summary>Accept the single pending invite for the caller.</summary>
	public void HostAccept( Player caller )
	{
		if ( !Networking.IsHost || caller is null )
		{
			return;
		}

		if ( IsInParty( caller.SteamId ) )
		{
			caller.Error( Language.GetPhrase( "party.already_in_party" ) );
			return;
		}

		if ( !PendingInvites.TryGetValue( caller.SteamId, out var partyId ) )
		{
			caller.Error( Language.GetPhrase( "party.invite_none" ) );
			return;
		}

		// The party may have disbanded between invite and accept.
		if ( !PartyLeader.ContainsKey( partyId ) )
		{
			PendingInvites.Remove( caller.SteamId );
			caller.Error( Language.GetPhrase( "party.invite_expired" ) );
			return;
		}

		if ( GetPartySize( partyId ) >= Settings.MaxPartySize )
		{
			caller.Error( Language.GetPhrase( "party.party_full" ) );
			return;
		}

		PendingInvites.Remove( caller.SteamId );
		MemberParty[caller.SteamId] = partyId;
		MemberOrder[caller.SteamId] = _nextOrder++;

		NotifyParty( partyId, string.Format( Language.GetPhrase( "party.joined" ), caller.DisplayName ) );
	}

	/// <summary>Leader kicks <paramref name="target"/> from the party.</summary>
	public void HostKick( Player caller, Player target )
	{
		if ( !Networking.IsHost || caller is null || target is null )
		{
			return;
		}

		var partyId = GetPartyId( caller.SteamId );
		if ( !partyId.HasValue )
		{
			caller.Error( Language.GetPhrase( "party.no_party" ) );
			return;
		}

		if ( !IsLeader( caller.SteamId ) )
		{
			caller.Error( Language.GetPhrase( "party.not_leader" ) );
			return;
		}

		if ( caller == target )
		{
			caller.Error( Language.GetPhrase( "party.cannot_kick_self" ) );
			return;
		}

		if ( GetPartyId( target.SteamId ) != partyId )
		{
			caller.Error( string.Format( Language.GetPhrase( "party.target_not_found" ), target.DisplayName ) );
			return;
		}

		RemovePlayerInternal( target.SteamId );
		target.Warn( Language.GetPhrase( "party.kicked" ) );
		NotifyParty( partyId.Value, string.Format( Language.GetPhrase( "party.member_kicked" ), target.DisplayName ) );
	}

	/// <summary>Caller leaves their party (disbands it if they were the last member).</summary>
	public void HostLeave( Player caller )
	{
		if ( !Networking.IsHost || caller is null )
		{
			return;
		}

		var partyId = GetPartyId( caller.SteamId );
		if ( !partyId.HasValue )
		{
			caller.Error( Language.GetPhrase( "party.no_party" ) );
			return;
		}

		RemovePlayerInternal( caller.SteamId );
		caller.Info( Language.GetPhrase( "party.left" ) );

		// Notify whoever remains (no-op if the party disbanded).
		if ( PartyLeader.ContainsKey( partyId.Value ) )
		{
			NotifyParty( partyId.Value, string.Format( Language.GetPhrase( "party.member_left" ), caller.DisplayName ) );
		}
	}

	/// <summary>Leader disbands the whole party.</summary>
	public void HostDisband( Player caller )
	{
		if ( !Networking.IsHost || caller is null )
		{
			return;
		}

		var partyId = GetPartyId( caller.SteamId );
		if ( !partyId.HasValue )
		{
			caller.Error( Language.GetPhrase( "party.no_party" ) );
			return;
		}

		if ( !IsLeader( caller.SteamId ) )
		{
			caller.Error( Language.GetPhrase( "party.not_leader" ) );
			return;
		}

		DisbandParty( partyId.Value );
	}

	/// <summary>Sends the caller a summary of their current party.</summary>
	public void HostInfo( Player caller )
	{
		if ( caller is null )
		{
			return;
		}

		var partyId = GetPartyId( caller.SteamId );
		var room = partyId.HasValue ? GetRoomView( partyId.Value ) : null;
		if ( room is null )
		{
			caller.SendMessage( Language.GetPhrase( "party.no_party" ) );
			return;
		}

		var names = string.Join( ", ", room.Members.Select( m =>
			m.IsLeader ? string.Format( Language.GetPhrase( "party.leader_tag" ), m.Name ) : m.Name ) );

		caller.SendMessage( string.Format( Language.GetPhrase( "party.info" ), room.Members.Count, Settings.MaxPartySize, names ) );
	}

	// ── Internal helpers (host-side) ────────────────────────────────────────────────────────
	private Guid CreatePartyForLeader( Player leader )
	{
		var partyId = Guid.NewGuid();
		PartyLeader[partyId] = leader.SteamId;
		MemberParty[leader.SteamId] = partyId;
		MemberOrder[leader.SteamId] = _nextOrder++;
		leader.Info( Language.GetPhrase( "party.created" ) );
		return partyId;
	}

	/// <summary>
	/// Removes a player from their party, disbanding it if empty or transferring leadership
	/// to the oldest remaining member when the leader leaves.
	/// </summary>
	private void RemovePlayerInternal( long steamId )
	{
		if ( !MemberParty.TryGetValue( steamId, out var partyId ) )
		{
			return;
		}

		MemberParty.Remove( steamId );
		MemberOrder.Remove( steamId );
		PendingInvites.Remove( steamId );

		var remaining = GetMembers( partyId ).ToList();
		if ( remaining.Count == 0 )
		{
			DisbandParty( partyId );
			return;
		}

		if ( PartyLeader.TryGetValue( partyId, out var leader ) && leader == steamId )
		{
			var newLeader = remaining.OrderBy( OrderOf ).First();
			PartyLeader[partyId] = newLeader;

			var promoted = GameUtils.GetPlayerById( newLeader );
			if ( promoted.IsValid() )
			{
				promoted.Info( Language.GetPhrase( "party.leader_changed" ) );
			}
		}
	}

	private void DisbandParty( Guid partyId )
	{
		foreach ( var id in GetMembers( partyId ).ToList() )
		{
			MemberParty.Remove( id );
			MemberOrder.Remove( id );

			var player = GameUtils.GetPlayerById( id );
			if ( player.IsValid() )
			{
				player.Info( Language.GetPhrase( "party.disbanded" ) );
			}
		}

		PartyLeader.Remove( partyId );

		foreach ( var invite in PendingInvites.Where( kv => kv.Value == partyId ).ToList() )
		{
			PendingInvites.Remove( invite.Key );
		}
	}

	private void NotifyParty( Guid partyId, string message )
	{
		foreach ( var id in GetMembers( partyId ) )
		{
			var player = GameUtils.GetPlayerById( id );
			if ( player.IsValid() )
			{
				player.Info( message );
			}
		}
	}

	// ── INetworkListener: clean up parties when a player disconnects ──────────────────────────
	public void OnDisconnected( Connection channel )
	{
		if ( !Networking.IsHost || channel is null )
		{
			return;
		}

		RemovePlayerInternal( channel.SteamId );
		PendingInvites.Remove( channel.SteamId );
	}
}
