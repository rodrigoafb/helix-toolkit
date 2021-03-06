﻿/*
The MIT License (MIT)
Copyright (c) 2018 Helix Toolkit contributors
*/
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
#if !NETFX_CORE
namespace HelixToolkit.Wpf.SharpDX.Core
#else
namespace HelixToolkit.UWP.Core
#endif
{
    using Shaders;
    using Utilities;
    using Render;

    public class CrossSectionMeshRenderCore : PatchMeshRenderCore, ICrossSectionRenderParams
    {
        #region Shader Variables

        /// <summary>
        /// Used to draw back faced triangles onto stencil buffer
        /// </summary>
        private RasterizerStateProxy backfaceRasterState;

        #endregion
        #region Properties
        /// <summary>
        /// Defines the sectionColor
        /// </summary>       
        public Color4 SectionColor
        {
            set
            {
                SetAffectsRender(ref clipParameter.CrossSectionColors, value);
            }
            get { return clipParameter.CrossSectionColors.ToColor4(); }
        }

        /// <summary>
        /// Defines the plane1Enabled
        /// </summary>
        public bool Plane1Enabled
        {
            set
            {
                if(clipParameter.EnableCrossPlane.X != value)
                {
                    clipParameter.EnableCrossPlane.X = value;
                    InvalidateRenderer();
                }
            }
            get { return clipParameter.EnableCrossPlane.X; }
        }

        /// <summary>
        /// Defines the plane2Enabled
        /// </summary>
        public bool Plane2Enabled
        {
            set
            {
                if (clipParameter.EnableCrossPlane.Y != value)
                {
                    clipParameter.EnableCrossPlane.Y = value;
                    InvalidateRenderer();
                }
            }
            get { return clipParameter.EnableCrossPlane.Y; }
        }

        /// <summary>
        /// Defines the plane3Enabled
        /// </summary>
        public bool Plane3Enabled
        {
            set
            {
                if (clipParameter.EnableCrossPlane.Z != value)
                {
                    clipParameter.EnableCrossPlane.Z = value;
                    InvalidateRenderer();
                }
            }
            get { return clipParameter.EnableCrossPlane.Z; }
        }

        /// <summary>
        /// Defines the plane4Enabled
        /// </summary>
        public bool Plane4Enabled
        {
            set
            {
                if (clipParameter.EnableCrossPlane.W != value)
                {
                    clipParameter.EnableCrossPlane.W = value;
                    InvalidateRenderer();
                }
            }
            get { return clipParameter.EnableCrossPlane.W; }
        }

        /// <summary>
        /// Defines the plane 1(Normal + d)
        /// </summary>
        public Vector4 Plane1Params
        {
            set
            {
                if(clipParameter.CrossPlaneParams.Row1 != value)
                {
                    clipParameter.CrossPlaneParams.Row1 = value;
                    InvalidateRenderer();
                }
            }
            get
            {
                return clipParameter.CrossPlaneParams.Row1;
            }
        }

        /// <summary>
        /// Defines the plane 2(Normal + d)
        /// </summary>
        public Vector4 Plane2Params
        {
            set
            {
                if (clipParameter.CrossPlaneParams.Row2 != value)
                {
                    clipParameter.CrossPlaneParams.Row2 = value;
                    InvalidateRenderer();
                }
            }
            get
            {
                return clipParameter.CrossPlaneParams.Row2;
            }
        }

        /// <summary>
        /// Defines the plane 3(Normal + d)
        /// </summary>
        public Vector4 Plane3Params
        {
            set
            {
                if (clipParameter.CrossPlaneParams.Row3 != value)
                {
                    clipParameter.CrossPlaneParams.Row3 = value;
                    InvalidateRenderer();
                }
            }
            get
            {
                return clipParameter.CrossPlaneParams.Row3;
            }
        }

        /// <summary>
        /// Defines the plane 4(Normal + d)
        /// </summary>
        public Vector4 Plane4Params
        {
            set
            {
                if (clipParameter.CrossPlaneParams.Row4 != value)
                {
                    clipParameter.CrossPlaneParams.Row4 = value;
                    InvalidateRenderer();
                }
            }
            get
            {
                return clipParameter.CrossPlaneParams.Row4;
            }
        }

        #endregion

        private ClipPlaneStruct clipParameter = new ClipPlaneStruct() { EnableCrossPlane = new Bool4(false, false, false, false), CrossSectionColors = Color.Blue.ToVector4(), CrossPlaneParams = new Matrix() };

        private ConstantBufferProxy clipParamCB;

        private ShaderPass drawBackfacePass;
        private ShaderPass drawScreenQuadPass;

        protected override bool OnAttach(IRenderTechnique technique)
        {
            if (base.OnAttach(technique))
            {
                clipParamCB = technique.ConstantBufferPool.Register(GetClipParamsCBDescription());
                drawBackfacePass = technique[DefaultPassNames.Backface];
                drawScreenQuadPass = technique[DefaultPassNames.ScreenQuad];
                return true;
            }
            else { return false; }
        }

        protected virtual ConstantBufferDescription GetClipParamsCBDescription()
        {
            return new ConstantBufferDescription(DefaultBufferNames.ClipParamsCB, ClipPlaneStruct.SizeInBytes);
        }

        protected override bool CreateRasterState(RasterizerStateDescription description, bool force)
        {
            if(!base.CreateRasterState(description, force))
            {
                return false;
            }
            #region Create states
            RemoveAndDispose(ref backfaceRasterState);
            this.backfaceRasterState = Collect(EffectTechnique.EffectsManager.StateManager.Register(new RasterizerStateDescription()
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.Front,
                DepthBias = description.DepthBias,
                DepthBiasClamp = description.DepthBiasClamp,
                SlopeScaledDepthBias = description.SlopeScaledDepthBias,
                IsDepthClipEnabled = description.IsDepthClipEnabled,
                IsFrontCounterClockwise = description.IsFrontCounterClockwise,
                IsMultisampleEnabled = false,
                IsScissorEnabled = false
            }));
            #endregion
            return true;
        }

        protected override void OnUploadPerModelConstantBuffers(DeviceContextProxy context)
        {
            base.OnUploadPerModelConstantBuffers(context);
            clipParamCB.UploadDataToBuffer(context, ref clipParameter);
        }

        protected override void OnRender(RenderContext renderContext, DeviceContextProxy deviceContext)
        {
            base.OnRender(renderContext, deviceContext);
            // Draw backface into stencil buffer
            var dsView = renderContext.RenderHost.DepthStencilBufferView;
            deviceContext.ClearDepthStencilView(dsView, DepthStencilClearFlags.Stencil, 0, 0);
            deviceContext.SetDepthStencilOnly(dsView);//Remove render target
            deviceContext.SetRasterState(backfaceRasterState);
            drawBackfacePass.BindShader(deviceContext);
            drawBackfacePass.BindStates(deviceContext, StateType.BlendState);
            deviceContext.SetDepthStencilState(drawBackfacePass.DepthStencilState, 1); //Draw backface onto stencil buffer, set value to 1
            OnDraw(deviceContext, InstanceBuffer);

            //Draw full screen quad to fill cross section            
            deviceContext.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            deviceContext.SetRasterState(RasterState);
            drawScreenQuadPass.BindShader(deviceContext);
            drawScreenQuadPass.BindStates(deviceContext, StateType.BlendState);
            renderContext.RenderHost.SetDefaultRenderTargets(false);//Rebind render target
            deviceContext.SetDepthStencilState(drawScreenQuadPass.DepthStencilState, 1); //Only pass stencil buffer test if value is 1
            deviceContext.Draw(4, 0);
        }
    }
}
