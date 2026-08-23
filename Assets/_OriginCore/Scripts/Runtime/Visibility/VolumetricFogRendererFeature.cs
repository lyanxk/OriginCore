using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OriginCore.Visibility
{
    public sealed class VolumetricFogRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material _material;
        [SerializeField] private string _cameraTag = "MainCamera";

        private BlackFogOfWarPass _pass;

        public Material FogMaterial => _material;
        public string CameraTag => _cameraTag;
        public bool IsConfigured => _material != null &&
                                    !string.IsNullOrWhiteSpace(_cameraTag);

        public void Configure(Material material, string cameraTag = "MainCamera")
        {
            _material = material;
            _cameraTag = string.IsNullOrWhiteSpace(cameraTag)
                ? "MainCamera"
                : cameraTag;
            Create();
        }

        public override void Create()
        {
            if (_pass == null)
            {
                _pass = new BlackFogOfWarPass();
            }

            _pass.SetMaterial(_material);
        }

        public override void SetupRenderPasses(
            ScriptableRenderer renderer,
            in RenderingData renderingData)
        {
            if (ShouldRender(renderingData.cameraData))
            {
                _pass.SetTarget(renderer.cameraColorTargetHandle);
            }
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (ShouldRender(renderingData.cameraData))
            {
                renderer.EnqueuePass(_pass);
            }
        }

        private bool ShouldRender(CameraData cameraData)
        {
            Camera camera = cameraData.camera;
            return _pass != null && IsConfigured &&
                   VisibilitySystem.HasActiveFogOfWar &&
                   camera != null && cameraData.cameraType == CameraType.Game &&
                   camera.targetTexture == null && camera.CompareTag(_cameraTag);
        }

        private sealed class BlackFogOfWarPass : ScriptableRenderPass
        {
            private Material _material;
            private RTHandle _target;

            public BlackFogOfWarPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void SetMaterial(Material material)
            {
                _material = material;
            }

            public void SetTarget(RTHandle target)
            {
                _target = target;
            }

            public override void OnCameraSetup(
                CommandBuffer commandBuffer,
                ref RenderingData renderingData)
            {
                if (_target != null)
                {
                    ConfigureTarget(_target);
                }
            }

            public override void Execute(
                ScriptableRenderContext context,
                ref RenderingData renderingData)
            {
                if (_material == null || _target == null ||
                    !VisibilitySystem.HasActiveFogOfWar)
                {
                    return;
                }

                CommandBuffer commandBuffer = CommandBufferPool.Get();
                CoreUtils.DrawFullScreen(commandBuffer, _material);
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }

            public override void OnCameraCleanup(CommandBuffer commandBuffer)
            {
                _target = null;
            }
        }
    }
}
