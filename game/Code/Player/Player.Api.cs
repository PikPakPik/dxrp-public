// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch DXRP editor overlay — gates player pulse until lp_authorize applies tenant ID.
// Sync: lifepunch/scripts/Sync-DxrpEditorOverlays.ps1
// ─────────────────────────────────────────────────────────────────────────────

using Sandbox.Engine;
using System.Threading.Tasks;

namespace Dxura.RP.Game;

public partial class Player
{
	private TimeSince LastPulse { get; set; } = 0;
	private bool IsPulsing { get; set; }

	private void OnSecondlyUpdateApi()
	{
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return;
		}

		// lp_authorize sets Token before InitializeServer returns TenantId — one stray pulse 403s without this gate.
		if ( !ServerApiLink.IsPortalTenantReady )
		{
			return;
		}

		if ( !IsPulsing && LastPulse > 60 )
		{
			_ = DoPulse();
		}
	}

	private async Task DoPulse()
	{
		IsPulsing = true;

		try
		{
			var hwid = $"{SystemInfo.ProcessorName?.Trim().ToLowerInvariant() ?? "unknown"}|{SystemInfo.ProcessorCount}|{SystemInfo.Gpu?.Trim().ToLowerInvariant() ?? "unknown"}|{SystemInfo.GpuMemory}|{SystemInfo.TotalMemory}";
			var response = await PlayerApiClient.Pulse( HasStatus( Constants.AfkStatus ), hwid );

			// If null response, something went wrong but we won't kick
			if ( response == null )
			{
				LastPulse = 0;
				IsPulsing = false;

				return;
			}

			if ( !response.Permitted )
			{
				Chat.Current.SubmitPlayerChat( "This is not a supported server, please join another!", MessageType.GlobalChat );
				await GameTask.DelayRealtimeSeconds( 3 );
				Sandbox.Game.Close();
				return;
			}

			LastPulse = 0;
			IsPulsing = false;
		}
		catch ( Exception ex )
		{
			Log.Error( $"Player pulse failed with exception: {ex.Message}" );
			LastPulse = 0;
			IsPulsing = false;
		}
	}
}
