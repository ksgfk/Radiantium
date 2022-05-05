namespace Radiantium.Realtime.Graphics
{
    #region Rasterizer
    public enum FillMode
    {
        Solid,
        Wireframe
    }

    public enum CullMode
    {
        None,
        Front,
        Back
    }

    public struct RasterizerState
    {
        public FillMode Fill;
        public CullMode Cull;
        public bool IsFrontCounterClockwise;
        public bool IsEnableMultisample;
        public bool IsEnableAntialiasedLine;

        public static RasterizerState Default => new RasterizerState()
        {
            Fill = FillMode.Solid,
            Cull = CullMode.Back,
            IsFrontCounterClockwise = true,
            IsEnableMultisample = false,
        };
    }
    #endregion

    #region Blend
    public enum BlendColor
    {
        Zero,
        One,
        SrcColor,
        InvSrcColor,
        SrcAlpha,
        InvSrcAlpha,
        DestAlpha,
        InvDestAlpha,
        DestColor,
        InvDestColor,
    }

    public enum BlendOperator
    {
        Add,
        Subtract,
        RevSubtract
    }

    public struct BlendState
    {
        public bool IsEnableBlend;
        public BlendColor SrcBlend;
        public BlendColor DestBlend;
        public BlendOperator BlendOp;
        public BlendColor SrcBlendAlpha;
        public BlendColor DestBlendAlpha;
        public BlendOperator BlendOpAlpha;

        public static BlendState Default => new BlendState()
        {
            IsEnableBlend = false,
            SrcBlend = BlendColor.One,
            DestBlend = BlendColor.Zero,
            BlendOp = BlendOperator.Add,
            SrcBlendAlpha = BlendColor.One,
            DestBlendAlpha = BlendColor.Zero,
            BlendOpAlpha = BlendOperator.Add
        };
    }
    #endregion

    #region DepthStencil
    public enum DepthWriteMask
    {
        Zero,
        All
    }

    public enum ComparisonFunc
    {
        Never,
        Less,
        Equal,
        LessEqual,
        Greater,
        NotEqual,
        GreaterEqual,
        Always
    }

    public enum StencilOp
    {
        Keep,
        Zero,
        Replace,
        IncrSat,
        DecrSat,
        Invert,
        Incr,
        Decr
    }

    public struct StencilOpDescriptor
    {
        public StencilOp FailOp;
        public StencilOp DepthFailOp;
        public StencilOp PassOp;
        public ComparisonFunc Func;
    }

    public struct DepthStencilState
    {
        public bool IsEnableDepth;
        public DepthWriteMask DepthMask;
        public ComparisonFunc DepthFunc;
        public bool IsEnableStencil;
        public uint StencilReadMask;
        public uint StencilWriteMask;
        public StencilOpDescriptor FrontFace;
        public StencilOpDescriptor BackFace;

        public static DepthStencilState Default
        {
            get
            {
                StencilOpDescriptor defaultStencilOp = new StencilOpDescriptor()
                {
                    FailOp = StencilOp.Keep,
                    DepthFailOp = StencilOp.Keep,
                    PassOp = StencilOp.Keep,
                    Func = ComparisonFunc.Always
                };
                return new DepthStencilState()
                {
                    IsEnableDepth = true,
                    DepthMask = DepthWriteMask.All,
                    DepthFunc = ComparisonFunc.Less,
                    IsEnableStencil = false,
                    StencilReadMask = 0xFF,
                    StencilWriteMask = 0xFF,
                    FrontFace = defaultStencilOp,
                    BackFace = defaultStencilOp,
                };
            }
        }
    }
    #endregion

    public enum PrimitiveTopology
    {
        Point,
        Line,
        Triangle,
    }

    public enum InputElementFormat
    {
        Float,
        FloatVec2,
        FloatVec3,
        FloatVec4,
        Int,
        IntVec2,
        IntVec3,
        IntVec4,
        UnsignedInt,
        UnsignedIntVec2,
        UnsignedIntVec3,
        UnsignedIntVec4
    }

    public struct InputElementLayout
    {
        public string Name;
        public string Semantic;
        public InputElementFormat Format;
        public uint InputSlot;
        public uint AlignedByteOffset;

        public InputElementLayout(string name, string semantic, InputElementFormat format, uint inputSlot, uint alignedByteOffset)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Semantic = semantic ?? throw new ArgumentNullException(nameof(semantic));
            Format = format;
            InputSlot = inputSlot;
            AlignedByteOffset = alignedByteOffset;
        }
    }

    public struct GraphicsPipelineState
    {
        public BlendState BlendState;
        public RasterizerState RasterizerState;
        public DepthStencilState DepthStencilState;
        public InputElementLayout[] InputLayout;
        public PrimitiveTopology PrimitiveTopologyType;
    }
}
