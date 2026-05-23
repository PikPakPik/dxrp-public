using System.Text.Json;

namespace Dxura.RP.Game;

public class TextDefinition : ConstructDefinition<Text, TextData>
{
	public override ConstructType Type => ConstructType.Text;
	public override uint Limit => Config.Current.Game.TextLimit;
	public override uint DataSchemaVersion => 2;

	public const uint MinTextLength = 1;
	public const uint MaxTextLength = 150;
	public const uint MinFontSize = 30;
	public const uint MaxFontSize = 200;
	public const uint MinOutlineSize = 1;
	public const uint MaxOutlineSize = 20;
	public const uint MinFontWeight = 100;
	public const uint MaxFontWeight = 800;
	public const uint MinLines = 1;
	public const uint MaxLines = 4;

	public override IConstructData? Migrate( uint fromVersion, JsonElement data, ConstructDataSerializer serializer )
	{
		if ( fromVersion == 1 )
		{
			var line = new TextLineData();

			if ( data.TryGetProperty( "text", out var t ) ) line.Text = t.GetString() ?? "";
			if ( data.TryGetProperty( "fontSize", out var f ) ) line.FontSize = f.GetSingle();
			if ( data.TryGetProperty( "italic", out var it ) ) line.Italic = it.GetBoolean();
			if ( data.TryGetProperty( "fontWeight", out var fw ) ) line.FontWeight = fw.GetInt32();
			if ( data.TryGetProperty( "outline", out var o ) ) line.Outline = o.GetBoolean();
			if ( data.TryGetProperty( "outlineSize", out var os ) ) line.OutlineSize = os.GetSingle();
			line.Color = ParseColor( data, "color", Color.White );
			line.OutlineColor = ParseColor( data, "outlineColor", Color.Black );

			return new TextData { Lines = new List<TextLineData> { line } };
		}

		return null;
	}

	private static Color ParseColor( JsonElement data, string propertyName, Color fallback )
	{
		if ( !data.TryGetProperty( propertyName, out var colorEl ) ) return fallback;

		float r = colorEl.TryGetProperty( "r", out var ri ) ? ri.GetSingle() : fallback.r;
		float g = colorEl.TryGetProperty( "g", out var gi ) ? gi.GetSingle() : fallback.g;
		float b = colorEl.TryGetProperty( "b", out var bi ) ? bi.GetSingle() : fallback.b;
		float a = colorEl.TryGetProperty( "a", out var ai ) ? ai.GetSingle() : fallback.a;

		return new Color( r, g, b, a );
	}

	protected override ConstructDataValidationResult ValidateTyped( TextData data )
	{
		var lines = data.Lines;

		if ( lines == null || lines.Count < MinLines )
			return ConstructDataValidationResult.Failure( "At least one text line is required" );

		if ( lines.Count > MaxLines )
			return ConstructDataValidationResult.Failure( $"Maximum {MaxLines} text lines allowed" );

		// Empty lines are allowed — they are simply skipped at render time.
		// Only validate lines that have content.
		bool hasContent = false;

		foreach ( var line in lines )
		{
			if ( string.IsNullOrEmpty( line.Text ) ) continue;

			hasContent = true;

			if ( line.Text.Length > MaxTextLength )
				return ConstructDataValidationResult.Failure( $"Text exceeds maximum of {MaxTextLength} characters" );

			if ( line.FontSize < MinFontSize )
				return ConstructDataValidationResult.Failure( $"Font size must be at least {MinFontSize}" );

			if ( line.FontSize > MaxFontSize )
				return ConstructDataValidationResult.Failure( $"Font size exceeds maximum of {MaxFontSize}" );

			if ( line.OutlineSize < MinOutlineSize )
				return ConstructDataValidationResult.Failure( $"Outline size must be at least {MinOutlineSize}" );

			if ( line.OutlineSize > MaxOutlineSize )
				return ConstructDataValidationResult.Failure( $"Outline size exceeds maximum of {MaxOutlineSize}" );

			if ( line.FontWeight < MinFontWeight )
				return ConstructDataValidationResult.Failure( $"Font weight must be at least {MinFontWeight}" );

			if ( line.FontWeight > MaxFontWeight )
				return ConstructDataValidationResult.Failure( $"Font weight exceeds maximum of {MaxFontWeight}" );
		}

		if ( !hasContent )
			return ConstructDataValidationResult.Failure( "At least one line must have text" );

		return ConstructDataValidationResult.Success();
	}

	protected override GameObject CreateConstructInternal( TextData data, Vector3 position, Rotation rotation )
	{
		return GameObject.GetPrefab( "prefabs/constructs/text.prefab" ).Clone( position, rotation );
	}

	protected override bool CanOwnerPlace( long owner )
	{
		var player = GameUtils.GetPlayerById( owner );

		if ( Status.Current.HasStatus( owner, Constants.GaggedStatus ) )
		{
			player?.Warn( "#chat.gagged.restrict" );
			return false;
		}

		return true;
	}
}
