
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// 화면을 낮은 해상도로 다운샘플링한 뒤 다시 확대하여 픽셀아트 느낌을 내는 패스입니다.
/// URP 17(Unity 6)에서 Compatibility Mode가 제거되어 RenderGraph API로 구현되었습니다.
/// </summary>
public class PixelizePass : ScriptableRenderPass
{
    private PixelizeFeature.CustomPassSettings settings;

    private Material material;

    // 셰이더(Hidden/Pixelize)가 사용하는 프로퍼티 이름들입니다.
    // 소스 텍스처는 Blit 규약에 따라 _BlitTexture로 자동 바인딩됩니다.
    private static readonly int blockCountID = Shader.PropertyToID("_BlockCount");
    private static readonly int blockSizeID = Shader.PropertyToID("_BlockSize");
    private static readonly int halfBlockSizeID = Shader.PropertyToID("_HalfBlockSize");

    public PixelizePass(PixelizeFeature.CustomPassSettings settings)
    {
        this.settings = settings;
        this.renderPassEvent = settings.renderPassEvent;

        // 화면(활성 컬러 타겟)을 텍스처로 읽어야 하므로 중간 타겟을 강제합니다.
        // 이 값이 false면 URP가 백버퍼에 직접 그려 이 패스를 사용할 수 없습니다.
        requiresIntermediateTexture = true;

        if (material == null) material = CoreUtils.CreateEngineMaterial("Hidden/Pixelize");
    }

    /// <summary>
    /// 인스펙터에서 설정이 변경되었을 때 최신 값을 반영합니다.
    /// </summary>
    public void Setup(PixelizeFeature.CustomPassSettings settings)
    {
        this.settings = settings;
        this.renderPassEvent = settings.renderPassEvent;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (material == null) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        // requiresIntermediateTexture = true 이므로 정상적으로는 발생하지 않습니다.
        // (renderPassEvent를 AfterRendering으로 두면 백버퍼만 남아 진입할 수 있습니다)
        if (resourceData.isActiveTargetBackBuffer) return;

        TextureHandle source = resourceData.activeColorTexture;
        if (!source.IsValid()) return;

        // 픽셀 버퍼 해상도를 계산합니다.
        int pixelScreenHeight = settings.screenHeight;
        if (pixelScreenHeight < 1) pixelScreenHeight = 1;

        int pixelScreenWidth = (int)(pixelScreenHeight * cameraData.camera.aspect + 0.5f);
        if (pixelScreenWidth < 1) pixelScreenWidth = 1;

        material.SetVector(blockCountID, new Vector2(pixelScreenWidth, pixelScreenHeight));
        material.SetVector(blockSizeID, new Vector2(1.0f / pixelScreenWidth, 1.0f / pixelScreenHeight));
        material.SetVector(halfBlockSizeID, new Vector2(0.5f / pixelScreenWidth, 0.5f / pixelScreenHeight));

        // 저해상도 중간 버퍼를 생성합니다.
        // 화면 텍스처의 서술자를 그대로 복사해 포맷/XR 설정을 유지하고 크기만 줄입니다.
        TextureDesc pixelDesc = renderGraph.GetTextureDesc(source);
        pixelDesc.name = "_PixelBuffer";
        pixelDesc.width = pixelScreenWidth;
        pixelDesc.height = pixelScreenHeight;
        pixelDesc.clearBuffer = false;
        pixelDesc.msaaSamples = MSAASamples.None;
        // 확대할 때 픽셀이 뭉개지지 않도록 최근접 필터를 사용합니다.
        pixelDesc.filterMode = FilterMode.Point;
        pixelDesc.wrapMode = TextureWrapMode.Clamp;

        TextureHandle pixelBuffer = renderGraph.CreateTexture(pixelDesc);

        // 1) 화면 -> 저해상도 버퍼 (셰이더가 블록 중심을 샘플링합니다)
        //    셰이더가 Blit.hlsl의 Vert(SV_VertexID 풀스크린 삼각형)를 사용하므로
        //    반드시 ProceduralTriangle과 기본 _BlitTexture 바인딩을 써야 합니다.
        RenderGraphUtils.BlitMaterialParameters blitParameters =
            new RenderGraphUtils.BlitMaterialParameters(
                source,
                pixelBuffer,
                material,
                shaderPass: 0,
                mpb: null,
                geometry: RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);

        renderGraph.AddBlitPass(blitParameters, "Pixelize Pass");

        // 2) 저해상도 버퍼 -> 화면 (계단 픽셀을 유지하기 위해 최근접 확대)
        renderGraph.AddBlitPass(
            pixelBuffer,
            source,
            Vector2.one,
            Vector2.zero,
            filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
            passName: "Pixelize Upscale");
    }
}
