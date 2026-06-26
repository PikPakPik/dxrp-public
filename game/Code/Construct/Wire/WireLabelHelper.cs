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
		return string.IsNullOrWhiteSpace( label ) ? defaultName : label.Trim();
	}

	public static string? GetDisplayText( string? label )
	{
		return string.IsNullOrWhiteSpace( label ) ? null : label.Trim();
	}
}
