namespace Dxura.RP.Game.Wire;

public static class WireLabelHelper
{
	public const int MaxWireLabelLength = 50;

	public static ConstructDataValidationResult ValidateLabel( string? label )
	{
		if ( string.IsNullOrEmpty( label ) )
		{
			return ConstructDataValidationResult.Success();
		}

		if ( label.Length > MaxWireLabelLength )
		{
			return ConstructDataValidationResult.Failure( $"Label cannot exceed {MaxWireLabelLength} characters" );
		}

		return ConstructDataValidationResult.Success();
	}

	public static string FormatDisplayName( string? label, string defaultName )
	{
		if ( string.IsNullOrWhiteSpace( label ) )
		{
			return defaultName;
		}

		var trimmedLabel = label.Trim();
		var parenIndex = defaultName.IndexOf( '(' );
		if ( parenIndex >= 0 )
		{
			return $"{trimmedLabel} {defaultName[parenIndex..]}";
		}

		return trimmedLabel;
	}

	public static string? GetDisplayText( string? label )
	{
		return string.IsNullOrWhiteSpace( label ) ? null : label.Trim();
	}

	public static string? GetLabel( IWireComponent component )
	{
		if ( component is not Component c )
		{
			return null;
		}

		var construct = c.GameObject.Root.GetComponent<IConstruct>();
		if ( construct?.Data is not IWireLabelData labelData )
		{
			return null;
		}

		return GetDisplayText( labelData.Label );
	}
}
