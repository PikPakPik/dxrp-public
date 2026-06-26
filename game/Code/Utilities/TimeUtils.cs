namespace Dxura.RP.Game;

public enum TimeDisplayFormat
{
	/// <summary>Compact elapsed time: 5m 30s, 30s</summary>
	Duration,

	/// <summary>Long-running uptime: 2h 15m, 1d 3h 20m</summary>
	Uptime,

	/// <summary>Media clock: 5:30, 1:05:30</summary>
	Clock,

	/// <summary>Padded countdown: 05:30</summary>
	Countdown,

	/// <summary>Compact countdown: 5:30</summary>
	CountdownShort,

	/// <summary>Compact hours played: 45m, 2.5h, 12h</summary>
	HoursCompact,
}

public static class TimeUtils
{
	public static string Format( float seconds, TimeDisplayFormat format = TimeDisplayFormat.Duration ) =>
		format switch
		{
			TimeDisplayFormat.Duration => FormatDuration( seconds ),
			TimeDisplayFormat.Uptime => FormatUptime( seconds ),
			TimeDisplayFormat.Clock => FormatClock( seconds ),
			TimeDisplayFormat.Countdown => FormatCountdown( seconds ),
			TimeDisplayFormat.CountdownShort => FormatCountdown( seconds, padMinutes: false ),
			TimeDisplayFormat.HoursCompact => FormatHoursCompact( seconds ),
			_ => FormatDuration( seconds ),
		};

	public static string Format( TimeSince timeSince, TimeDisplayFormat format = TimeDisplayFormat.Duration ) =>
		Format( timeSince.Relative, format );

	public static string Format( TimeSpan timeSpan, TimeDisplayFormat format = TimeDisplayFormat.Duration ) =>
		Format( (float)timeSpan.TotalSeconds, format );

	/// <summary>Compact elapsed time: 5m 30s, 30s</summary>
	public static string FormatDuration( float seconds )
	{
		var totalSeconds = (int)Math.Ceiling( Math.Max( 0, seconds ) );
		var minutes = totalSeconds / 60;
		var secs = totalSeconds % 60;

		if ( minutes > 0 )
		{
			return $"{minutes}m {secs}s";
		}

		return $"{secs}s";
	}

	/// <summary>Long-running uptime: 2h 15m, 1d 3h 20m</summary>
	public static string FormatUptime( float seconds )
	{
		var ts = TimeSpan.FromSeconds( Math.Max( 0, seconds ) );

		return ts.TotalHours >= 24
			? $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m"
			: $"{(int)ts.TotalHours}h {ts.Minutes}m";
	}

	/// <summary>Media clock: 5:30, 1:05:30</summary>
	public static string FormatClock( float seconds )
	{
		var ts = TimeSpan.FromSeconds( Math.Max( 0, seconds ) );

		return ts.TotalHours >= 1
			? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
			: $"{ts.Minutes}:{ts.Seconds:D2}";
	}

	/// <summary>Countdown timer: 05:30 or 5:30</summary>
	public static string FormatCountdown( float seconds, bool padMinutes = true )
	{
		var totalSeconds = Math.Max( 0, (int)seconds );
		var minutes = totalSeconds / 60;
		var secs = totalSeconds % 60;

		return padMinutes
			? $"{minutes:D2}:{secs:D2}"
			: $"{minutes}:{secs:D2}";
	}

	/// <summary>Compact hours played: 45m, 2.5h, 12h</summary>
	public static string FormatHoursCompact( float seconds )
	{
		var minutes = seconds / 60.0;

		if ( minutes < 60 )
		{
			return minutes.ToString( "0m" );
		}

		var hours = seconds / 3600.0;

		return hours > 10
			? hours.ToString( "0h" )
			: hours.ToString( "0.#h" );
	}

	/// <summary>Relative timestamp: just now, 5 mins ago, 2h ago</summary>
	public static string FormatRelative( DateTimeOffset timestamp )
	{
		var elapsed = DateTimeOffset.Now - timestamp.ToLocalTime();

		if ( elapsed <= TimeSpan.Zero )
		{
			return "just now";
		}

		if ( elapsed < TimeSpan.FromMinutes( 1 ) )
		{
			return "<1 min ago";
		}

		if ( elapsed < TimeSpan.FromHours( 1 ) )
		{
			var minutes = (int)Math.Floor( elapsed.TotalMinutes );
			return $"{minutes} min{(minutes == 1 ? string.Empty : "s")} ago";
		}

		if ( elapsed < TimeSpan.FromDays( 1 ) )
		{
			var hours = (int)Math.Floor( elapsed.TotalHours );
			var minutes = elapsed.Minutes;

			return minutes == 0
				? $"{hours}h ago"
				: $"{hours}h {minutes}m ago";
		}

		var days = (int)Math.Floor( elapsed.TotalDays );
		return $"{days} day{(days == 1 ? string.Empty : "s")} ago";
	}

	public static string FormatAbsolute( DateTimeOffset timestamp ) =>
		timestamp.LocalDateTime.ToString( "g" );
}
