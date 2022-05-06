using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class GraphicsPipelineOpenGL
    {
        readonly GL _gl;
        readonly GraphicsPipelineState _state;
        readonly PrimitiveType _primitiveType;

        public GraphicsPipelineOpenGL(GL gl, GraphicsPipelineState state)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _state = state;

            _primitiveType = _state.PrimitiveTopologyType switch
            {
                PrimitiveTopology.Point => PrimitiveType.Points,
                PrimitiveTopology.Line => PrimitiveType.Lines,
                PrimitiveTopology.Triangle => PrimitiveType.Triangles,
                _ => PrimitiveType.Triangles,
            };
        }

        public void Bind()
        {
            //Rasterizer State
            {
                //fill mode
                PolygonMode fillMode = _state.RasterizerState.Fill switch
                {
                    FillMode.Solid => PolygonMode.Fill,
                    FillMode.Wireframe => PolygonMode.Line,
                    _ => PolygonMode.Fill,
                };
                _gl.PolygonMode(MaterialFace.FrontAndBack, fillMode); //只能同时设置正面和背面
                DebugOpenGL.Check(_gl);
                //_gl.PolygonMode(MaterialFace.Back, fillMode);
                //DebugOpenGL.Check(_gl);

                //cull mode
                CullFaceMode cullMode = _state.RasterizerState.Cull switch
                {
                    CullMode.None => 0,
                    CullMode.Front => CullFaceMode.Front,
                    CullMode.Back => CullFaceMode.Back,
                    _ => 0,
                };
                if (cullMode == 0)
                {
                    _gl.Disable(EnableCap.CullFace);
                    DebugOpenGL.Check(_gl);
                }
                else
                {
                    _gl.Enable(EnableCap.CullFace);
                    DebugOpenGL.Check(_gl);
                    _gl.CullFace(cullMode);
                    DebugOpenGL.Check(_gl);
                }
                FrontFaceDirection isCCW = _state.RasterizerState.IsFrontCounterClockwise ? FrontFaceDirection.Ccw : FrontFaceDirection.CW;
                _gl.FrontFace(isCCW);
                DebugOpenGL.Check(_gl);

                //MSAA
                if (_state.RasterizerState.IsEnableMultisample)
                {
                    _gl.Enable(EnableCap.Multisample);
                    DebugOpenGL.Check(_gl);
                }
                else
                {
                    _gl.Disable(EnableCap.Multisample);
                    DebugOpenGL.Check(_gl);
                }
            }

            //Depth Stencil State
            {
                //depth test
                if (_state.DepthStencilState.IsEnableDepth)
                {
                    _gl.Enable(EnableCap.DepthTest);
                    DebugOpenGL.Check(_gl);
                    bool mask = _state.DepthStencilState.DepthMask switch
                    {
                        DepthWriteMask.Zero => false,
                        DepthWriteMask.All => true,
                        _ => true,
                    };
                    _gl.DepthMask(mask);
                    DebugOpenGL.Check(_gl);

                    DepthFunction func = _state.DepthStencilState.DepthFunc switch
                    {
                        ComparisonFunc.Never => DepthFunction.Never,
                        ComparisonFunc.Less => DepthFunction.Less,
                        ComparisonFunc.Equal => DepthFunction.Equal,
                        ComparisonFunc.LessEqual => DepthFunction.Lequal,
                        ComparisonFunc.Greater => DepthFunction.Greater,
                        ComparisonFunc.NotEqual => DepthFunction.Notequal,
                        ComparisonFunc.GreaterEqual => DepthFunction.Gequal,
                        ComparisonFunc.Always => DepthFunction.Always,
                        _ => DepthFunction.Less,
                    };
                    _gl.DepthFunc(func);
                    DebugOpenGL.Check(_gl);
                }
                else
                {
                    _gl.Disable(EnableCap.DepthTest);
                    DebugOpenGL.Check(_gl);
                }

                //stencil test
                if (_state.DepthStencilState.IsEnableStencil)
                {
                    DepthStencilState dss = _state.DepthStencilState;
                    StencilOpDescriptor frontDesc = dss.FrontFace;
                    StencilOpDescriptor backDesc = dss.BackFace;
                    static StencilFunction MapStencilCompToGL(ComparisonFunc func)
                    {
                        return func switch
                        {
                            ComparisonFunc.Never => StencilFunction.Never,
                            ComparisonFunc.Less => StencilFunction.Less,
                            ComparisonFunc.Equal => StencilFunction.Equal,
                            ComparisonFunc.LessEqual => StencilFunction.Lequal,
                            ComparisonFunc.Greater => StencilFunction.Greater,
                            ComparisonFunc.NotEqual => StencilFunction.Notequal,
                            ComparisonFunc.GreaterEqual => StencilFunction.Gequal,
                            ComparisonFunc.Always => StencilFunction.Always,
                            _ => StencilFunction.Always,
                        };
                    }
                    static Silk.NET.OpenGL.StencilOp MapStencilOpToGL(StencilOp op)
                    {
                        return op switch
                        {
                            StencilOp.Keep => Silk.NET.OpenGL.StencilOp.Keep,
                            StencilOp.Zero => Silk.NET.OpenGL.StencilOp.Zero,
                            StencilOp.Replace => Silk.NET.OpenGL.StencilOp.Replace,
                            StencilOp.IncrSat => Silk.NET.OpenGL.StencilOp.IncrWrap,
                            StencilOp.DecrSat => Silk.NET.OpenGL.StencilOp.DecrWrap,
                            StencilOp.Invert => Silk.NET.OpenGL.StencilOp.Invert,
                            StencilOp.Incr => Silk.NET.OpenGL.StencilOp.Incr,
                            StencilOp.Decr => Silk.NET.OpenGL.StencilOp.Decr,
                            _ => Silk.NET.OpenGL.StencilOp.Keep,
                        };
                    };
                    _gl.Enable(EnableCap.StencilTest);
                    DebugOpenGL.Check(_gl);
                    int @ref = (int)dss.StencilReadMask;
                    uint mask = dss.StencilWriteMask;
                    _gl.StencilFuncSeparate(StencilFaceDirection.Front, MapStencilCompToGL(frontDesc.Func), @ref, mask);
                    DebugOpenGL.Check(_gl);
                    _gl.StencilFuncSeparate(StencilFaceDirection.Back, MapStencilCompToGL(backDesc.Func), @ref, mask);
                    DebugOpenGL.Check(_gl);
                    _gl.StencilOpSeparate(StencilFaceDirection.Front, MapStencilOpToGL(frontDesc.FailOp), MapStencilOpToGL(frontDesc.DepthFailOp), MapStencilOpToGL(frontDesc.PassOp));
                    DebugOpenGL.Check(_gl);
                    _gl.StencilOpSeparate(StencilFaceDirection.Back, MapStencilOpToGL(backDesc.FailOp), MapStencilOpToGL(backDesc.DepthFailOp), MapStencilOpToGL(backDesc.PassOp));
                    DebugOpenGL.Check(_gl);
                }
                else
                {
                    _gl.Disable(EnableCap.StencilTest);
                    DebugOpenGL.Check(_gl);
                }
            }

            //Blend State
            {
                if (_state.BlendState.IsEnableBlend)
                {
                    _gl.Enable(EnableCap.Blend);
                    DebugOpenGL.Check(_gl);
                    BlendState state = _state.BlendState;
                    static BlendingFactor MapBlendColorToGL(BlendColor color)
                    {
                        return color switch
                        {
                            BlendColor.Zero => BlendingFactor.Zero,
                            BlendColor.One => BlendingFactor.One,
                            BlendColor.SrcColor => BlendingFactor.SrcColor,
                            BlendColor.InvSrcColor => BlendingFactor.OneMinusSrcColor,
                            BlendColor.SrcAlpha => BlendingFactor.SrcAlpha,
                            BlendColor.InvSrcAlpha => BlendingFactor.OneMinusSrcAlpha,
                            BlendColor.DestAlpha => BlendingFactor.DstAlpha,
                            BlendColor.InvDestAlpha => BlendingFactor.OneMinusDstAlpha,
                            BlendColor.DestColor => BlendingFactor.DstColor,
                            BlendColor.InvDestColor => BlendingFactor.OneMinusDstColor,
                            _ => BlendingFactor.Zero,
                        };
                    }
                    static BlendEquationModeEXT MapBlendOpToGL(BlendOperator op)
                    {
                        return op switch
                        {
                            BlendOperator.Add => BlendEquationModeEXT.FuncAdd,
                            BlendOperator.Subtract => BlendEquationModeEXT.FuncSubtract,
                            BlendOperator.RevSubtract => BlendEquationModeEXT.FuncReverseSubtract,
                            _ => BlendEquationModeEXT.FuncAdd,
                        };
                    }
                    _gl.BlendFuncSeparate(
                        MapBlendColorToGL(state.SrcBlend), MapBlendColorToGL(state.DestBlend),
                        MapBlendColorToGL(state.SrcBlendAlpha), MapBlendColorToGL(state.DestBlendAlpha));
                    DebugOpenGL.Check(_gl);
                    _gl.BlendEquationSeparate(MapBlendOpToGL(state.BlendOp), MapBlendOpToGL(state.BlendOpAlpha));
                    DebugOpenGL.Check(_gl);
                }
                else
                {
                    _gl.Disable(EnableCap.Blend);
                    DebugOpenGL.Check(_gl);
                }
            }
        }
    }
}
