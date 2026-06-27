namespace Dxura.RP.Game.Wire;

[Title( "Meta" )]
[Category( "Wire" )]
[Icon( "cable" )]
public class MetaWire() : BaseWireConstruct( ConstructType.MetaWire ), IWireEvents
{
	[WireOutput( "tax_rate" )]
	public float TaxRate { get; private set; }

	[WireOutput( "laws" )]
	public string Laws { get; private set; } = string.Empty;

	[WireOutput( "law_count" )]
	public float LawCount { get; private set; }

	[WireOutput( "mayor" )]
	public string Mayor { get; private set; } = string.Empty;

	[WireOutput( "has_mayor" )]
	public bool HasMayor { get; private set; }

	[WireOutput( "town_name" )]
	public string TownName { get; private set; } = string.Empty;

	[WireOutput( "treasury" )]
	public float Treasury { get; private set; }

	[WireOutput( "lockdown" )]
	public bool Lockdown { get; private set; }

	public override string Name => "Meta";

	protected override void OnStart()
	{
		base.OnStart();
		RefreshOutputs();
	}

	public void OnWireTick()
	{
		RefreshOutputs();
	}

	private void RefreshOutputs()
	{
		var governance = Governance.Current;
		var mayor = GameUtils.GetPlayersByJobTag( JobTag.Mayoral ).FirstOrDefault();

		var taxRate = Config.Current.Game.GovernanceTaxEnabled && governance != null
			? governance.TaxRate * 100f
			: 0f;

		var laws = Config.Current.Game.GovernanceLawEnabled && governance != null
			? FormatLaws( governance.GetAllLaws() )
			: string.Empty;

		var lawCount = Config.Current.Game.GovernanceLawEnabled && governance != null
			? governance.GetAllLaws().Count()
			: 0;

		var mayorName = mayor?.SteamName ?? string.Empty;
		var hasMayor = mayor != null;

		var townName = Config.Current.Game.GovernanceTaxEnabled && governance != null
			? governance.TownName ?? string.Empty
			: string.Empty;

		var treasury = Config.Current.Game.GovernanceTaxEnabled && governance != null
			? governance.GovernmentBalance
			: 0u;

		var lockdown = Config.Current.Game.GovernanceLockdownEnabled
			&& LockdownSystem.Instance.IsValid()
			&& LockdownSystem.Instance.Lockdown;

		if ( TaxRate == taxRate
			&& Laws == laws
			&& LawCount == lawCount
			&& Mayor == mayorName
			&& HasMayor == hasMayor
			&& TownName == townName
			&& Treasury == treasury
			&& Lockdown == lockdown )
		{
			return;
		}

		TaxRate = taxRate;
		Laws = laws;
		LawCount = lawCount;
		Mayor = mayorName;
		HasMayor = hasMayor;
		TownName = townName;
		Treasury = treasury;
		Lockdown = lockdown;
	}

	private static string FormatLaws( IEnumerable<string> laws )
	{
		var formattedLaws = laws
			.Select( ( law, index ) => $"{index + 1}. {LocalizeLaw( law )}" )
			.ToList();

		return formattedLaws.Count == 0 ? string.Empty : string.Join( '\n', formattedLaws );
	}

	private static string LocalizeLaw( string law )
	{
		var phrase = Language.GetPhrase( law );
		if ( phrase == law && law.StartsWith( '#' ) )
		{
			phrase = Language.GetPhrase( law[1..] );
		}

		return phrase;
	}
}
