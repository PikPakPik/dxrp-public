namespace Dxura.RP.Game;

public partial class Player
{
	/// <summary>
	///     What effect should we spawn when a player gets headshot?
	/// </summary>
	[Property]
	[Feature( "Effects" )]
	private GameObject? HeadshotEffect { get; set; }

	/// <summary>
	///     What effect should we spawn when a player gets headshot while wearing a helmet?
	/// </summary>
	[Property]
	[Feature( "Effects" )]
	private GameObject? HeadshotWithHelmetEffect { get; set; }

	/// <summary>
	///     What effect should we spawn when we hit a player?
	/// </summary>
	[Property]
	[Feature( "Effects" )]
	private GameObject? BloodEffect { get; set; }

	/// <summary>
	///     What sound should we play when a player gets headshot?
	/// </summary>
	[Property]
	[Feature( "Effects" )]
	[Group( "Sounds" )]
	private SoundEvent? HeadshotSound { get; set; }

	/// <summary>
	///     What sound should we play when a player gets headshot?
	/// </summary>
	[Property]
	[Feature( "Effects" )]
	[Group( "Sounds" )]
	private SoundEvent HeadshotWithHelmetSound { get; } = null!;

	/// <summary>
	///     What sound should we play when we hit a player?
	/// </summary>
	[Property]
	[Feature( "Effects" )]
	[Group( "Sounds" )]
	private SoundEvent? BloodImpactSound { get; set; }

	/// <summary>
	///     What sound should we play when we change jobs?
	/// </summary>
	[Property]
	[Feature( "Effects" )]
	[Group( "Sounds" )]
	private SoundEvent? JobChangedSound { get; set; }

	[Property]
	[Feature( "Effects" )]
	[Group( "Sounds" )]
	public SoundEvent? LandSound { get; set; }

	/// <summary>
	///     The outline effect for this player.
	/// </summary>
	[RequireComponent]
	public HighlightOutline Outline { get; set; } = null!;


	private bool IsOutlineVisible()
	{
		var localPlayer = Local;
		if ( !localPlayer.IsValid() ||
		     localPlayer.HealthComponent.State != LifeState.Dead )
		{
			return false;
		}

		return localPlayer.GetLastKiller() == this;
	}

	/// <summary>
	/// Party silhouettes through geometry for the local viewer. Uses the existing player
	/// <see cref="HighlightOutline"/> plus the camera's <see cref="Highlight"/> post-process.
	/// </summary>
	private bool IsPartyOutlineVisible()
	{
		var localPlayer = Local;
		if ( !localPlayer.IsValid() || localPlayer == this || localPlayer.IsDead )
		{
			return false;
		}

		var party = PartySystem.Instance;
		if ( party is null || !party.AreInSameParty( localPlayer.SteamId, SteamId ) )
		{
			return false;
		}

		var partyId = party.GetPartyId( localPlayer.SteamId );
		return partyId.HasValue && party.IsMemberOutlineEnabled( partyId.Value );
	}

	private void OnUpdateEffects()
	{
		if ( IsPartyOutlineVisible() )
		{
			var partyColor = PartySystem.Instance!.GetPartyColorForMember( SteamId ).ToColor();
			Outline.Enabled = true;
			Outline.OverrideTargets = true;
			Outline.Targets = GetPartyOutlineTargets();
			Outline.Width = 0.12f;
			Outline.Color = partyColor.WithAlpha( 0.12f );
			Outline.InsideColor = Color.Transparent;
			Outline.InsideObscuredColor = Color.Transparent;
			Outline.ObscuredColor = partyColor.WithAlpha( 0.85f );
			return;
		}

		Outline.OverrideTargets = false;
		Outline.Targets = null;

		if ( !IsOutlineVisible() )
		{
			Outline.Enabled = false;
			return;
		}

		Outline.Enabled = true;
		Outline.Width = 0.1f;
		Outline.Color = Color.Transparent;
		Outline.InsideColor = HealthComponent.IsGodMode ? Color.White.WithAlpha( 0.1f ) : Color.Transparent;
		Outline.ObscuredColor = Color.Red;
	}

	/// <summary>
	/// Outline the dressed citizen body only — never equipment under hold bones, emotes, or nameplates.
	/// Targets must be <see cref="Renderer"/> instances, not <see cref="GameObject"/>.
	/// </summary>
	private List<Renderer> GetPartyOutlineTargets()
	{
		var targets = new List<Renderer>();

		if ( Renderer.IsValid() && Renderer.Enabled && Renderer.Model is { IsError: false } )
		{
			targets.Add( Renderer );
		}

		if ( !BodyRoot.IsValid() )
		{
			return targets;
		}

		foreach ( var skinnedRenderer in BodyRoot.GetComponentsInChildren<SkinnedModelRenderer>( true ) )
		{
			if ( skinnedRenderer == Renderer || skinnedRenderer == EmoteRenderer )
			{
				continue;
			}

			// Dresser clothing only — equipment rigs live under hold_* and must not be outlined.
			if ( !skinnedRenderer.GameObject.Name.StartsWith( "Clothing - ", StringComparison.Ordinal ) )
			{
				continue;
			}

			if ( !skinnedRenderer.Enabled || skinnedRenderer.Model is null || skinnedRenderer.Model.IsError )
			{
				continue;
			}

			targets.Add( skinnedRenderer );
		}

		return targets;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	private void HandleHeadshotEffects( DamageInfo damageInfo, Vector3 position, Player? attacker, Player? victim )
	{
		// Non-local viewer
		if ( IsProxy )
		{
			var go = damageInfo.HasHelmet
				? HeadshotWithHelmetEffect?.Clone( position )
				: HeadshotEffect?.Clone( position );
		}

		var headshotSound = damageInfo.HasHelmet ? HeadshotWithHelmetSound : HeadshotSound;
		headshotSound.Play( position );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	private void HandleBodyshotEffects( Vector3 position )
	{
		if ( BloodEffect.IsValid() )
		{
			BloodEffect?.Clone( new CloneConfig
			{
				StartEnabled = true, Transform = new Transform( position ), Name = $"Blood effect from ({GameObject})"
			} );
		}

		if ( BloodImpactSound is not null )
		{
			BloodImpactSound.Play( position );
		}
	}
}
