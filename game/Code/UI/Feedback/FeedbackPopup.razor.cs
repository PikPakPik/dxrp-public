using System.Threading.Tasks;

namespace Dxura.RP.Game.UI;

public partial class FeedbackPopup
{
	private bool _capturing;

	public static void Open()
	{
		Sandbox.Game.Overlay.CloseAll();
		GameManager.ShowUi<FeedbackPopup>();
	}

	private void Close()
	{
		Destroy();
	}

	// Hides this popup for a couple of frames so the resulting screenshot shows the rest of the
	// game/HUD underneath, then restores it. Runs on the same GameObject as the HUD, so everything
	// else (chat, health, etc.) stays visible in the capture.
	private async Task<byte[]?> CaptureScreenshot()
	{
		if ( !Scene.Camera.IsValid() )
		{
			return null;
		}

		_capturing = true;
		StateHasChanged();

		// Give the hidden state a couple of real frames to actually render before capturing.
		await GameTask.DelayRealtime( 50 );

		var texture = Texture.CreateRenderTarget().WithSize( (int)Screen.Width, (int)Screen.Height ).Create();
		byte[] png;

		try
		{
			Scene.Camera.RenderToTexture( texture );

			await GameTask.WorkerThread();
			var bitmap = texture.GetBitmap( 0 );
			png = bitmap.ToPng();
		}
		finally
		{
			texture.Dispose();
		}

		await GameTask.MainThread();
		_capturing = false;
		StateHasChanged();

		return png;
	}
}
