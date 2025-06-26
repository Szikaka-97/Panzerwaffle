using System;
using Sandbox;
using Sandbox.Rendering;

namespace Panzerwaffle.TankControl {
	public sealed class GunnerSight : Component {
		private struct RuntimeShaderParams {
			public uint RangeMonitorReadout;
			public float TurretRotation;
		}

		[Property]
		public CameraComponent SightCamera { get; private set; }
		[Property]
		public ModelRenderer SightModel { get; init; }

		private Texture sightTexture;
		private Material sightMaterial;
		private Material overlayMaterial;
		private RuntimeShaderParams shaderParams;

		protected override void OnAwake() {
			this.sightTexture = Texture.CreateRenderTarget("Tank Sight Rendertarget", ImageFormat.RGBA8888, new Vector2(512, 512));
			SightCamera.RenderTarget = this.sightTexture;

			ModelRenderer model = SightModel.GetComponent<ModelRenderer>();

			this.sightMaterial = Enumerable.Range(0, model.Materials.Count).Select(model.Materials.GetOriginal).First( mat => mat.Name.Contains("screen") );
			this.overlayMaterial = Enumerable.Range(0, model.Materials.Count).Select(model.Materials.GetOriginal).First( mat => mat.Name.Contains("overlay") );
			
			if (!sightMaterial.Set("Color", this.sightTexture)) {
				Log.Error("Could not set RenderTexture");
			}
		}

		public void UpdateDisplay(
			float? turretRotation = null,
			uint? rangeMonitorReadout = null,
			bool? aligned = null,
			bool? readyToFire = null
		) {
			if (turretRotation.HasValue) {
				shaderParams.TurretRotation = MathX.DegreeToRadian(turretRotation.Value);
			}
			if (rangeMonitorReadout.HasValue) {
				shaderParams.RangeMonitorReadout = rangeMonitorReadout.Value;
			}
			
			this.overlayMaterial.Set("RangeMonitorReadout", shaderParams.RangeMonitorReadout);
			this.overlayMaterial.Set("TurretRotation", shaderParams.TurretRotation);
		}
	}
}